// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class SceneLifetime
{
    internal static Maybe<FingerprintScene> Clear(Maybe<FingerprintScene> current)
    {
        current.Tap(scene => scene.Dispose());
        return Maybe<FingerprintScene>.None;
    }

    internal static Maybe<FingerprintScene> Replace(
        Maybe<FingerprintScene> current,
        FingerprintScene next
    )
    {
        Clear(current);
        return Maybe.From(next);
    }
}
