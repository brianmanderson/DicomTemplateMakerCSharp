using System;
using System.IO;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Where the program keeps things. Files that ship with the program are found next to the executable
    /// (<see cref="AppContext.BaseDirectory"/>, also for a single-file build), never in the working directory; user
    /// data lives in %LOCALAPPDATA%\DicomTemplateMaker and in the template folder.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>%LOCALAPPDATA%\DicomTemplateMaker (shared with the online-template settings and caches).</summary>
        public static string DataDirectory => TemplateSourceCatalog.AppDataDirectory;

        /// <summary>%LOCALAPPDATA%\DicomTemplateMaker\logs: the daily log files.</summary>
        public static string LogsDirectory => Path.Combine(DataDirectory, "logs");

        /// <summary>The window settings (template folder, remembered folders); separate from the online-template settings.json.</summary>
        public static string UiSettingsFile => Path.Combine(DataDirectory, "ui-settings.json");

        /// <summary>The folder that holds the executable and the files that ship with it.</summary>
        public static string ProgramDirectory => AppContext.BaseDirectory;

        /// <summary>The RT Structure Set every generated RT is built from.</summary>
        public static string TemplateRsFile => ProgramFile("template_RS.dcm");

        /// <summary>The four-slice sample CT copied by "Create folder with loadable RTs".</summary>
        public static string SmallCtFolder => ProgramFile("SmallCT");

        /// <summary>A file that ships with the program.</summary>
        public static string ProgramFile(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, name);
        }
    }
}
