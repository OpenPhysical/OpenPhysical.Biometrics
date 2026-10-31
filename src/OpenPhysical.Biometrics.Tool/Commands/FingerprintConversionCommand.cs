// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Globalization;
using System.Security.Cryptography;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

// Use the same codec and projection as library consumers so parsing rules and
// coordinate conversion have one implementation across every entry point.
internal static class FingerprintConversionCommand
{
    internal static int Run(string[] args)
    {
        byte[]? bytes = null;
        try
        {
            if (args.Length != 6)
                throw new ArgumentException("Supply input, output, finger, and view.");
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
                    throw new ArgumentException("Invalid or duplicate selection option.");
            }
            if (
                !options.TryGetValue("--finger", out var finger)
                || !options.TryGetValue("--view", out var view)
            )
                throw new ArgumentException("Select both finger and view.");
            if (new FileInfo(args[0]).Length > 1_000_000)
                throw new ArgumentException("The fingerprint record exceeds the input limit.");
            bytes = File.ReadAllBytes(args[0]);
            var parsed = Incits378Codec.Parse(bytes);
            if (!parsed.IsSuccess)
            {
                foreach (var diagnostic in parsed.Diagnostics)
                    Console.Error.WriteLine(
                        $"{diagnostic.Code} at byte {diagnostic.Offset}, length {diagnostic.Length}"
                    );
                return 1;
            }
            var template = parsed.Value!.ToMatchingTemplate((FingerPosition)finger, view);
            // CreateNew protects an existing template if the output path is reused.
            using var stream = new FileStream(
                args[1],
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            using var writer = new StreamWriter(stream);
            writer.Write(FingerprintTemplateJson.Serialize(template));
            return 0;
        }
        catch (Exception error)
            when (error
                    is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or NotSupportedException
            )
        {
            Console.Error.WriteLine(
                "Fingerprint conversion failed. Check the raw FMR record, selected finger/view, resolution, and output availability."
            );
            return 1;
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
