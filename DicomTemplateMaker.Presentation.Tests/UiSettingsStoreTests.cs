using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class UiSettingsStoreTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly CapturingLogger logger = new();

    public void Dispose() => folder.Dispose();

    private string SettingsPath => Path.Combine(folder.Path, "data", "ui-settings.json");

    private UiSettingsStore Store() => new(SettingsPath, logger);

    [Fact]
    public void A_missing_file_gives_defaults_without_a_problem()
    {
        UiSettingsStore store = Store();

        UiSettings settings = store.Load();

        Assert.Null(settings.TemplateRoot);
        Assert.Null(settings.VarianXmlShare);
        Assert.Null(store.LoadProblem);
        Assert.False(File.Exists(SettingsPath));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Settings_round_trip_with_camel_case_names()
    {
        UiSettingsStore store = Store();
        store.Load();
        Assert.True(store.SetTemplateRoot(@"D:\Templates"));
        Assert.True(store.RememberFolder(FolderPurpose.Output, @"D:\Out"));
        Assert.True(store.RememberFolder(FolderPurpose.RtFile, @"D:\RTs"));
        Assert.True(store.RememberFolder(FolderPurpose.VarianXml, @"D:\Xml"));
        Assert.True(store.RememberFolder(FolderPurpose.MonitoredPath, @"D:\Incoming"));

        UiSettingsStore reloaded = Store();
        UiSettings settings = reloaded.Load();

        Assert.Equal(@"D:\Templates", settings.TemplateRoot);
        Assert.Equal(@"D:\Out", settings.GetLastFolder(FolderPurpose.Output));
        Assert.Equal(@"D:\RTs", settings.GetLastFolder(FolderPurpose.RtFile));
        Assert.Equal(@"D:\Xml", settings.GetLastFolder(FolderPurpose.VarianXml));
        Assert.Equal(@"D:\Incoming", settings.GetLastFolder(FolderPurpose.MonitoredPath));
        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"templateRoot\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastOutputFolder\"", json, StringComparison.Ordinal);
        Assert.Contains("\"varianXmlShare\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_varian_share_is_empty_by_default_and_read_when_set_by_hand()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ // edited by hand\n \"varianXmlShare\": \"  \\\\\\\\server\\\\share  \", \"templateRoot\": \"   \", }");

        UiSettings settings = Store().Load();

        Assert.Equal(@"\\server\share", settings.VarianXmlShare);
        Assert.Null(settings.TemplateRoot);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"templateRoot\": 5 }")]
    public void A_corrupt_file_falls_back_to_defaults_is_logged_and_kept_as_a_backup(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);
        UiSettingsStore store = Store();

        UiSettings settings = store.Load();

        Assert.Null(settings.TemplateRoot);
        Assert.NotNull(store.LoadProblem);
        Assert.Contains(store.BackupPath, store.LoadProblem, StringComparison.Ordinal);
        Assert.Equal(content, File.ReadAllText(store.BackupPath));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(SettingsPath, StringComparison.Ordinal));
        // Loading alone never replaces the damaged file.
        Assert.Equal(content, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Saving_after_a_corrupt_load_replaces_the_file_but_keeps_the_backup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ broken");
        UiSettingsStore store = Store();
        store.Load();

        Assert.True(store.SetTemplateRoot(@"D:\Templates"));

        Assert.Equal("{ broken", File.ReadAllText(store.BackupPath));
        Assert.Equal(@"D:\Templates", Store().Load().TemplateRoot);
    }

    [Fact]
    public void Saves_leave_no_temporary_files_behind()
    {
        UiSettingsStore store = Store();
        store.Load();

        store.SetTemplateRoot(@"D:\A");
        store.SetTemplateRoot(@"D:\B");

        Assert.Equal(new[] { "ui-settings.json" }, Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Remembering_the_same_folder_again_does_not_write()
    {
        UiSettingsStore store = Store();
        store.Load();
        store.RememberFolder(FolderPurpose.Output, @"D:\Out");
        DateTime written = File.GetLastWriteTimeUtc(SettingsPath);
        File.SetLastWriteTimeUtc(SettingsPath, written.AddHours(-1));

        Assert.True(store.RememberFolder(FolderPurpose.Output, @"D:\Out"));

        Assert.Equal(written.AddHours(-1), File.GetLastWriteTimeUtc(SettingsPath));
    }

    [Fact]
    public void A_save_that_cannot_write_returns_false_and_logs()
    {
        // The settings path is an existing directory, so the final rename fails.
        Directory.CreateDirectory(SettingsPath);
        UiSettingsStore store = Store();

        Assert.False(store.SetTemplateRoot(@"D:\Templates"));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Settings_written_by_a_newer_version_are_kept_when_this_one_saves()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ \"templateRoot\": \"D:\\\\Templates\", \"recentTemplateRoots\": [\"D:\\\\A\", \"D:\\\\B\"], \"windowPlacement\": { \"left\": 10 } }");
        UiSettingsStore store = Store();
        store.Load();

        Assert.True(store.RememberFolder(FolderPurpose.RtFile, @"D:\RTs"));

        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"recentTemplateRoots\"", json, StringComparison.Ordinal);
        Assert.Contains("\"windowPlacement\"", json, StringComparison.Ordinal);
        Assert.Contains("\"left\": 10", json, StringComparison.Ordinal);
        Assert.Equal(@"D:\Templates", Store().Load().TemplateRoot);
    }

    [Fact]
    public void A_save_by_one_copy_does_not_undo_what_another_copy_saved()
    {
        // Two running copies: copy 2 used to write its stale template folder back over copy 1's choice.
        UiSettingsStore copy1 = Store();
        UiSettingsStore copy2 = Store();
        copy1.Load();
        copy2.Load();

        Assert.True(copy1.SetTemplateRoot(@"D:\NewRoot"));
        Assert.True(copy2.RememberFolder(FolderPurpose.RtFile, @"D:\RTs"));

        UiSettings saved = Store().Load();
        Assert.Equal(@"D:\NewRoot", saved.TemplateRoot);
        Assert.Equal(@"D:\RTs", saved.GetLastFolder(FolderPurpose.RtFile));
        Assert.Equal(@"D:\NewRoot", copy2.Settings.TemplateRoot);
    }

    [Fact]
    public void A_damaged_file_is_not_replaced_while_no_backup_of_it_can_be_made()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ broken");
        UiSettingsStore store = Store();
        Directory.CreateDirectory(store.BackupPath); // a directory where the backup copy should go, so the copy fails
        store.Load();

        Assert.False(store.SetTemplateRoot("/templates"));

        Assert.Equal("{ broken", File.ReadAllText(SettingsPath));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("not saved", StringComparison.Ordinal));
        Assert.Equal("/templates", store.Settings.TemplateRoot); // still used for this session
    }
}
