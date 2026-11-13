# OFIQ integration assessment

Read-only assessment, 2026-11-12. OFIQ is not integrated or enabled in Studio.

[BSI OFIQ](https://github.com/BSI-OFIQ/OFIQ-Project) is the C++ reference
implementation for ISO/IEC 29794-5 quality assessment, rather than an image-record
codec. Upstream [build instructions](https://github.com/BSI-OFIQ/OFIQ-Project/blob/main/BUILD.md)
document native Apple Silicon support, tested on macOS Sonoma 14.4.1.

Integration needs a bounded C ABI, reused engines, captured-image processing and
native bundling. Upstream links OpenCV and ONNX Runtime 1.18.1; Studio currently
uses ONNX Runtime 1.20.1, so runtime alignment and conformance require validation.
Build-time Python/Conan are not intended application-runtime dependencies.

The source [license](https://github.com/BSI-OFIQ/OFIQ-Project/blob/main/LICENSE.md)
is MIT, with separate model and image notices. Bounded HTTP range inspection of
the [official model archive](https://standards.iso.org/iso-iec/29794/-5/ed-1/en/OFIQ-MODELS.zip)
read its directory and ten model notices, transferring 12,182 bytes without
downloading weights or facial images. The archive is 422,400,019 bytes compressed
and approximately 453.5 MB expanded. Model notices identify MIT terms; HSEmotion
and MagFace also reference Apache 2.0. Any distribution must retain the exact
applicable notices and pin the selected source, model and configuration assets.

No local OFIQ build, runtime benchmark, model inference or conformance run was
performed. Its components would provide a stronger basis for automated quality
decisions than ad hoc image-statistic cutoffs, but acceptance thresholds would
still need an explicit, validated application policy. OFIQ scores do not turn
vendor example percentages into normative PIV requirements.
