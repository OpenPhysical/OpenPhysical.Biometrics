// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using CSharpFunctionalExtensions;

// Confirms that outputs are distinct from each other and from every input before any file is touched.
internal static class FacePaths
{
    private const long InputLimit = 64 * 1024 * 1024;

    internal static Result<FaceArguments, ToolError> Validate(FaceArguments arguments) =>
        ToolEdge
            .Try(() => Distinct(arguments), ToolErrors.FaceInput())
            .Ensure(distinct => distinct, ToolErrors.FaceInput())
            .Bind(_ => ToolEdge.Try(() => Exists(arguments), ToolErrors.FaceFileAccess()))
            .Ensure(exists => !exists, ToolErrors.FaceFileAccess())
            .Bind(_ => InputSize(arguments))
            .Map(_ => arguments);

    private static IEnumerable<string> Outputs(FaceArguments arguments) =>
        new[] { Maybe.From(arguments.Output), arguments.Preview, arguments.Record }
            .Where(path => path.HasValue)
            .Select(path => Path.GetFullPath(path.Value));

    private static bool Distinct(FaceArguments arguments)
    {
        var comparer = OperatingSystem.IsLinux()
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        var destinations = Outputs(arguments).ToArray();
        var inputPath = Path.GetFullPath(arguments.Input);
        return destinations.Distinct(comparer).Count() == destinations.Length
            && !destinations.Contains(inputPath, comparer)
            && !new[] { arguments.Geometry, arguments.Acquisition }
                .Where(path => path.HasValue)
                .Select(path => Path.GetFullPath(path.Value))
                .Any(path => destinations.Contains(path, comparer));
    }

    private static bool Exists(FaceArguments arguments) =>
        !arguments.Overwrite && Outputs(arguments).Any(File.Exists);

    private static Result<long, ToolError> InputSize(FaceArguments arguments) =>
        ToolEdge
            .Try(
                () => new FileInfo(Path.GetFullPath(arguments.Input)).Length,
                ToolErrors.FaceFileAccess()
            )
            .Ensure(length => length <= InputLimit, ToolErrors.FaceInput());
}
