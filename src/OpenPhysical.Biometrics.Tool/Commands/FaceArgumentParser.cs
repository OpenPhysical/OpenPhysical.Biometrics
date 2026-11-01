// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using CSharpFunctionalExtensions;

// Reads the option and flag tokens that follow the image path.
internal static class FaceArgumentParser
{
    private static readonly string[] Flags = ["--json", "--overwrite", "--include-sensitive"];
    private static readonly string[] Valued =
    [
        "--output",
        "--preview",
        "--face-index",
        "--geometry",
        "--record",
        "--acquisition",
    ];

    internal static Result<FaceArguments, ToolError> Parse(string[] args) =>
        Tokenize(args)
            .Bind(tokens =>
                FaceIndex(tokens.Options).Bind(index => Build(args[0], tokens, index).Succeed())
            )
            .Ensure(
                arguments => arguments.Record.HasValue == arguments.Acquisition.HasValue,
                ToolErrors.FaceInput()
            );

    private static Result<
        (Dictionary<string, string> Options, HashSet<string> Flags),
        ToolError
    > Tokenize(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            if (Flags.Contains(args[i]) && flags.Add(args[i]))
                continue;
            var valid =
                Valued.Contains(args[i])
                && i + 1 < args.Length
                && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                && options.TryAdd(args[i], args[++i]);
            if (!valid)
                return ToolErrors.FaceInput().Fail<(Dictionary<string, string>, HashSet<string>)>();
        }
        return options.ContainsKey("--output")
            ? (options, flags).Succeed()
            : ToolErrors.FaceInput().Fail<(Dictionary<string, string>, HashSet<string>)>();
    }

    private static Result<Maybe<int>, ToolError> FaceIndex(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("--face-index", out var text))
            return Maybe<int>.None.Succeed();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? Maybe.From(index).Succeed()
            : ToolErrors.FaceInput().Fail<Maybe<int>>();
    }

    private static FaceArguments Build(
        string input,
        (Dictionary<string, string> Options, HashSet<string> Flags) tokens,
        Maybe<int> index
    )
    {
        Maybe<string> Option(string key) =>
            tokens.Options.TryGetValue(key, out var value) ? value : Maybe<string>.None;
        return new FaceArguments(
            input,
            tokens.Options["--output"],
            Option("--preview"),
            index,
            Option("--geometry"),
            Option("--record"),
            Option("--acquisition"),
            tokens.Flags.Contains("--json"),
            tokens.Flags.Contains("--include-sensitive"),
            tokens.Flags.Contains("--overwrite")
        );
    }
}
