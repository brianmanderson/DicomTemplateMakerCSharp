using System;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    public class FolderWatcher
    {
        private bool folder_changed;
        public bool Folder_Changed
        {
            get
            {
                return folder_changed;
            }
            set
            {
                folder_changed = value;
            }
        }
        public FolderWatcher(string directory)
        {
            FileSystemWatcher file_watcher = new FileSystemWatcher(directory);
            file_watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;
            file_watcher.Changed += OnChanged;
            file_watcher.EnableRaisingEvents = true;
        }
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                folder_changed = false;
                return;
            }
            folder_changed = true;
        }
    }
}
