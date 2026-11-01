// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using CSharpFunctionalExtensions;

internal sealed record ConversionArguments(string Input, string Output, byte Finger, byte View)
{
    internal static Result<ConversionArguments, ToolError> Parse(string[] args)
    {
        if (args.Length != 6)
            return ToolErrors.ConversionFailed().Fail<ConversionArguments>();
        var options = new Dictionary<string, byte>(StringComparer.Ordinal);
        for (var i = 2; i < args.Length; i += 2)
        {
            if (
                args[i] is not ("--finger" or "--view")
                || !byte.TryParse(
                    args[i + 1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value
                )
                || !options.TryAdd(args[i], value)
            )
                return ToolErrors.ConversionFailed().Fail<ConversionArguments>();
        }
        return
            options.TryGetValue("--finger", out var finger)
            && options.TryGetValue("--view", out var view)
            ? new ConversionArguments(args[0], args[1], finger, view).Succeed()
            : ToolErrors.ConversionFailed().Fail<ConversionArguments>();
    }
}
