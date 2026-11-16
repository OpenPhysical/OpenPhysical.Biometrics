// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

[SupportedOSPlatform("windows")]
internal static class WindowsPrivateFiles
{
    internal static DirectoryInfo CreateDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            if (directory.Parent is DirectoryInfo parent && !parent.Exists)
                CreateDirectory(parent.FullName);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            using var identity = WindowsIdentity.GetCurrent();
            var owner = identity.User!;
            security.SetOwner(owner);
            security.AddAccessRule(
                new FileSystemAccessRule(
                    owner,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow
                )
            );
            directory.Create(security);
        }
        return directory;
    }

    internal static FileStream CreateFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        security.SetOwner(owner);
        security.AddAccessRule(
            new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow)
        );
        return new FileInfo(path).Create(
            FileMode.CreateNew,
            FileSystemRights.FullControl,
            FileShare.None,
            4096,
            FileOptions.Asynchronous,
            security
        );
    }
}
