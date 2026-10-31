# Third-party notices

The original MIT notice is retained in LICENSE-MIT-ORIGINAL. Existing facial and
imaging components retain their source attribution and dependency license terms.

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

The optional facial-model package retains the existing RetinaFace and PFLD assets.
Its `models/provenance.json` records exact hashes and the pre-consolidation source
commit, while `models/README.md` retains the historical model-source context.
`models/LICENSE-FaceONNX-MIT.txt` preserves the model archive publisher MIT notice
(copyright 2020–2025 Valery Asiryan), pinned by revision and SHA-256 in the
provenance file. `LICENSE-MIT-ORIGINAL` separately preserves the FaceOFFx notice.
