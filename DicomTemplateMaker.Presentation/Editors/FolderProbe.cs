using System;
using System.IO;
using System.Threading.Tasks;

namespace DicomTemplateMakerGUI.Editors
{
    public enum FolderState
    {
        Exists,
        Missing,

        /// <summary>The check did not finish in time (typically a network share that does not answer).</summary>
        Unreachable,
    }

    /// <summary>Checks whether folders exist without blocking the caller: a network path that does not answer can take a long time.</summary>
    public static class FolderProbe
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Runs <paramref name="exists"/> (Directory.Exists by default) on the thread pool and gives up after
        /// <paramref name="timeout"/>. A check that throws counts as missing.
        /// </summary>
        public static async Task<FolderState> CheckAsync(string path, TimeSpan timeout, Func<string, bool>? exists = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            Func<string, bool> check = exists ?? Directory.Exists;
            Task<bool> probe = Task.Run(() =>
            {
                try
                {
                    return check(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                {
                    return false;
                }
            });
            Task finished = await Task.WhenAny(probe, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished != probe)
            {
                // The probe keeps running in the background; its answer is ignored.
                return FolderState.Unreachable;
            }

            return await probe.ConfigureAwait(false) ? FolderState.Exists : FolderState.Missing;
        }

        /// <summary>The warning shown next to a monitored folder in <paramref name="state"/>, or null when it exists.</summary>
        public static string? Describe(FolderState state, TimeSpan timeout)
        {
            return state switch
            {
                FolderState.Missing => "This folder does not exist (or its drive is not connected). No RTs are written for it until it exists.",
                FolderState.Unreachable => $"This folder did not answer within {timeout.TotalSeconds:0} seconds (a network share may be down). No RTs are written for it while it cannot be reached.",
                _ => null,
            };
        }
    }
}
