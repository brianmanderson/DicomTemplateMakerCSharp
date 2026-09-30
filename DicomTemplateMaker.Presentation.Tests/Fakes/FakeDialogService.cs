using DicomTemplateMakerGUI.ViewModels;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

public sealed record ShownDialog(string Kind, string Title, string Message);

/// <summary>Records every dialog. Confirmations take their answers from <see cref="Answers"/>; an unexpected one fails the test.</summary>
public sealed class FakeDialogService : IDialogService
{
    public Queue<bool> Answers { get; } = new();

    public List<ShownDialog> Shown { get; } = new();

    public IEnumerable<ShownDialog> Confirmations => Shown.Where(d => d.Kind == "confirm");

    public bool Confirm(string title, string message)
    {
        Shown.Add(new ShownDialog("confirm", title, message));
        if (Answers.Count == 0)
        {
            throw new InvalidOperationException("Unexpected confirmation: " + message);
        }

        return Answers.Dequeue();
    }

    public void ShowInfo(string title, string message) => Shown.Add(new ShownDialog("info", title, message));

    public void ShowWarning(string title, string message) => Shown.Add(new ShownDialog("warning", title, message));

    public void ShowError(string title, string message) => Shown.Add(new ShownDialog("error", title, message));
}
