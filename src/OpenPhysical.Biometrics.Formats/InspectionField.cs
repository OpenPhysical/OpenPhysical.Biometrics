// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

/// <summary>Locates a binary field in its original input.</summary>
/// <param name="Name">Stable field path.</param>
/// <param name="Offset">Zero-based byte offset.</param>
/// <param name="Length">Field size in bytes.</param>
/// <param name="Value">Hexadecimal field contents when sensitive inspection is enabled; otherwise no value.</param>
public sealed record InspectionField(
    string Name,
    int Offset,
    int Length,
    [property: JsonConverter(typeof(OptionalTextJsonConverter))] Maybe<string> Value
);
