// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class DisplayedSceneFactory
{
    internal static Result<FingerprintScene, DesktopError> Frozen(
        DisplayedFingerprintFrame frame
    ) =>
        Create(frame)
            .Map(value =>
                value with
                {
                    Framing = FingerprintSceneFraming.Full(
                        frame.Frame,
                        frame.Frame.Width,
                        frame.Frame.Height
                    ),
                }
            );

    internal static Result<FingerprintScene, DesktopError> Create(DisplayedFingerprintFrame live) =>
        live.Processing.Match(
            image => SceneFactory.Processed(image, []),
            () => SceneFactory.Frame(live.Frame, [])
        );
}
