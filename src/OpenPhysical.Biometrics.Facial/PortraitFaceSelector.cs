// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Chooses the detection to prepare, or explains why the caller must review first.
internal static class PortraitFaceSelector
{
    internal static Maybe<PortraitReview> NeedsReview(
        ImmutableArray<PortraitFace> faces,
        Maybe<int> faceIndex
    )
    {
        if (faces.IsEmpty)
            return Review(
                PortraitStatus.NoFace,
                "NoFace",
                "No face passed detection and presence checks."
            );
        return faceIndex.HasNoValue && faces.Length != 1
            ? Review(
                PortraitStatus.FaceSelectionRequired,
                "SelectFace",
                "Select one face by its stable index."
            )
            : Maybe<PortraitReview>.None;
    }

    internal static Result<PortraitFace, FacialError> Select(
        ImmutableArray<PortraitFace> faces,
        int index
    ) =>
        index < 0 || index >= faces.Length
            ? FacialErrors.FaceIndexOutOfRange().Fail<PortraitFace>()
            : faces[index].Succeed();

    private static Maybe<PortraitReview> Review(
        PortraitStatus status,
        string code,
        string message
    ) => new PortraitReview(status, new PortraitFinding(code, message));
}
