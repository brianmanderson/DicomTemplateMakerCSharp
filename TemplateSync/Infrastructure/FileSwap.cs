using System;
using System.IO;
using System.Threading;

namespace TemplateSync.Infrastructure
{
    /// <summary>
    /// The last step of an atomic save: renames a fully written temporary file over the target in one step, so readers
    /// see the old or the new content and never a partly written file. On Windows the rename fails while another process
    /// briefly holds the target or the new file open without delete sharing (a virus scanner or the search indexer does
    /// this right after a write; so can a reader). The rename is therefore retried after each of
    /// <see cref="RetryDelays"/>; if it still fails, the target keeps its old content, the temporary file is removed and
    /// the error propagates.
    /// </summary>
    internal static class FileSwap
    {
        /// <summary>Waits before each retry; their sum (about 0.6 s) bounds how long a locked file is waited for.</summary>
        internal static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(320),
        };

        public static void MoveOver(string temp, string target)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temp, target, overwrite: true);
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < RetryDelays.Length)
                {
                    Thread.Sleep(RetryDelays[attempt]);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    TryDelete(temp);
                    throw;
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // The failed rename is the error worth reporting; a stray .tmp file is overwritten by the next save.
            }
        }
    }
}
