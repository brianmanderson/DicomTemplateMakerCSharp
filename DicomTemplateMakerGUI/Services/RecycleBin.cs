using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using DicomTemplateMakerGUI.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Moves folders to the Windows Recycle Bin instead of deleting them.</summary>
    internal static class RecycleBin
    {
        /// <summary>
        /// Moves each folder to the Recycle Bin on a dedicated STA thread (the Windows shell expects one), blocking the
        /// calling (background) thread until done. One failure is recorded and the others go on.
        /// </summary>
        public static BulkFolderResult SendFolders(IReadOnlyList<string> folders, ILogger logger, CancellationToken cancellationToken)
        {
            BulkFolderResult? result = null;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    result = BulkFolderAction.Run(folders, SendFolder, logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Handed to the calling thread below, where the caller's handler reports it.
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return result ?? throw new InvalidOperationException("The Recycle Bin thread ended without a result.");
        }

        /// <summary>The type of the drive whose root is <paramref name="root"/>; Unknown when it cannot be told.</summary>
        public static DriveType DriveTypeOf(string root)
        {
            try
            {
                return new DriveInfo(root).DriveType;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return DriveType.Unknown;
            }
        }

        private static void SendFolder(string folder)
        {
            // Checked again here: only folders the Recycle Bin can take are passed on, never one that would be destroyed.
            string? reason = RecycleBinRule.WhyNotRecyclable(folder, DriveTypeOf);
            if (reason != null)
            {
                throw new IOException(folder + " was not deleted: " + reason + ", which has no Recycle Bin.");
            }

            // AllDialogs, not OnlyErrorDialogs: the latter adds FOF_NOCONFIRMATION, under which the shell deletes a folder
            // it cannot recycle (too big, or the bin is turned off for the drive) permanently and without a word. With the
            // shell's own dialogs it asks before any permanent delete; answering No cancels (OperationCanceledException),
            // which is reported as "not moved and unchanged".
            FileSystem.DeleteDirectory(folder, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        }
    }
}
