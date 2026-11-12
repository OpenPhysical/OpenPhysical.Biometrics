// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class EnrollmentTestSuggestions
{
    internal static EnrollmentFingerSuggestions IndexPair() => new(new IndexRandom());

    private sealed class IndexRandom : Random
    {
        public override int Next(int maxValue) => 1;
    }
}
