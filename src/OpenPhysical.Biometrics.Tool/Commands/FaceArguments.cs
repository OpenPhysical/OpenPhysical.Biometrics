// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using CSharpFunctionalExtensions;

internal sealed record FaceArguments(
    string Input,
    string Output,
    Maybe<string> Preview,
    Maybe<int> FaceIndex,
    Maybe<string> Geometry,
    Maybe<string> Record,
    Maybe<string> Acquisition,
    bool Json,
    bool IncludeSensitive,
    bool Overwrite
)
{
    internal const string Usage =
        "biotool face prepare <image> --output <portrait.jp2> [--preview <portrait.png>] [--face-index N] [--geometry <review.json>] [--record <record.bin> --acquisition <capture.json>] [--json] [--include-sensitive] [--overwrite]";
}
