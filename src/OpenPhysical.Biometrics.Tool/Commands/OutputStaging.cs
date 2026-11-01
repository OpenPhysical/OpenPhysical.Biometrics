// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Writes every output next to its destination, then moves them into place together.
// A failure at any step removes installed files and restores displaced originals.
internal static class OutputStaging
{
    internal static UnitResult<ToolError> Write(
        ImmutableArray<(string Path, byte[] Bytes)> outputs,
        bool overwrite
    )
    {
        var committed = ToolEdge.Try(() => Commit(outputs, overwrite), ToolErrors.FaceFileAccess());
        return committed.IsSuccess
            ? UnitResult.Success<ToolError>()
            : UnitResult.Failure(committed.Error);
    }

    private static bool Commit(ImmutableArray<(string Path, byte[] Bytes)> outputs, bool overwrite)
    {
        var staged = new List<StagedFile>();
        var installed = new List<string>();
        var committed = false;
        try
        {
            foreach (var output in outputs)
            {
                staged.Add(Plan(output.Path));
                Fill(staged[^1].Stage, output.Bytes);
            }
            for (var i = 0; i < staged.Count; i++)
            {
                staged[i] = Displace(staged[i], overwrite);
                File.Move(staged[i].Stage, staged[i].Destination);
                installed.Add(staged[i].Destination);
            }
            committed = true;
            return committed;
        }
        finally
        {
            Cleanup(staged, installed, committed);
        }
    }

    private static StagedFile Plan(string path)
    {
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination) ?? string.Empty;
        return new StagedFile(
            destination,
            Path.Combine(directory, ".portrait-" + Guid.NewGuid().ToString("N")),
            Maybe<string>.None
        );
    }

    private static long Fill(string stage, byte[] bytes)
    {
        using var stream = new FileStream(
            stage,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None
        );
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(stage, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        stream.Write(bytes);
        stream.Flush(true);
        return stream.Length;
    }

    // Overwriting keeps the original beside its stage until the whole set is committed.
    private static StagedFile Displace(StagedFile item, bool overwrite)
    {
        if (!overwrite || !File.Exists(item.Destination))
            return item;
        var backup = item.Stage + ".backup";
        File.Move(item.Destination, backup);
        return item with { Backup = backup };
    }

    private static bool Cleanup(List<StagedFile> staged, List<string> installed, bool committed)
    {
        if (!committed)
        {
            foreach (var path in installed)
                File.Delete(path);
            foreach (var item in staged.Where(s => s.Backup.HasValue))
                File.Move(item.Backup.Value, item.Destination);
        }
        foreach (var item in staged)
        {
            File.Delete(item.Stage);
            if (committed && item.Backup.HasValue)
                File.Delete(item.Backup.Value);
        }
        return committed;
    }
}
