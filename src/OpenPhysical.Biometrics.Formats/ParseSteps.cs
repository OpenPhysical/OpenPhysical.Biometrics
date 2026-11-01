// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Repetition for count-prefixed and length-bounded structures. Each step receives
// the items produced so far, and the first failure ends the sequence.
internal static class ParseSteps
{
    internal static Result<ImmutableArray<T>, FormatError> Repeat<T>(
        int count,
        Func<IReadOnlyList<T>, Result<T, FormatError>> step
    ) => Until(items => items.Count >= count, step);

    internal static Result<ImmutableArray<T>, FormatError> Until<T>(
        Func<IReadOnlyList<T>, bool> done,
        Func<IReadOnlyList<T>, Result<T, FormatError>> step
    )
    {
        var items = new List<T>();
        while (!done(items))
        {
            var next = step(items);
            if (next.IsFailure)
                return Result.Failure<ImmutableArray<T>, FormatError>(next.Error);
            items.Add(next.Value);
        }
        return items.ToImmutableArray();
    }
}
