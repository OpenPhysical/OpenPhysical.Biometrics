# Captured mouth-closure policy

The captured-image mouth check reuses the existing MediaPipe landmarks. It runs no additional model and changes no pixels. A failure keeps the source-supported portrait available for inspection and requires a retake or an explicit, recorded operator override.

## Measurement and tolerance

Google's [published lip topology](https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/python/solutions/face_mesh_connections.py) identifies inner mouth corners 78 and 308. The policy measures the gaps at landmark pairs 13/14, 82/87 and 312/317 perpendicular to the corner-to-corner line, divides each gap by inner mouth width, and uses the largest ratio. Translation, uniform scale and image-plane rotation do not change the ratio.

`inner-lip-opening-v1` defaults to a maximum ratio of **0.04**. This is a configurable application tolerance. PIV requires a closed mouth but does not supply this landmark formula or numeric threshold. The tolerance allows small landmark separation when lips appear closed; it is not a calibrated probability or a measure of expression neutrality.

Invalid, nonfinite or collapsed mouth geometry produces `MouthUnassessable`. A measured opening beyond the tolerance produces `MouthOpen`. Both are named “Mouth closed” checks. An override still requires a valid portrait and the application's separate attended-review and provenance requirements.

The preparation result retains the exact policy and source-bound measurement for saving. Policy changes reuse cached face analysis. Export must preserve the tolerance actually used instead of substituting the current preference.

## Public regression evidence

Measurements from the pinned production model, before enabling the check:

| Public portrait | Largest inner gap / mouth width |
| --- | ---: |
| Official 2003 Bush portrait, visible teeth | 0.120270 |
| NASA Glenn portrait, visible teeth | 0.133936 |
| Official 2017 Trump portrait, visible teeth | 0.134755 |
| Official January 2025 Trump portrait, closed lips | 0.002589 |

These examples are regression evidence, not population validation. The closed-mouth fixture is not asserted to satisfy pose, lighting or other PIV requirements. Keep false detections reviewable and record overrides rather than silently widening the policy for a particular person.

Behavior tests cover the cutoff, asymmetric opening, translation/rotation/scale, invalid configuration, missing or unusable landmarks, real cached-model examples, and preservation of the cropped inspection image and its policy evidence.
