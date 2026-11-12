// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

/// <summary>Original acquisition-time choices, distinct from later processing edits.</summary>
/// <param name="Settings">Validated choices captured at acquisition.</param>
/// <param name="DelaySeconds">Original presentation delay in seconds.</param>
public sealed record CaptureAcquisitionPolicy(CapturePolicySettings Settings, double DelaySeconds)
{
    /// <summary>Identifies the one current acquisition snapshot shape.</summary>
    public const string Version = "studio-acquisition-policy-v1";

    /// <summary>Attaches the original acquisition decision without changing image or processing controls.</summary>
    /// <param name="capture">Prepared capture.</param>
    /// <returns>A capture with the original acquisition snapshot.</returns>
    public CapturedFingerprint ApplyTo(CapturedFingerprint capture) =>
        capture with
        {
            Processing = capture.Processing.Map(evidence =>
                evidence with
                {
                    OriginalAcquisitionPolicy = this,
                }
            ),
        };

    /// <summary>Serializes the complete current acquisition snapshot.</summary>
    /// <returns>Current canonical policy JSON.</returns>
    public JsonElement ToJson() =>
        JsonSerializer.SerializeToElement(
            new
            {
                version = Version,
                autoCaptureDelaySeconds = DelaySeconds,
                capturePolicy = CapturePolicyJson.Write(Settings),
            }
        );

    /// <summary>Reads only the one current acquisition snapshot shape.</summary>
    /// <param name="value">Current serialized snapshot.</param>
    /// <returns>A snapshot or precise format/settings failure.</returns>
    public static Result<CaptureAcquisitionPolicy, DesktopError> Read(JsonElement value) =>
        DesktopEdge
            .Try(
                () => Parse(value),
                "Settings.AcquisitionPolicy",
                "The acquisition policy is invalid."
            )
            .Bind(result => result);

    private static Result<CaptureAcquisitionPolicy, DesktopError> Parse(JsonElement value)
    {
        if (
            value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Count() != 3
            || value.GetProperty("version").GetString() != Version
        )
            return new DesktopError(
                "Settings.AcquisitionPolicy",
                "The acquisition policy version is unsupported."
            );
        return DesktopPreferences
            .CheckDelay(value.GetProperty("autoCaptureDelaySeconds").GetDouble())
            .Bind(delay =>
                CapturePolicyJson
                    .Read(value)
                    .Map(policy => new CaptureAcquisitionPolicy(policy, delay))
            );
    }
}
