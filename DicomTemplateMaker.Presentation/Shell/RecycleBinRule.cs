using System;
using System.Collections.Generic;
using System.IO;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>A selected template folder that is not deleted, because it has no Recycle Bin.</summary>
    public sealed record NotRecyclable(string Name, string Folder, string Reason);

    /// <summary>
    /// Which folders can go to the Windows Recycle Bin. Only local fixed drives have one: on a network share, a mapped
    /// network drive or a removable drive, a "move to the Recycle Bin" deletes permanently. "Delete selected" leaves such
    /// folders alone and says so, instead of promising they can be restored.
    /// </summary>
    public static class RecycleBinRule
    {
        /// <summary>
        /// Why <paramref name="folder"/> cannot be recycled, or null when it can. Works on Windows paths on any OS (UNC
        /// paths and drive letters are recognised as text); <paramref name="driveTypeOf"/> gives the type of a drive root
        /// such as <c>C:\</c> (on Windows, <c>new DriveInfo(root).DriveType</c>).
        /// </summary>
        public static string? WhyNotRecyclable(string folder, Func<string, DriveType> driveTypeOf)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            ArgumentNullException.ThrowIfNull(driveTypeOf);
            string path = folder.Trim().Replace('/', '\\');
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"\\.\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                return "it is on a network share";
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                path = path.Substring(4);
            }

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return "it is on a network share";
            }

            string root = path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'
                ? char.ToUpperInvariant(path[0]) + @":\"
                : Path.GetPathRoot(folder) ?? string.Empty;
            if (root.Length == 0)
            {
                return "its drive could not be determined";
            }

            DriveType type;
            try
            {
                type = driveTypeOf(root);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                type = DriveType.Unknown;
            }

            return type switch
            {
                DriveType.Fixed => null,
                DriveType.Network => $"it is on a network drive ({root})",
                DriveType.Removable => $"it is on a removable drive ({root})",
                _ => $"its drive ({root}) has no Recycle Bin or could not be checked",
            };
        }

        /// <summary>Splits <paramref name="templates"/> into the folders that can be recycled and those that cannot, with why.</summary>
        public static (List<(string Name, string Folder)> Recyclable, List<NotRecyclable> NotRecyclable) Split(
            IEnumerable<(string Name, string Folder)> templates, Func<string, DriveType> driveTypeOf)
        {
            ArgumentNullException.ThrowIfNull(templates);
            var recyclable = new List<(string Name, string Folder)>();
            var refused = new List<NotRecyclable>();
            foreach ((string name, string folder) in templates)
            {
                string? reason = WhyNotRecyclable(folder, driveTypeOf);
                if (reason == null)
                {
                    recyclable.Add((name, folder));
                }
                else
                {
                    refused.Add(new NotRecyclable(name, folder, reason));
                }
            }

            return (recyclable, refused);
        }
    }
}
