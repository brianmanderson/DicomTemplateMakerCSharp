using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ROIOntologyClass
{
    /// <summary>
    /// Reads template and library files the way File.ReadAllText and File.ReadAllLines do (UTF-8, byte order marks
    /// honoured), but lets other programs write, rename and delete the file meanwhile. The RT generator reads every
    /// template every few seconds; with File.ReadAllText's FileShare.Read, a save that replaces the file (see
    /// <see cref="AtomicFile"/>) at that moment fails on Windows with "access denied".
    /// </summary>
    internal static class SharedFile
    {
        private const FileShare LetOthersReplace = FileShare.ReadWrite | FileShare.Delete;

        internal static string ReadAllText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, LetOthersReplace))
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                return reader.ReadToEnd();
            }
        }

        internal static string[] ReadAllLines(string path)
        {
            var lines = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, LetOthersReplace))
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }
    }
}
