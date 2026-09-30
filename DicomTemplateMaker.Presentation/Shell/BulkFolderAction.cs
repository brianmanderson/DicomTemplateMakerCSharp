using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DicomTemplateMakerGUI.Shell
{
    public sealed record FolderFailure(string Folder, string Message);

    public sealed class BulkFolderResult
    {
        public List<string> Done { get; } = new List<string>();

        public List<FolderFailure> Failed { get; } = new List<FolderFailure>();
    }

    /// <summary>Applies an action (moving to the Recycle Bin) to several folders; one failure is recorded and the rest go on.</summary>
    public static class BulkFolderAction
    {
        public static BulkFolderResult Run(IEnumerable<string> folders, Action<string> action, ILogger logger, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(folders);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(logger);
            var result = new BulkFolderResult();
            foreach (string folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    action(folder);
                    result.Done.Add(folder);
                    logger.LogInformation("Moved {Folder} to the Recycle Bin.", folder);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException
                    || ex is NotSupportedException || ex is ArgumentException || ex is OperationCanceledException)
                {
                    // PlatformNotSupportedException is a NotSupportedException; OperationCanceledException is the user
                    // cancelling a Windows dialog about this folder.
                    result.Failed.Add(new FolderFailure(folder, ex.Message));
                    logger.LogError(ex, "Could not move {Folder} to the Recycle Bin.", folder);
                }
            }

            return result;
        }
    }
}
