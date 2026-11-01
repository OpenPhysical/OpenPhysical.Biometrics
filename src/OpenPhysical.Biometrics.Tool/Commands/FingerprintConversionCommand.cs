// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

// Use the same codec and projection as library consumers so parsing rules and
// coordinate conversion have one implementation across every entry point.
internal static class FingerprintConversionCommand
{
    private const long InputLimit = 1_000_000;

    internal static Result<CommandOutcome, ToolError> Run(string[] args) =>
        ConversionArguments.Parse(args).Bind(Convert);

    private static Result<CommandOutcome, ToolError> Convert(ConversionArguments arguments) =>
        ToolEdge
            .Try(() => new FileInfo(arguments.Input).Length, ToolErrors.ConversionFailed())
            .Ensure(length => length <= InputLimit, ToolErrors.ConversionFailed())
            .Bind(_ =>
                ToolEdge.Try(
                    () => File.ReadAllBytes(arguments.Input),
                    ToolErrors.ConversionFailed()
                )
            )
            .Bind(bytes => SensitiveBytes.With(bytes, buffer => Project(buffer, arguments)))
            .Bind(json => Write(arguments.Output, json));

    private static Result<string, ToolError> Project(byte[] bytes, ConversionArguments arguments)
    {
        var parsed = Incits378Codec.Parse(bytes);
        if (parsed.IsFailure)
            return ToolErrors
                .InvalidFingerprintRecord(
                    parsed.Error.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} at byte {diagnostic.Offset}, length {diagnostic.Length}"
                    )
                )
                .Fail<string>();
        return parsed
            .Value.Record.ToMatchingTemplate((FingerPosition)arguments.Finger, arguments.View)
            .MapError(_ => ToolErrors.ConversionFailed())
            .Bind(template =>
                ToolEdge.Try(
                    () => FingerprintTemplateJson.Serialize(template),
                    ToolErrors.ConversionFailed()
                )
            );
    }

    // CreateNew protects an existing template if the output path is reused.
    private static Result<CommandOutcome, ToolError> Write(string output, string json) =>
        ToolEdge.Try(
            () =>
            {
                using var stream = new FileStream(
                    output,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );
                using var writer = new StreamWriter(stream);
                writer.Write(json);
                return CommandOutcome.Of(0);
            },
            ToolErrors.ConversionFailed()
        );
}
