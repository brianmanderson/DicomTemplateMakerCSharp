using System;

namespace ROIOntologyClass
{
    /// <summary>
    /// A template file (All_ROIs.json, a legacy ROIs folder) or the ontology library (All_Ontologies.json) exists
    /// but cannot be read. Nothing was written: the file is left as it is so that it can be repaired, and code that
    /// sees this exception must not save over it.
    /// </summary>
    public sealed class TemplateLoadException : Exception
    {
        public TemplateLoadException(string filePath, string problem, Exception? innerException = null)
            : base($"Could not read '{filePath}': {problem}", innerException)
        {
            FilePath = filePath;
            Problem = problem;
        }

        /// <summary>The file, or legacy folder, that could not be read.</summary>
        public string FilePath { get; }

        /// <summary>What is wrong with it, without the path.</summary>
        public string Problem { get; }
    }
}
