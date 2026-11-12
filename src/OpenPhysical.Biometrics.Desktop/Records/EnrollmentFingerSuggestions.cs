// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Suggests a distinct finger from each hand once per person session.</summary>
public sealed class EnrollmentFingerSuggestions
{
    private static readonly ImmutableArray<FingerPosition> Fingers =
    [
        .. Enum.GetValues<FingerPosition>(),
    ];
    private readonly Func<int, int> chooseIndex;

    /// <summary>Uses the application random source for new person suggestions.</summary>
    public EnrollmentFingerSuggestions()
        : this(Random.Shared) { }

    /// <summary>Uses an explicitly supplied random source.</summary>
    /// <param name="random">Random source for choosing the two fingers.</param>
    public EnrollmentFingerSuggestions(Random random) => chooseIndex = random.Next;

    /// <summary>Creates a fresh opposite-hand enrollment plan.</summary>
    /// <returns>An empty session with its two suggested fingers.</returns>
    public EnrollmentSession Next()
    {
        var first = chooseIndex(Fingers.Length);
        var handSize = Fingers.Length / 2;
        var second = chooseIndex(handSize) + (first < handSize ? handSize : 0);
        return EnrollmentSession.Create([Fingers[first], Fingers[second]]).Value;
    }
}
