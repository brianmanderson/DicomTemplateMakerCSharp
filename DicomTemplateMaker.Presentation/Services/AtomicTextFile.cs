using System;
using System.IO;
using System.Text;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Replaces a file in one rename, so a reader sees the old or the new content and never a partly written file.
    /// The content goes to a temporary file in the same folder first; on failure the target keeps its old content and
    /// the temporary file is removed.
    /// </summary>
    internal static class AtomicTextFile
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Writes <paramref name="contents"/> as UTF-8 without a byte order mark.</summary>
        public static void Write(string path, string contents)
        {
            WriteVia(path, temp =>
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(Utf8NoBom.GetBytes(contents));
                    stream.Flush(flushToDisk: true);
                }
            });
        }

        /// <summary>
        /// Lets <paramref name="writeTemp"/> write a temporary file next to <paramref name="path"/>, then moves it over
        /// <paramref name="path"/>. Exceptions from <paramref name="writeTemp"/> or the move propagate.
        /// </summary>
        public static void WriteVia(string path, Action<string> writeTemp)
        {
            string target = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(target) ?? throw new ArgumentException($"'{path}' is not a file path.", nameof(path));
            Directory.CreateDirectory(folder);
            string temp = Path.Combine(folder, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
            bool moved = false;
            try
            {
                writeTemp(temp);
                File.Move(temp, target, overwrite: true);
                moved = true;
            }
            finally
            {
                if (!moved)
                {
                    TryDelete(temp);
                }
            }
        }

        private static void TryDelete(string temp)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // The original failure is the one worth reporting; a stray .tmp file is harmless.
            }
        }
    }
}
