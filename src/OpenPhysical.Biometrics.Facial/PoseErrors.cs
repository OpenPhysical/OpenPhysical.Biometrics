// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

internal static class PoseErrors
{
    internal static FacialError Unassessable() =>
        new(
            FacialErrorCodes.PoseUnassessable,
            "The source face geometry cannot support a pose estimate."
        );
}
