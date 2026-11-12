# Third-party notices

The original FaceONNx files do not remain in the current tree. The repository's
LICENSE explains the scope of the MIT-licensed historical FaceONNx revisions.
Current facial and imaging components retain their source attribution and
dependency license terms.

OpenPhysical.Biometrics.NBIS and the NFIQ packages carry their own source,
algorithm, model, and dependency notices. NuGet dependency metadata identifies
the exact selected versions. Distribution retains those package notices.

## Optional NFIQ 2 desktop assessment

OpenPhysical.Biometrics.NFIQ2 1.0.1 bundles NIST NFIQ 2.3.0, OpenCV,
FingerJetFX OSE, and digestpp. The package retains the NIST notices, Apache-2.0
OpenCV terms, LGPL-3.0-or-later FingerJet terms, and public-domain digestpp notice.
Matching source, including recursively pinned dependencies, is available at
[the package source revision](https://github.com/OpenPhysical/OpenPhysical.Biometrics.NFIQ/tree/17555f05ad9be6e0bd64487fb37d301b9c79b033).
The desktop publisher preserves the package's complete notices and rebuild
instructions under `Contents/Resources/Licenses`. FingerJet remains a separate
replaceable shared library; the application does not statically incorporate it.

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

## Avalonia desktop utility

The desktop application uses Avalonia Desktop and the Inter font package 11.3.6,
Material.Avalonia 3.14.2, CommunityToolkit.Mvvm 8.4.0, Tmds.DBus.Protocol 0.21.3, and
Microsoft.Extensions.Hosting 10.0.0. These NuGet packages declare MIT licenses;
their distributed dependency and font notices remain applicable.
Sources: [Avalonia](https://github.com/AvaloniaUI/Avalonia),
[Material.Avalonia](https://github.com/AvaloniaCommunity/Material.Avalonia),
[Community Toolkit](https://github.com/CommunityToolkit/dotnet),
[Tmds.DBus](https://github.com/tmds/Tmds.DBus), and
[.NET extensions](https://github.com/dotnet/runtime).

Inserted-card access uses PCSC 7.0.1 under BSD-2-Clause. Its exact published-source
notice is retained at `src/OpenPhysical.Biometrics.Desktop/Notices/PCSC-COPYING.txt`.
Source: [pcsc-sharp](https://github.com/danm-de/pcsc-sharp).

## BouncyCastle.Cryptography

SHA-256 digests (model integrity checks and input hashing) use
BouncyCastle.Cryptography 2.6.1, distributed under the MIT license (Legion of
the Bouncy Castle Inc.). Source and notices: https://github.com/bcgit/bc-csharp.
All cryptographic operations in this repository use BouncyCastle; the
`System.Security.Cryptography` namespace is banned at build time.


## FS50 USB runtime

The FS50 USB transport receives its native runtime through
`UsbDotNet.LibUsbNative` 1.4.2, with the managed `UsbDotNet.Core` dependency.
UsbDotNet is MIT licensed; its bundled libusb is LGPL-2.1 licensed. The existing
OpenPhysical transport retains interface ownership and bounded transfer behavior.
Distribution includes the package's `licenses/LICENSE` and
`licenses/LICENSE.libusb`. Source and notices:
https://github.com/tmittet/UsbDotNet.

## FS50 numerical oracle (development only)

The FS50 interpolation profile was characterized using `opencv-python-headless==5.0.0.93`
and NumPy 2.4.6 on synthetic pixels. OpenCV 5 is Apache-2.0 licensed; it is used only by
`tools/fs50/generate_resampler_oracle.py` in a separate development environment. Neither
OpenCV binaries nor its new cubic-kernel source are included in the managed capture
library. Numerical coefficient constants were recovered from observable synthetic
outputs and common cubic mathematics. No upstream cubic implementation was copied or
translated. See `docs/FS50-CAPTURE.md` and the immutable numerical oracle fixture for
profile and provenance details.
