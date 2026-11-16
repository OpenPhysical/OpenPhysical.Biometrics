# Facial models

The facial package uses the published MediaPipe BlazeFace and 478-point Face Landmarker ONNX networks from Yakhyokhuja Valikhujaev. Google supplies the original trained weights. Both models retain Apache-2.0 terms; see LICENSE-MediaPipe-Apache-2.0.txt and provenance.json for the pinned sources and checksums.

The landmark model accepts RGB float32 NCHW 256 by 256 inputs in [0,1]. The detector accepts 128 by 128 inputs in [-1,1]. Detector decoding, weighted suppression, roll normalization, and source-coordinate projection are implemented in OpenPhysical.Biometrics.Facial. Models are embedded for offline operation.

Reproduce reference vectors using the pinned upstream Python implementation, CPU ONNX Runtime 1.23.2, and the fixture inputs recorded in the portrait test manifest. Compare the same normalized tensors with Google's original TFLite network when updating a model.

The square Selfie Segmenter is Google's Apache-2.0 float16 model, version 1, converted to float32 standard ONNX operators. Its input is 256×256 RGB NHWC in [0,1], with a stretch resize. Its output is one person-confidence channel, with no additional sigmoid. The converted model uses the existing ONNX Runtime dependency, with no MediaPipe runtime or custom kernels.

`scripts/convert-selfie-segmenter.py` converts the hash-pinned official TFLite model. It maps the one 2×2 stride-2 `Convolution2DTransposeBias` to standard transpose/ConvTranspose operators and retains native opset-14 HardSwish. Converter node names can vary; compare probability outputs before replacing the committed hash-pinned asset. Conversion and independent reference tools have separate pinned requirements files because their protobuf dependencies differ.

`scripts/generate-selfie-reference.py` runs the original Google TFLite model through MediaPipe 0.10.21 on already-square derivatives of the existing public Bush and Glenn fixtures. The retained little-endian float32 masks verify model parity independently of our ONNX graph. On Apple M4 Max, maximum absolute probability differences were 0.000031472 and 0.000018120, respectively; 30 warmed single-thread CPU runs took approximately 4.5 ms p95. This verifies model inference on identical input tensors, not a claim that every framework's full-source resize is identical.

The mask is an estimate for person/background compositing. Hair and fine boundaries can be missed; lighting, motion, occlusion and multiple people affect results. It does not establish PIV conformance or anatomical head measurements. The original source remains separate from any edited portrait. Original model: https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_segmenter/float16/1/selfie_segmenter.tflite . Google model card: https://storage.googleapis.com/mediapipe-assets/Model%20Card%20MediaPipe%20Selfie%20Segmentation.pdf . Existing Apache-2.0 license and attribution apply.
