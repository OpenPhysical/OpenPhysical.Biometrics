# Preparing a PIV portrait

`biotool face prepare` locates a face, levels its eye line, and selects a source-supported Full Frontal crop. It writes a 480 × 640 sRGB portrait using JPEG 2000. MediaPipe supplies facial geometry; the crop service applies the PIV rules. Review the result against the capture before issuance.

```sh
# Prepare a portrait and a lossless preview for the operator.
biotool face prepare capture.jpg --output portrait.jp2 --preview portrait.png

# Inspect detailed geometry when a capture needs review. Coordinates refer to
# the image after EXIF orientation has been applied, with (0,0) at the top left.
biotool face prepare capture.jpg --output portrait.jp2 --json --include-sensitive

# Supply reviewed head boundaries and explicitly choose the face.
biotool face prepare capture.jpg --face-index 0 --geometry reviewed.json \
  --output portrait.jp2 --preview portrait.png --overwrite

# Produce the INCITS 385 record using the same encoded portrait and explicit
# acquisition evidence. Signing is coordinated by the calling application.
biotool face prepare capture.jpg --geometry reviewed.json \
  --output portrait.jp2 --record face.bin --acquisition capture.json
biotool inspect face.bin --json
```

Detections are indexed from zero by horizontal center, then vertical center. A single face is selected automatically. Several faces require `--face-index` or a geometry file identifying the selected face. Exit codes are 0 for completed output, 2 for an expected selection/review outcome, and 1 for invalid inputs or execution failure. Existing files require `--overwrite`; outputs are staged beside their destination and rolled back on a failed installation. Staged image files use owner-only permissions on Unix.

The default report contains status, face count, dimensions, and diagnostic codes. `--include-sensitive` explicitly includes source hash, landmarks, measurements, and transforms. Request a lossless preview with `--preview`. Application logs contain operational status.

## Reviewed geometry

```json
{
  "Version": 1,
  "SourceSha256": "replace-with-the-source-hash-from-the-detailed-report",
  "Width": 1200,
  "Height": 1600,
  "FaceIndex": 0,
  "Crown": { "X": 600, "Y": 400 },
  "ViewerLeftEar": { "X": 440, "Y": 650 },
  "ViewerRightEar": { "X": 760, "Y": 650 }
}
```

Enter coordinates measured from the orientation-corrected capture. The crown is the top of the hair; ear points identify the attachment positions used to measure head width. `Chin` is optional. Partial overrides retain automatic estimates for the other points. Automatic crown estimation extends upward from the eye line by 0.9 times the eye-to-chin distance. The report marks estimated head boundaries, and record creation requires reviewed crown and both ear attachments. The mesh identifies the forehead; the crown marks the top of the hair.

The source hash and oriented dimensions bind the review to the exact image. A source or selection mismatch is rejected. Geometry review is independent of acquisition confirmation:

```json
{
  "CapturedAt": "2026-10-31T12:00:00Z",
  "DeviceType": 1,
  "SourceType": 2,
  "FullFrontalConfirmed": true,
  "OpticalResolutionConfirmed": true,
  "Quality": 0
}
```

The acquisition operator supplies those findings from the actual camera and capture procedure. The preparation result preserves those acquisition findings.

## Rules and sources

The controlling references are the local SP 800-76-2 §7.2, Table 14 notes, and INCITS 385-2004 §§7–8. The reference documents stay in the Reference Library; this table records mandatory profile rules. Approximate horizontal centering uses an explicit application tolerance. Output dimensions, preferred eye placement, and preferred head width are application defaults, described below. Acquisition confirmation covers the mandatory optical-resolution test: a 20 cm target normal to the optical axis at 1.5 m must span at least 240 pixels. The recommendation to place the subject at least 1.5 m from the camera is recorded separately from the pixel-derived crop measurements.

| Rule | Basis | Implementation |
| --- | --- | --- |
| Full Frontal type | SP 800-76-2 §7.2 note 4; INCITS §8 | Encode image type 1. |
| Centering and eyes | INCITS §§8.3.2–8.3.3 | Center nose-bridge/mouth midpoint; each within 2% of image width as an application tolerance. Eyes 30–50% down from the top. |
| Head extents | INCITS §§8.3.4–8.3.5 | Image/head-width ratio ≥7:4; crown-to-chin ≤80% of image height. |
| Resolution | SP 800-76-2 §7.2 note 7 | Width >420; head width ≥240 output pixels; scale ≤1. Acquisition confirmation remains explicit. |
| Color | SP 800-76-2 §7.2 note 8 | Decode with source profile into sRGB; apply EXIF orientation before analysis. Untagged images use the decoder's sRGB interpretation. |
| Compression | SP 800-76-2 §7.2 notes 5–6 | JPEG 2000, whole-image ratio ≤15:1; decode-check dimensions and components. |

Application defaults are 480×640, a target eye line at 40% from the top, and a target head width of 256 pixels. The solver intersects the linear constraints in scale and eye-line position, then chooses the point in the feasible polygon nearest those two targets in normalized squared-distance units (16 pixels for head width and 64 pixels for eye placement). Every candidate must fit the source and all geometric constraints. One uniform affine resampling produces the portrait. Model-input resampling is a separate operation and can use zero padding; final portrait sampling requires source coverage.

## Implementation and provenance

`PortraitPreparer` is a synchronous, disposable service registered by `AddPortraitPreparation()`. It loads the pinned models lazily and serializes inference with disposal. Results own immutable pixel buffers and mesh points. `PortraitEncoding` supplies PNG previews and JPEG 2000 encoding; `FacialRecordPreparer` shares that encoder.

The model source is yakhyo/mediapipe-face-mesh-onnx commit `3bdf1e0d100a4c709766c051a0c9e9cdab1fed62`, with Google's original MediaPipe weights. Package provenance records SHA-256 hashes, and runtime loading checks them. Apache-2.0 notices accompany the model package; first-party code carries the additional MediaPipe combination permission in `LICENSE-MEDIAPIPE-EXCEPTION`.

Use `Microsoft.ML.OnnxRuntime` CPU inference. Unsupported native-runtime deployments fail explicitly when initializing the model. Facial sessions initialize on the first portrait-preparation call.


## Troubleshooting

`NoFace` asks for a capture with a clearly visible face. `SelectFace` asks for an
explicit index when the report contains several detections. `HeadGeometry`,
`HeadProportions`, and `SourceCoverage` identify crops needing reviewed boundaries
or a new capture with sufficient resolution and surrounding image area.

`InvalidPortraitInput` covers malformed images, invalid selections, and reviewed
coordinates associated with a different source. `InvalidReviewMetadata`
identifies malformed JSON. `PortraitFileAccessFailed` identifies input/output
access failures. Use `--overwrite` to replace an existing output.
`NativeRuntimeUnavailable` identifies missing or incompatible ONNX Runtime or
Skia native assets. Packages support Windows x64, Linux x64/ARM64, and macOS
x64/ARM64. Install the package matching the process architecture.
