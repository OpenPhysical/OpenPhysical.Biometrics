// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;

internal sealed class FaceCameraSelection(ICameraProvider cameras)
{
    internal ImmutableArray<CameraSource> Sources { get; private set; } = [];
    internal CameraSource Source { get; private set; } = new(string.Empty, "Choose a camera", []);
    internal CameraMode Mode { get; private set; } = new(0, 0, 0, 1, string.Empty);
    private Maybe<CameraOpticalQualification> qualification;
    internal Maybe<CameraOpticalQualification> OpticalQualification =>
        qualification.Where(value => CameraOpticalQualification.Matches(value, Source.Id, Mode));
    internal bool Qualified => OpticalQualification.HasValue;

    internal async Task<UnitResult<DesktopError>> RefreshAsync()
    {
        var result = await cameras.EnumerateAsync(CancellationToken.None);
        if (result.IsFailure)
            return new DesktopError(result.Error.Code, result.Error.Message);
        Sources = result.Value;
        var selected = Sources.FirstOrDefault(value => value.Id == Source.Id);
        var found = Maybe.From(selected).Or(Sources.Select(Maybe.From).FirstOrDefault());
        if (found.HasValue)
            Select(found.Value);
        else
        {
            Source = new(string.Empty, "No camera available", []);
            Mode = new(0, 0, 0, 1, string.Empty);
        }
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Select(CameraSource source)
    {
        if (!Sources.Contains(source))
            return new DesktopError("Face.Camera", "Choose a connected camera.");
        Source = source;
        Mode = source
            .Modes.Where(value => value == Mode)
            .Select(Maybe.From)
            .FirstOrDefault()
            .GetValueOrDefault(() =>
                source.PreferredMode.GetValueOrDefault(new CameraMode(0, 0, 0, 1, string.Empty))
            );
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Select(CameraMode mode)
    {
        if (!Source.Modes.Contains(mode))
            return new DesktopError(
                "Face.CameraMode",
                "Choose a native mode listed by the camera."
            );
        Mode = mode;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Qualify(string id, CameraMode mode, bool confirmed)
    {
        if (id != Source.Id || mode != Mode)
            return new DesktopError(
                "Face.Qualification",
                "Camera setup findings must match the selected device and mode."
            );
        if (!confirmed || OpticalQualification.HasNoValue)
            qualification = default;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Qualify(CameraOpticalQualification value)
    {
        if (!CameraOpticalQualification.Matches(value, Source.Id, Mode))
            return new DesktopError(
                "Face.Qualification",
                "The optical measurement must be valid and match this camera and native mode."
            );
        qualification = value;
        return UnitResult.Success<DesktopError>();
    }
}
