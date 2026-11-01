# Third-party notices

The original FaceONNx files do not remain in the current tree. The repository's
LICENSE explains the scope of the MIT-licensed historical FaceONNx revisions.
Current facial and imaging components retain their source attribution and
dependency license terms.

OpenPhysical.Biometrics.NBIS and the NFIQ packages carry their own source,
algorithm, model, and dependency notices. NuGet dependency metadata identifies
the exact selected versions. Distribution retains those package notices.

## CoreJ2K.Skia and SkiaSharp

JPEG 2000 integration uses CoreJ2K.Skia 2.4.1.108 and CoreJ2K 2.4.1.108.
CoreJ2K retains its BSD-3-Clause license and the JJ2000 notices distributed with
the upstream package. Source and notices: https://github.com/cinderblocks/CoreJ2K.

Imaging uses SkiaSharp 3.119.4, distributed under MIT with the applicable native
Skia and dependency notices. Source and notices: https://github.com/mono/SkiaSharp.
Linux native assets use SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.4.
The dependency packages carry their native libraries and third-party notices.

## Retained facial models

The facial-model package includes Google's MediaPipe weights in the ONNX
conversion published by Yakhyokhuja Valikhujaev. The models and adapted
detector/crop postprocessing retain Apache-2.0 terms. See
`src/OpenPhysical.Biometrics.Onnx/Resources/provenance.json` and
`LICENSE-MediaPipe-Apache-2.0.txt` for exact artifacts and notices. The
first-party combination permission is part of the repository's `LICENSE`.

## BouncyCastle.Cryptography

SHA-256 digests (model integrity checks and input hashing) use
BouncyCastle.Cryptography 2.6.1, distributed under the MIT license (Legion of
the Bouncy Castle Inc.). Source and notices: https://github.com/bcgit/bc-csharp.
All cryptographic operations in this repository use BouncyCastle; the
`System.Security.Cryptography` namespace is banned at build time.
