# Biometric quality assessment

Quality assessments help the capture workflow choose an attempt, request another
capture, and present findings to the operator. Keep the assessment with its source
capture and the application's policy version so a later decision has its original
context.

## Fingerprint capture quality

`IFingerprintQualityProvider` supplies the original image's NFIQ 1 class and its
algorithm or model identity. `PivFingerprintEnrollmentService.Prepare` keeps that
assessment with the extracted minutiae. Its ranker selects the best attempt for
each finger, then orders candidates by quality and the preferred-hand sequence.
The PIV encoding uses `20 * (6 - NFIQ1)`.

The NFIQ packages expose the NIST quality engines, and the
[fingerprint enrollment examples](FINGERPRINT-WORKFLOWS.md) show quality-aware
selection and explicit primary/secondary fingers. Applications define their
reacquisition and acceptance policies.

## Facial capture quality

The optional `.Facial.Infrastructure` pipeline combines three assessments:

- **Symmetry** compares mirrored facial regions with Gabor filters at five
  orientations and reports illumination and pose findings.
- **Sharpness** uses DCT frequency content and regional scores to help identify
  blur and loss of detail.
- **Geometry** measures landmark positions, face proportions, and centering in
  image coordinates against the selected processing profile.

Scores are normalized from zero through one. The application selects the minimum
score and enforcement settings for its capture workflow. Regional findings help
an operator decide whether to adjust focus, lighting, framing, or subject pose.

## Assess an owned image

Register `AddBiometricServices` from the optional facial package, then resolve its
`IFaceDetector`, `ILandmarkExtractor`, and `IQualityAssessor`. Each service accepts
the shared `RgbImage` type.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Imaging;

static async Task<Iso19794Assessment> AssessPortraitAsync(
    string path,
    IServiceProvider services,
    float minimumQuality)
{
    using var image = RgbImage.Load(path);
    var detected = await services.GetRequiredService<IFaceDetector>()
        .DetectFacesAsync(image);
    if (detected.IsFailure || detected.Value.Count == 0)
        throw new InvalidOperationException("Select a capture containing a detectable face.");

    var subject = detected.Value.OrderByDescending(face => face.Confidence).First();
    var landmarks = await services.GetRequiredService<ILandmarkExtractor>()
        .ExtractLandmarksAsync(image, subject.BoundingBox);
    if (landmarks.IsFailure)
        throw new InvalidOperationException("Request another capture for landmark assessment.");

    var options = QualityAssessmentOptions.ForStandard("piv") with
    {
        MinQualityThreshold = minimumQuality,
        EnforceCompliance = true,
    };
    var result = await services.GetRequiredService<IQualityAssessor>()
        .AssessAsync(image, subject, landmarks.Value, options);
    if (result.IsFailure)
        throw new InvalidOperationException("The capture requires operator review.");
    return result.Value;
}
```

The returned assessment includes the overall score, component results, and typed
violations. Present those findings in the authorized capture interface and retain
them with the application's decision.

## Record acquisition facts and check the PIV profile

Facial preparation records the actual camera source, device, acquisition instant,
quality, and operator confirmations in `FacialAcquisitionMetadata`. Supply the
original image geometry and confirmed optical capture information.

`FacialRecordPreparer` encodes the sRGB portrait as JPEG 2000 and checks its
canonical metadata with `PivBiometricProfile`. Typed outcomes distinguish a
prepared record, acquisition review, profile rejection, and compression rejection.
The encoded facial profile requires width greater than 420 pixels; the capture
workflow selects its portrait geometry explicitly.

The [API guide](API.md) shows preparation and the signature handoff. Issuance
combines capture findings, record-profile checks, identity binding, and the
application's signing and trust policy.

## Compatibility CLI

The compatibility CLI exposes the existing facial assessment and processing
commands. Run it from the source checkout:

```text
dotnet run --project src/OpenPhysical.Biometrics.Tool.Compatibility -- quality --input portrait.jpg --format json
dotnet run --project src/OpenPhysical.Biometrics.Tool.Compatibility -- quality --input portrait.jpg --format detailed
dotnet run --project src/OpenPhysical.Biometrics.Tool.Compatibility -- process portrait.jpg --quality-gate 0.8 --quality-report
```

The threshold in the final command is an application setting. Configure it for the
capture workflow and keep its version with the resulting decision. JSON output
supports machine-readable report handling; detailed output supports operator
review.
