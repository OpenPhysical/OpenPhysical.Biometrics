// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Reflection;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

/// <summary>Checks the optimized bounded rounding against the runtime's independent binary16 conversion.</summary>
[TestFixture]
public sealed class Fs50HalfRoundingTests
{
    /// <summary>Every binary16 interval through 1024 preserves subnormal, tie and adjacent rounding.</summary>
    [Test]
    public void BoundedRoundingMatchesHalfAtEveryPositiveAndNegativeTransition()
    {
        var round = Maybe
            .From(
                typeof(Fs50Resampler).GetMethod(
                    "RoundToHalf",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
            )
            .Value.CreateDelegate<Func<double, double>>();
        var candidates = new List<double>();
        for (ushort bits = 0; bits < 0x6400; bits++)
        {
            double lower = (double)BitConverter.UInt16BitsToHalf(bits);
            double upper = (double)BitConverter.UInt16BitsToHalf((ushort)(bits + 1));
            double midpoint = (lower + upper) / 2;
            foreach (
                var value in new[]
                {
                    lower,
                    midpoint,
                    Math.BitIncrement(midpoint),
                    Math.BitDecrement(midpoint),
                }
            )
            {
                candidates.Add(value);
                candidates.Add(-value);
            }
        }
        candidates.Select(round).Should().Equal(candidates.Select(value => (double)(Half)value));
    }
}
