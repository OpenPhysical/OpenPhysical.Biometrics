// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Text.Json;

internal sealed record RidgeBoundaryObservation(
    string Name,
    string Requirement,
    bool Learned,
    bool Qualified,
    bool ExposureReady,
    string Decision,
    int CaptureCount,
    int SpatialCount,
    string SourceDigest,
    string EvidencePolicyDigest,
    string ContactDigest,
    string FeatureDigest,
    Fs50WholeContactMetrics ContactMetrics,
    Fs50WholeContactMetrics ObservedMetrics,
    string ComparisonKind,
    Fs50WholeContactRidgeEvidence RidgeEvidence,
    int DirectSelectedSupportArea,
    int SupportedContactArea,
    int ContactArea
)
{
    public JsonElement SelectedSupportFraction =>
        Ratio(DirectSelectedSupportArea, RidgeEvidence.ObservedArea);
    public JsonElement ContactSupportFraction => Ratio(SupportedContactArea, ContactArea);

    internal static RidgeBoundaryObservation From(
        RidgeBoundaryControl control,
        bool learned,
        (CaptureFrame Frame, Fs50ContactRegion Contact, Fs50WholeContactProof Proof) observed,
        string policyDigest
    )
    {
        var diagnostics = observed.Contact.Diagnostics;
        var measured = Counts(diagnostics);
        _ = Export(control.Name, diagnostics, observed.Frame);
        return new(
            control.Name,
            control.Requirement,
            learned,
            observed.Proof.Qualified,
            observed.Proof.ExposureReady,
            observed.Proof.Record.Decision.ToString(),
            observed.Contact.ComponentCount,
            diagnostics.SpatialComponentCount,
            observed.Frame.PixelDigest,
            policyDigest,
            observed.Proof.Record.Identity.ContactDigest,
            observed.Proof.Record.Identity.FeatureDigest,
            observed.Proof.Record.Metrics,
            observed.Proof.Record.ObservedMetrics,
            Comparison(observed.Proof.Record),
            observed.Proof.Record.RidgeEvidence,
            measured.Observed,
            measured.Contact,
            diagnostics.ContactMask.Count(value => value != 0)
        );
    }

    private static string Comparison(Fs50WholeContactRecord record) =>
        record.ReferenceDigest.HasValue ? "matched-reference"
        : record.QualifiedAnchorDigest.HasValue ? "fixed-anchor-geometry"
        : "not-evaluated";

    private static bool Export(string name, Fs50ContactDiagnostics value, CaptureFrame frame)
    {
        var folder = Environment.GetEnvironmentVariable("OPENPHYSICAL_RIDGE_BOUNDARY_PLANES");
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".gray8"), frame.Pixels.ToArray());
        File.WriteAllBytes(Path.Combine(folder, name + ".C.mask"), value.ContactMask.ToArray());
        File.WriteAllBytes(
            Path.Combine(folder, name + ".W.mask"),
            value.ObservedContactMask.ToArray()
        );
        File.WriteAllBytes(
            Path.Combine(folder, name + ".S.mask"),
            value.RidgeSupportMask.ToArray()
        );
        return true;
    }

    private static (int Observed, int Contact) Counts(Fs50ContactDiagnostics value)
    {
        int observed = 0,
            contact = 0;
        for (var i = 0; i < 600000; i++)
        {
            if (value.RidgeSupportMask[i] == 0)
                continue;
            observed += value.ObservedContactMask[i];
            contact += value.ContactMask[i];
        }
        return (observed, contact);
    }

    private static JsonElement Ratio(int numerator, int denominator)
    {
        using var document = JsonDocument.Parse(
            denominator > 0 ? JsonSerializer.Serialize(numerator / (double)denominator) : "null"
        );
        return document.RootElement.Clone();
    }

    public bool Matches =>
        Learned
        && (
            Requirement switch
            {
                "qualified" => Qualified,
                "rejected" => !Qualified && !ExposureReady,
                "exposure-only" => !Qualified && ExposureReady,
                _ => true,
            }
        );
}
