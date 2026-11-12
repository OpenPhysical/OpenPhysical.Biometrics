// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record TemplateManifestImage(
    int ViewIndex,
    byte Finger,
    string FileName,
    string Sha256,
    string PixelSha256,
    int Width,
    int Height,
    DateTimeOffset CapturedAtUtc,
    long FrameSequence,
    JsonElement Provenance,
    JsonElement Quality
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Nfiq2 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Processing { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ExtractorVersion { get; init; }
}
