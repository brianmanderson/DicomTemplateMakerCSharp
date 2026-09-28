using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.Editors;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Chooses the folder a file or folder dialog opens in, without blocking the UI thread: remembered folders are often
    /// on network shares, and Directory.Exists on a share that does not answer can take tens of seconds.
    /// </summary>
    public static class DialogStartFolder
    {
        /// <summary>How long each candidate may take to answer; a dialog only needs a start folder, not this one.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The folders to try, in order: <paramref name="preferred"/>, then <paramref name="remembered"/>. Blank values
        /// are left out, and so is any folder in <paramref name="unreachableRoot"/> (a share that was just found not to
        /// answer), which would only hang again.
        /// </summary>
        public static IReadOnlyList<string> Candidates(string? preferred, string? remembered, string? unreachableRoot = null)
        {
            var candidates = new List<string>();
            foreach (string? folder in new[] { preferred, remembered })
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    continue;
                }

                string trimmed = folder.Trim();
                if (unreachableRoot != null && IsSameOrInside(trimmed, unreachableRoot))
                {
                    continue;
                }

                if (!candidates.Any(c => IsSameOrInside(c, trimmed) && IsSameOrInside(trimmed, c)))
                {
                    candidates.Add(trimmed);
                }
            }

            return candidates;
        }

        /// <summary>
        /// The first of <paramref name="candidates"/> that exists, each checked on the thread pool and given up after
        /// <paramref name="timeout"/> (see <see cref="FolderProbe"/>); null when none does, so the dialog opens in its
        /// default folder.
        /// </summary>
        public static async Task<string?> ChooseAsync(IReadOnlyList<string> candidates, TimeSpan timeout, Func<string, bool>? exists = null)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            var unreachable = new List<string>();
            foreach (string candidate in candidates)
            {
                if (unreachable.Any(u => IsSameOrInside(candidate, ShareRoot(u))))
                {
                    continue;
                }

                FolderState state = await FolderProbe.CheckAsync(candidate, timeout, exists).ConfigureAwait(false);
                if (state == FolderState.Exists)
                {
                    return candidate;
                }

                if (state == FolderState.Unreachable)
                {
                    unreachable.Add(candidate);
                }
            }

            return null;
        }

        /// <summary>
        /// True when <paramref name="path"/> is <paramref name="root"/> or inside it. Compares Windows paths as text on any
        /// OS: '/' and '\' are the same, case is ignored, and trailing separators do not count.
        /// </summary>
        public static bool IsSameOrInside(string path, string root)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(root);
            string p = Normalize(path);
            string r = Normalize(root);
            if (r.Length == 0)
            {
                return false;
            }

            return string.Equals(p, r, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(r.EndsWith('\\') ? r : r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>\\server\share for a UNC path, else the path itself: a share that does not answer hangs for every folder in it.</summary>
        private static string ShareRoot(string path)
        {
            string p = Normalize(path);
            if (!p.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return p;
            }

            string[] parts = p.Substring(2).Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? @"\\" + parts[0] + "\\" + parts[1] : p;
        }

        private static string Normalize(string path)
        {
            string p = path.Trim().Replace('/', '\\');
            // Keep the separators of a drive root (C:\) and of a UNC prefix; drop others at the end.
            while (p.Length > 3 && p.EndsWith('\\'))
            {
                p = p.Substring(0, p.Length - 1);
            }

            return p;
        }
    }
}
