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

## Facial capture geometry

`PortraitPreparer` measures the eye line, nose bridge, mouth center, chin, and ear
attachments in orientation-corrected source pixels. Its Full Frontal crop uses one
uniform transform and returns head dimensions, eye placement, and typed findings.
Reviewed boundary coordinates remain distinct from automatic estimates.

Use `FacialAcquisitionMetadata` to retain the camera source, device, capture time,
quality value, and the operator's full-frontal and optical-resolution findings.
`FacialRecordPreparer` encodes the resulting sRGB portrait as JPEG 2000 and checks
the canonical facial record with `PivBiometricProfile`.

[Portrait preparation](PIV-PORTRAITS.md) shows the CLI, reviewed geometry, and
record-creation workflows. [Signature integration](SIGNATURE-INTEGRATION.md)
connects the prepared record to the application's signing and trust services.
