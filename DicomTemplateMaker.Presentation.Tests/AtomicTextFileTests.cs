using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

/// <summary>The Presentation copy of the atomic write (ui-settings.json, Varian XML export): a failure leaves the old file and no temp file.</summary>
public sealed class AtomicTextFileTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string[] FilesIn(string directory) => Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void A_writer_that_fails_halfway_leaves_the_old_content_and_no_temp_file()
    {
        string target = Path.Combine(folder.Path, "Brain.xml");
        File.WriteAllText(target, "old");
        string? tempSeen = null;

        Assert.Throws<IOException>(() => AtomicTextFile.WriteVia(target, temp =>
        {
            tempSeen = temp;
            File.WriteAllText(temp, "half");
            throw new IOException("disk full");
        }));

        Assert.Equal("old", File.ReadAllText(target));
        Assert.NotNull(tempSeen);
        Assert.False(File.Exists(tempSeen));
        Assert.Equal(new[] { "Brain.xml" }, FilesIn(folder.Path));
    }

    [Fact]
    public void A_target_that_cannot_be_replaced_leaves_no_temp_file()
    {
        string target = folder.Sub("ui-settings.json"); // a directory where the file should be

        // Linux reports the rename onto a directory as an IOException (EISDIR), Windows as an UnauthorizedAccessException.
        Exception failure = Assert.ThrowsAny<Exception>(() => AtomicTextFile.Write(target, "{}"));
        Assert.True(failure is IOException || failure is UnauthorizedAccessException, $"Unexpected {failure.GetType()}: {failure.Message}");

        Assert.Empty(FilesIn(folder.Path));
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void The_content_is_written_through_a_temp_file_next_to_the_target()
    {
        string target = Path.Combine(folder.Path, "Brain.xml");
        string? tempSeen = null;

        AtomicTextFile.WriteVia(target, temp =>
        {
            tempSeen = temp;
            Assert.False(File.Exists(target)); // the target appears only with the final rename
            File.WriteAllText(temp, "new");
        });

        Assert.Equal(folder.Path, Path.GetDirectoryName(tempSeen));
        Assert.EndsWith(".tmp", tempSeen, StringComparison.Ordinal);
        Assert.Equal("new", File.ReadAllText(target));
        Assert.Equal(new[] { "Brain.xml" }, FilesIn(folder.Path));
    }
}
