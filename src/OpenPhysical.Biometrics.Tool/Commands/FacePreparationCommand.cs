// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;

internal static class FacePreparationCommand
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static int Run(string[] args)
    {
        if (args is ["--help"] || args.Length == 0)
        {
            Console.WriteLine(
                "biotool face prepare <image> --output <portrait.jp2> [--preview <portrait.png>] [--face-index N] [--geometry <review.json>] [--record <record.bin> --acquisition <capture.json>] [--json] [--include-sensitive] [--overwrite]"
            );
            return 0;
        }
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] is "--json" or "--overwrite" or "--include-sensitive")
                {
                    if (!flags.Add(args[i]))
                        throw new ArgumentException("Duplicate flag.");
                }
                else if (
                    args[i]
                    is "--output"
                        or "--preview"
                        or "--face-index"
                        or "--geometry"
                        or "--record"
                        or "--acquisition"
                )
                {
                    if (
                        i + 1 >= args.Length
                        || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                        || !options.TryAdd(args[i], args[++i])
                    )
                        throw new ArgumentException("Invalid or duplicate option.");
                }
                else
                    throw new ArgumentException("Unknown option.");
            }
            if (
                !options.ContainsKey("--output")
                || options.ContainsKey("--record") != options.ContainsKey("--acquisition")
            )
                throw new ArgumentException(
                    "Supply an output; record and acquisition must be supplied together."
                );
            int? index = options.TryGetValue("--face-index", out var indexText)
                ? int.Parse(indexText, CultureInfo.InvariantCulture)
                : null;
            var inputPath = Path.GetFullPath(args[0]);
            var destinations = new[] { "--output", "--preview", "--record" }
                .Where(options.ContainsKey)
                .Select(k => Path.GetFullPath(options[k]))
                .ToArray();
            var comparer = OperatingSystem.IsLinux()
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
            if (
                destinations.Distinct(comparer).Count() != destinations.Length
                || destinations.Contains(inputPath, comparer)
                || new[] { "--geometry", "--acquisition" }
                    .Where(options.ContainsKey)
                    .Select(k => Path.GetFullPath(options[k]))
                    .Any(p => destinations.Contains(p, comparer))
            )
                throw new ArgumentException(
                    "Output paths must be distinct from each other and all inputs."
                );
            if (!flags.Contains("--overwrite") && destinations.Any(File.Exists))
                throw new IOException("Output exists.");
            if (new FileInfo(inputPath).Length > 64 * 1024 * 1024)
                throw new ArgumentException("Input image exceeds 64 MiB.");
            var geometry = Read<ReviewedPortraitGeometry>(options, "--geometry");
            using var service = new PortraitPreparer();
            var result = service.Prepare(File.ReadAllBytes(inputPath), geometry, index);
            if (result.Status != PortraitStatus.Prepared)
            {
                Report(result, flags);
                return 2;
            }
            var pixels = result.RgbPixels.AsSpan();
            var outputs = new List<(string Path, byte[] Bytes)>();
            byte[] jp2;
            if (options.ContainsKey("--record"))
            {
                if (!result.Measurements!.ReviewedHeadBoundary)
                {
                    Report(
                        result with
                        {
                            Status = PortraitStatus.ReviewRequired,
                            Findings =
                            [
                                new(
                                    "ReviewHeadBoundary",
                                    "Confirm crown and both ear attachments before preparing an issuance record."
                                ),
                            ],
                        },
                        flags
                    );
                    return 2;
                }
                var acquisition = Read<FacialAcquisitionMetadata>(options, "--acquisition")!;
                var prepared = new FacialRecordPreparer().Prepare(pixels, 480, 640, acquisition);
                if (prepared.Status != FacialPreparationStatus.Prepared)
                {
                    Report(
                        result with
                        {
                            Status = PortraitStatus.ReviewRequired,
                            Findings =
                            [
                                new(
                                    prepared.Status.ToString(),
                                    "Check acquisition evidence and encoded profile findings."
                                ),
                            ],
                        },
                        flags
                    );
                    return 2;
                }
                var encodedRecord = Incits385Codec.Write(prepared.Record!);
                if (encodedRecord.IsFailure)
                    return ReportFailure("InvalidPortraitInput", flags);
                jp2 = prepared.Record!.Images[0].ImageData.ToArray();
                outputs.Add((options["--record"], encodedRecord.Value));
            }
            else
                jp2 = PortraitEncoding.EncodeJpeg2000(pixels, 480, 640);
            outputs.Add((options["--output"], jp2));
            if (options.TryGetValue("--preview", out var preview))
                outputs.Add((preview, PortraitEncoding.EncodePng(pixels, 480, 640)));
            WriteOutputs(outputs, flags.Contains("--overwrite"));
            Report(result, flags);
            return 0;
        }
        catch (Exception error)
            when (error
                    is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or NotSupportedException
                        or JsonException
                        or FormatException
                        or OverflowException
                        or Microsoft.ML.OnnxRuntime.OnnxRuntimeException
                        or DllNotFoundException
                        or EntryPointNotFoundException
                        or BadImageFormatException
                        or TypeInitializationException
            )
        {
            var code = error switch
            {
                DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or TypeInitializationException => "NativeRuntimeUnavailable",
                Microsoft.ML.OnnxRuntime.OnnxRuntimeException => "ModelExecutionFailed",
                JsonException => "InvalidReviewMetadata",
                ArgumentException or FormatException or OverflowException => "InvalidPortraitInput",
                IOException or UnauthorizedAccessException => "PortraitFileAccessFailed",
                _ => "PortraitPreparationFailed",
            };
            return ReportFailure(code, flags);
        }
    }

    private static int ReportFailure(string code, HashSet<string> flags)
    {
        if (flags.Contains("--json"))
            Console.WriteLine(
                JsonSerializer.Serialize(new { Status = "Failed", Code = code }, Json)
            );
        else
            Console.Error.WriteLine(
                code == "NativeRuntimeUnavailable"
                    ? "NativeRuntimeUnavailable: install the ONNX Runtime and Skia native assets for this operating system and architecture."
                    : $"{code}: check the input, reviewed geometry, model runtime, and output paths."
            );
        return 1;
    }

    private static T? Read<T>(Dictionary<string, string> options, string key)
        where T : class
    {
        if (!options.TryGetValue(key, out var path))
            return null;
        if (new FileInfo(path).Length > 64 * 1024)
            throw new ArgumentException("Metadata exceeds 64 KiB.");
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)
            ?? throw new ArgumentException("Metadata is empty.");
    }

    private static void Report(PortraitPreparationResult result, HashSet<string> flags)
    {
        object summary = flags.Contains("--include-sensitive")
            ? new
            {
                result.Status,
                ModelRevision = PortraitProvenance.ModelRevision,
                GeometryPolicy = PortraitProvenance.GeometryPolicy,
                result.SourceSha256,
                result.SourceWidth,
                result.SourceHeight,
                result.Faces,
                result.Transform,
                result.Measurements,
                result.Findings,
            }
            : new
            {
                result.Status,
                FaceCount = result.Faces.Length,
                Width = 480,
                Height = 640,
                Findings = result.Findings.Select(f => f.Code).ToArray(),
            };
        if (flags.Contains("--json"))
            Console.WriteLine(JsonSerializer.Serialize(summary, Json));
        else
        {
            Console.WriteLine($"{result.Status}: {result.Faces.Length} face(s).");
            foreach (var finding in result.Findings)
                Console.WriteLine($"{finding.Code}: {finding.Message}");
            if (flags.Contains("--include-sensitive"))
                Console.WriteLine(JsonSerializer.Serialize(summary, Json));
        }
    }

    private static void WriteOutputs(List<(string Path, byte[] Bytes)> outputs, bool overwrite)
    {
        var staged = new List<(string Destination, string Stage, string? Backup)>();
        var installed = new List<string>();
        var committed = false;
        try
        {
            foreach (var output in outputs)
            {
                var destination = Path.GetFullPath(output.Path);
                var stage = Path.Combine(
                    Path.GetDirectoryName(destination)!,
                    ".portrait-" + Guid.NewGuid().ToString("N")
                );
                staged.Add((destination, stage, null));
                using var stream = new FileStream(
                    stage,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(stage, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                stream.Write(output.Bytes);
                stream.Flush(true);
            }
            for (var i = 0; i < staged.Count; i++)
            {
                var item = staged[i];
                if (overwrite && File.Exists(item.Destination))
                {
                    var backup = item.Stage + ".backup";
                    File.Move(item.Destination, backup);
                    staged[i] = item with { Backup = backup };
                }
                File.Move(item.Stage, item.Destination);
                installed.Add(item.Destination);
            }
            committed = true;
        }
        catch
        {
            foreach (var path in installed)
                File.Delete(path);
            foreach (var item in staged.Where(s => s.Backup is not null))
                File.Move(item.Backup!, item.Destination);
            throw;
        }
        finally
        {
            foreach (var item in staged)
            {
                File.Delete(item.Stage);
                if (committed && item.Backup is not null)
                    File.Delete(item.Backup);
            }
        }
    }
}
