using System;
using System.Threading;

namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>
    /// Reports progress on the synchronization context that created it (the UI thread in the GUI). Unlike
    /// <see cref="Progress{T}"/>, a report made on that context, or when there is none, runs immediately,
    /// so messages keep their order and tests see them without waiting.
    /// </summary>
    internal sealed class ContextProgress : IProgress<string>
    {
        private readonly Action<string> handler;
        private readonly SynchronizationContext? context;

        public ContextProgress(Action<string> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            context = SynchronizationContext.Current;
        }

        public void Report(string value)
        {
            if (context == null || ReferenceEquals(SynchronizationContext.Current, context))
            {
                handler(value);
            }
            else
            {
                context.Post(_ => handler(value), null);
            }
        }
    }
}
