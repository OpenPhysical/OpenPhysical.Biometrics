// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using CSharpFunctionalExtensions;

// One output on its way to its destination: the staged copy and any displaced original.
internal sealed record StagedFile(string Destination, string Stage, Maybe<string> Backup);
