// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Frame-associated readiness without comparing incompatible exposures.</summary>
public sealed record Fs50EmptySensorObservation
{
    internal Fs50EmptySensorObservation(
        bool ready,
        bool trusted,
        int count,
        Maybe<Fs50EmptySensorReference> reference,
        Maybe<double> difference,
        bool confidentlyEmpty
    )
    {
        Ready = ready;
        PresentationTrusted = trusted;
        ReferenceCount = count;
        Reference = reference;
        Difference = difference;
        ConfidentlyEmpty = confidentlyEmpty;
    }

    /// <summary>Gets whether a matching reference or this already-qualified presentation permits capture.</summary>
    public bool Ready { get; }

    /// <summary>Gets whether photometric comparison has an exactly matching exposure reference.</summary>
    public bool MatchingReference => Reference.HasValue;

    /// <summary>Gets whether this presentation began against a matching trusted reference.</summary>
    public bool PresentationTrusted { get; }

    /// <summary>Gets whether this bright, candidate-free frame can advance empty learning or removal.</summary>
    public bool ConfidentlyEmpty { get; }

    /// <summary>Gets the current bounded reference count.</summary>
    public int ReferenceCount { get; }

    /// <summary>Gets only the compatible reference, never an exposure-mismatched one.</summary>
    public Maybe<Fs50EmptySensorReference> Reference { get; }

    /// <summary>Gets absolute photometric difference only when a compatible reference exists.</summary>
    public Maybe<double> Difference { get; }
}
