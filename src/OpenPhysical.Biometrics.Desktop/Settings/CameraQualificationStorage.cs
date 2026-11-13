// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Persists camera measurements without images or biometric landmarks.</summary>
public sealed class CameraQualificationStorage
{
    private readonly string path;

    /// <summary>Creates a private configuration store at an explicit path.</summary>
    /// <param name="path">Configuration file outside repositories.</param>
    public CameraQualificationStorage(string path) => this.path = path;

    /// <summary>Gets the durable per-user optical qualification configuration.</summary>
    public static string DefaultPath =>
        Path.Combine(DesktopPaths.UserData, "Biometrics", "camera-qualification.json");

    /// <summary>Reads measured qualifications or an empty first-run configuration.</summary>
    /// <returns>Validated measurements or a typed read failure.</returns>
    public async Task<Result<ImmutableArray<CameraOpticalQualification>, DesktopError>> LoadAsync()
    {
        var loaded = await DesktopEdge.TryAsync(
            async () =>
            {
                if (!File.Exists(path))
                    return Result.Success<
                        ImmutableArray<CameraOpticalQualification>,
                        DesktopError
                    >([]);
                var bytes = await TemplateFileReader.ReadAsync(
                    path,
                    64 * 1024,
                    CancellationToken.None
                );
                if (bytes.IsFailure)
                    return bytes.Error;
                var values = JsonSerializer.Deserialize<ImmutableArray<CameraOpticalQualification>>(
                    bytes.Value
                );
                return !values.IsDefault && values.All(CameraOpticalQualification.Valid)
                    ? Result.Success<ImmutableArray<CameraOpticalQualification>, DesktopError>(
                        values
                    )
                    : new DesktopError(
                        "Camera.Qualification",
                        "The saved camera measurements are invalid."
                    );
            },
            "Camera.QualificationRead",
            "Cannot read camera setup."
        );
        return loaded.Bind(value => value);
    }

    /// <summary>Writes only validated scalar qualification evidence.</summary>
    /// <param name="values">Reviewed measurements.</param>
    /// <returns>Atomic private write outcome.</returns>
    public async Task<UnitResult<DesktopError>> SaveAsync(
        ImmutableArray<CameraOpticalQualification> values
    )
    {
        if (values.IsDefault || !values.All(CameraOpticalQualification.Valid))
            return new DesktopError(
                "Camera.Qualification",
                "Record a valid optical target measurement."
            );
        var bytes = JsonSerializer.SerializeToUtf8Bytes(values);
        var saved = await PrivateFileTransaction.WriteBatchAsync(
            [new PrivateFileWrite(path, [.. bytes])],
            true,
            CancellationToken.None
        );
        return saved.MapError(error => new DesktopError(error.Code, error.Message));
    }
}
