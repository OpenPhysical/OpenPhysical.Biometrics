// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

// The synchronous CLI boundary shares the private batch transaction with Studio.
internal static class OutputStaging
{
    internal static UnitResult<ToolError> Write(
        ImmutableArray<(string Path, byte[] Bytes)> outputs,
        bool overwrite
    ) =>
        PrivateFileTransaction
            .WriteBatchAsync(
                [
                    .. outputs.Select(value => new PrivateFileWrite(
                        value.Path,
                        ImmutableArray.Create(value.Bytes)
                    )),
                ],
                overwrite
            )
            .GetAwaiter()
            .GetResult()
            .MapError(error => new ToolError(error.Code, error.Message));
}
