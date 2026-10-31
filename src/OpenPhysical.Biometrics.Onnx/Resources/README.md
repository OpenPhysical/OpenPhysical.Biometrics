# Facial analysis models

The optional facial pipeline embeds two models retained from the existing
OpenPhysical/FaceOFFx codebase:

- `FaceDetector.onnx` locates faces with RetinaFace.
- `landmarks_68_pfld.onnx` locates 68 facial landmarks with PFLD.

`provenance.json` records the exact retained bytes, SHA-256 values, and original
repository commit. Git LFS stores the model files. The build verifies those hashes
before testing or packaging the optional facial pipeline.

The historical model notes identify the FaceONNX model-archive distribution as an
input. Its [source repository](https://github.com/FaceONNX/FaceONNX.Models) records
MIT terms. `LICENSE-FaceONNX-MIT.txt` retains the publisher notice at the revision
recorded in `provenance.json`. `LICENSE-MIT-ORIGINAL` separately preserves the
FaceOFFx project notice. The package includes both notices.

Applications select the optional facial packages when they need model inference.
The facial record preparer also accepts attended sRGB captures and acquisition
metadata directly through its standard .NET API.
