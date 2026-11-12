// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Source-bound role-separated geometry; it is not live sensor qualification.</summary>
public sealed class Fs50ContactContourArtifact
{
    private readonly Fs50ContactContourShape shape;

    internal Fs50ContactContourArtifact(
        string sourceDigest,
        string evidenceVersion,
        string evidencePolicyDigest,
        Fs50ContactContourShape shape
    )
    {
        this.shape = shape;
        SourceDigest = sourceDigest;
        EvidenceVersion = evidenceVersion;
        EvidencePolicyDigest = evidencePolicyDigest;
    }

    /// <summary>Gets SHA-256 of the exact original corrected acquisition.</summary>
    public string SourceDigest { get; }

    /// <summary>Gets the original contact-evidence implementation.</summary>
    public string EvidenceVersion { get; }

    /// <summary>Gets the original contact-evidence policy SHA-256.</summary>
    public string EvidencePolicyDigest { get; }

    /// <summary>Gets the independent contour algorithm identifier.</summary>
    public string Version => Fs50ContactContourPolicy.Version;

    /// <summary>Gets the validated image-context choices.</summary>
    public Fs50ContactContourPolicy Policy => shape.Policy;

    /// <summary>Gets the version-bound image-context policy SHA-256.</summary>
    public string PolicyDigest => Policy.Digest;

    /// <summary>Gets all immutable geometry planes.</summary>
    public Fs50ContactContourPlanes Planes => shape.Planes;

    /// <summary>Gets measured selected connected contact C.</summary>
    public ImmutableArray<byte> ContactMask => shape.ContactMask;

    /// <summary>Gets inferred enclosed holes H.</summary>
    public ImmutableArray<byte> FilledHoleMask => shape.FilledHoleMask;

    /// <summary>Gets feature admission F.</summary>
    public ImmutableArray<byte> FeatureMask => shape.FeatureMask;

    /// <summary>Gets measured support S intersected with selected contact.</summary>
    public ImmutableArray<byte> SupportMask => shape.SupportMask;

    /// <summary>Gets image context E, which does not authorize features.</summary>
    public ImmutableArray<byte> ContextMask => shape.ContextMask;

    /// <summary>Gets feature area without selected measured support.</summary>
    public ImmutableArray<byte> UncertainMask => shape.UncertainMask;

    /// <summary>Gets SHA-256 of C.</summary>
    public string ContactDigest => shape.ContactDigest;

    /// <summary>Gets SHA-256 of H.</summary>
    public string FilledHoleDigest => shape.FilledHoleDigest;

    /// <summary>Gets SHA-256 of F.</summary>
    public string FeatureDigest => shape.FeatureDigest;

    /// <summary>Gets SHA-256 of S.</summary>
    public string SupportDigest => shape.SupportDigest;

    /// <summary>Gets SHA-256 of E.</summary>
    public string ContextDigest => shape.ContextDigest;

    /// <summary>Gets SHA-256 of F minus S.</summary>
    public string UncertainDigest => shape.UncertainDigest;

    /// <summary>Gets geometry counts without assigning image or minutia quality.</summary>
    public Fs50ContactContourStatistics Statistics => shape.Statistics;

    /// <summary>Creates actual pixel and feature masks for the chosen pixel-modification mode.</summary>
    /// <param name="modifiedPixels">Whether pixels outside E will be modified.</param>
    /// <returns>Retained E, eligible F intersected with guarded E, and the actual cut guard.</returns>
    public Result<Fs50ContourProcessingRegion, CaptureError> ToProcessingRegion(
        bool modifiedPixels
    ) => shape.ToProcessingRegion(modifiedPixels);

    /// <summary>Changes context without re-analyzing pixels or authorizing new contact features.</summary>
    /// <param name="policy">New validated context choices.</param>
    /// <param name="cancellationToken">Cancellation prevents publication of partial evidence.</param>
    /// <returns>Updated context with unchanged original C, H, F, S and evidence identity.</returns>
    public Result<Fs50ContactContourArtifact, CaptureError> WithContext(
        Fs50ContactContourPolicy policy,
        CancellationToken cancellationToken
    ) =>
        shape
            .WithContext(policy, cancellationToken)
            .Map(next => new Fs50ContactContourArtifact(
                SourceDigest,
                EvidenceVersion,
                EvidencePolicyDigest,
                next
            ));

    /// <summary>Validates saved geometry without image analysis or constructing live capture authorization.</summary>
    /// <param name="sourceDigest">Original Gray8 SHA-256.</param>
    /// <param name="evidenceVersion">Original contact-evidence implementation.</param>
    /// <param name="evidencePolicyDigest">Original contact-evidence policy SHA-256.</param>
    /// <param name="policy">Saved validated image-context policy.</param>
    /// <param name="planes">Saved original role-separated geometry planes.</param>
    /// <param name="statistics">Saved geometry counts.</param>
    /// <param name="cancellationToken">Cancellation prevents publication of partial restored evidence.</param>
    /// <returns>Verified immutable geometry or a precise malformed-metadata failure.</returns>
    public static Result<Fs50ContactContourArtifact, CaptureError> Restore(
        string sourceDigest,
        string evidenceVersion,
        string evidencePolicyDigest,
        Fs50ContactContourPolicy policy,
        Fs50ContactContourPlanes planes,
        Fs50ContactContourStatistics statistics,
        CancellationToken cancellationToken
    ) =>
        Fs50ContourRestore.Create(
            sourceDigest,
            evidenceVersion,
            evidencePolicyDigest,
            policy,
            planes,
            statistics,
            cancellationToken
        );
}
