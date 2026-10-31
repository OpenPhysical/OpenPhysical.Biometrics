namespace OpenPhysical.Domains.Cbeff.Formats.Incits385;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// Metadata used when creating INCITS 385 facial image records.
/// </summary>
public sealed record Incits385Metadata(
    byte Quality,
    Gender Gender,
    EyeColor EyeColor,
    HairColor HairColor,
    Maybe<ImmutableArray<FeaturePoint>> FeaturePoints
)
{
    /// <summary>Gets default facial metadata with unspecified appearance fields.</summary>
    public static Incits385Metadata Default =>
        new(
            Quality: 0,
            Gender: Gender.Unspecified,
            EyeColor: EyeColor.Unspecified,
            HairColor: HairColor.Unspecified,
            FeaturePoints: Maybe<ImmutableArray<FeaturePoint>>.None
        );
}
