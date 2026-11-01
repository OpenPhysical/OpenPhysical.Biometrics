# Facial models

The facial package uses the published MediaPipe BlazeFace and 478-point Face Landmarker ONNX networks from Yakhyokhuja Valikhujaev. Google supplies the original trained weights. Both models retain Apache-2.0 terms; see LICENSE-MediaPipe-Apache-2.0.txt and provenance.json for the pinned sources and checksums.

The landmark model accepts RGB float32 NCHW 256 by 256 inputs in [0,1]. The detector accepts 128 by 128 inputs in [-1,1]. Detector decoding, weighted suppression, roll normalization, and source-coordinate projection are implemented in OpenPhysical.Biometrics.Facial. Models are embedded for offline operation.

Reproduce reference vectors using the pinned upstream Python implementation, CPU ONNX Runtime 1.23.2, and the fixture inputs recorded in the portrait test manifest. Compare the same normalized tensors with Google's original TFLite network when updating a model.
