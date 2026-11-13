// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

internal sealed class PrivateFileInstallation(PrivateFileWrite file, string stage, string backup)
{
    internal PrivateFileWrite File { get; } = file;
    internal string Stage { get; } = stage;
    internal string Backup { get; } = backup;
    private bool installed;
    private bool backedUp;

    internal Result<bool, PackageError> Install(bool overwrite)
    {
        var valid = PrivatePath.Validate(File.Path);
        if (valid.IsFailure)
            return valid.Error;
        if (System.IO.File.Exists(File.Path) && overwrite)
        {
            System.IO.File.Move(File.Path, Backup, false);
            backedUp = true;
        }
        System.IO.File.Move(Stage, File.Path, false);
        installed = true;
        return true;
    }

    internal bool Rollback()
    {
        if (installed)
            System.IO.File.Delete(File.Path);
        if (backedUp)
            System.IO.File.Move(Backup, File.Path, false);
        return true;
    }

    internal bool Cleanup(bool committed)
    {
        System.IO.File.Delete(Stage);
        if (committed)
            System.IO.File.Delete(Backup);
        return true;
    }
}
