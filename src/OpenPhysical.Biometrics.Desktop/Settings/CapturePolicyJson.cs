// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class CapturePolicyJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static object Write(CapturePolicySettings policy) =>
        new
        {
            tileSize = policy.Enhancement.TileSize,
            clipLimit = policy.Enhancement.ClipLimit,
            blend = policy.Enhancement.Blend,
            evidence = JsonSerializer.SerializeToElement(policy.EvidenceOptions, Options),
            emptySensor = JsonSerializer.SerializeToElement(policy.EmptySensorOptions, Options),
            minimumNfiq1Gain = policy.MinimumNfiq1Gain,
            minimumNfiq2Gain = policy.MinimumNfiq2Gain,
            contextMargin = policy.ContextMargin,
            wholeContact = JsonSerializer.SerializeToElement(policy.WholeContactOptions, Options),
            presentation = CapturePresentationPolicyJson.Write(policy.Presentation),
        };

    internal static Result<CapturePolicySettings, DesktopError> Read(JsonElement root)
    {
        if (
            !root.TryGetProperty("capturePolicy", out var policy)
            || policy.ValueKind != JsonValueKind.Object
            || policy.EnumerateObject().Count() != 10
        )
            return Invalid();
        return ReadObject<Fs50EvidenceOptions>(policy, "evidence")
            .Bind(evidence =>
                ReadObject<Fs50EmptySensorOptions>(policy, "emptySensor")
                    .Bind(empty =>
                        EnhancementSettings
                            .Create(
                                policy.GetProperty("tileSize").GetInt32(),
                                policy.GetProperty("clipLimit").GetDouble(),
                                policy.GetProperty("blend").GetDouble()
                            )
                            .Bind(enhancement =>
                                CapturePolicySettings.Create(
                                    enhancement,
                                    evidence,
                                    empty,
                                    policy.GetProperty("minimumNfiq1Gain").GetInt32(),
                                    policy.GetProperty("minimumNfiq2Gain").GetInt32(),
                                    policy.GetProperty("contextMargin").GetInt32()
                                )
                            )
                    )
            )
            .Bind(settings =>
                CapturePresentationPolicyJson
                    .Read(policy.GetProperty("presentation"))
                    .Bind(settings.WithPresentation)
            )
            .Bind(settings =>
                ReadObject<Fs50WholeContactOptions>(policy, "wholeContact")
                    .Bind(settings.WithWholeContact)
            );
    }

    private static Result<T, DesktopError> ReadObject<T>(JsonElement root, string name)
        where T : class =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? Maybe.From(value.Deserialize<T>(Options)).ToResult(Invalid())
            : Invalid();

    private static DesktopError Invalid() =>
        new("Settings.Policy", "The saved capture policy is invalid.");
}
