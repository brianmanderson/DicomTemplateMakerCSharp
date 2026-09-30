using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ROIOntologyClass
{
    /// <summary>
    /// Replaces a file so that readers (the RT runner re-reads templates every 3 s) see either the old or the
    /// new content, never a partly written file: the content goes to a temporary file in the same folder, is
    /// flushed to disk, and then replaces the target in one rename. When anything fails the target keeps its
    /// old content and the temporary file is removed.
    /// </summary>
    internal static class AtomicFile
    {
        // What File.WriteAllText and File.WriteAllLines use, so the bytes written do not change.
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>Writes <paramref name="contents"/> as File.WriteAllText does (UTF-8, no byte order mark).</summary>
        internal static void WriteAllText(string path, string contents)
        {
            Write(path, writer => writer.Write(contents));
        }

        /// <summary>Writes each line followed by a newline, as File.WriteAllLines does.</summary>
        internal static void WriteAllLines(string path, IEnumerable<string> lines)
        {
            Write(path, writer =>
            {
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
            });
        }

        /// <summary>
        /// Writes the content produced by <paramref name="writeContent"/> to <paramref name="path"/>. An exception
        /// from <paramref name="writeContent"/>, the flush or the final replace propagates; the target is then
        /// unchanged.
        /// </summary>
        internal static void Write(string path, Action<TextWriter> writeContent)
        {
            WriteCore(path, writeContent, overwrite: true);
        }

        /// <summary>
        /// Writes <paramref name="contents"/> (as <see cref="WriteAllText"/>) only when <paramref name="path"/> does not
        /// exist when the file is put in place; returns false, and leaves the existing file alone, when it does. Two
        /// writers racing to create the same file therefore never replace each other's content: the first one wins.
        /// </summary>
        internal static bool TryCreateText(string path, string contents)
        {
            return WriteCore(path, writer => writer.Write(contents), overwrite: false);
        }

        private static bool WriteCore(string path, Action<TextWriter> writeContent, bool overwrite)
        {
            string target = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(target) ?? throw new ArgumentException($"'{path}' is not a file path.", nameof(path));
            // Ends in .tmp so the *.txt scans of legacy ROI and ontology folders never pick it up.
            string temp = Path.Combine(folder, $".{Path.GetFileName(target)}.{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.tmp");
            bool placed = false;
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: -1, leaveOpen: true))
                    {
                        writeContent(writer);
                    }
                    stream.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temp, target, overwrite);
                }
                catch (IOException) when (!overwrite && File.Exists(target))
                {
                    // Another writer created the file first; its content stays.
                    return false;
                }

                placed = true;
                return true;
            }
            finally
            {
                if (!placed)
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
