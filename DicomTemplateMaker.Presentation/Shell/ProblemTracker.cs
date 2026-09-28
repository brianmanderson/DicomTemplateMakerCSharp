using System;
using System.Collections.Generic;
using System.Linq;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>Remembers which problems were already shown in a message box this session.</summary>
    public sealed class ProblemTracker
    {
        private readonly HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The problems not returned by an earlier call; each distinct problem is returned once per session.</summary>
        public IReadOnlyList<TemplateProblem> TakeNew(IEnumerable<TemplateProblem> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);
            return problems.Where(p => shown.Add(p.Key)).ToList();
        }
    }
}
