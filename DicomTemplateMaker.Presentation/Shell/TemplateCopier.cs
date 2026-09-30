using System;
using System.IO;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>"Copy selected": copies a template folder to a new, unused name next to it.</summary>
    public static class TemplateCopier
    {
        /// <summary>{name}_Copy0, {name}_Copy1, ...: the first that does not exist in <paramref name="templateRoot"/>.</summary>
        public static string NextCopyName(string templateRoot, string templateName)
        {
            for (int number = 0; ; number++)
            {
                string candidate = $"{templateName}_Copy{number}";
                string path = Path.Combine(templateRoot, candidate);
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    return candidate;
                }
            }
        }

        /// <summary>The file that lists a template's monitored folders; a copy gets an empty one.</summary>
        public const string PathsFileName = "Paths.txt";

        /// <summary>
        /// Copies <paramref name="sourceFolder"/> into a new folder of <paramref name="templateRoot"/> and returns its
        /// name. The destination must not exist; nothing is overwritten. The copy gets an empty Paths.txt instead of the
        /// original's: a running RT generator would otherwise start writing the copy's RTs next to every series in the
        /// original's monitored folders at once, before the copy could be edited.
        /// </summary>
        public static string Copy(string sourceFolder, string templateRoot, string templateName)
        {
            string name = NextCopyName(templateRoot, templateName);
            string destination = Path.Combine(templateRoot, name);
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(sourceFolder, directory)));
            }

            string sourcePaths = Path.Combine(sourceFolder, PathsFileName);
            foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(sourcePaths), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destination, Path.GetRelativePath(sourceFolder, file)), overwrite: false);
            }

            using (new FileStream(Path.Combine(destination, PathsFileName), FileMode.CreateNew, FileAccess.Write))
            {
                // An empty Paths.txt: the copy is a template that monitors no folder.
            }

            return name;
        }
    }
}
