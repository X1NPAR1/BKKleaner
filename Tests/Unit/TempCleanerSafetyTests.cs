using BKKleaner.Services;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class TempCleanerSafetyTests
{
    [Fact]
    public void User_temp_path_is_cleanable()
    {
        var path = Path.Combine(Path.GetTempPath(), "somefile.tmp");
        Assert.True(TempCleanerService.IsPathSafeToClean(path));
    }

    [Fact]
    public void Windows_temp_is_cleanable()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.True(TempCleanerService.IsPathSafeToClean(Path.Combine(windows, "Temp", "x.log")));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\Program Files\Important\app.exe")]
    [InlineData(@"D:\MyDocuments\thesis.docx")]
    public void System_and_user_data_paths_are_rejected(string path)
    {
        Assert.False(TempCleanerService.IsPathSafeToClean(path));
    }

    [Fact]
    public void Windows_root_itself_is_rejected()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.False(TempCleanerService.IsPathSafeToClean(windows));
    }

    [Fact]
    public void User_profile_root_is_rejected()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(TempCleanerService.IsPathSafeToClean(profile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_paths_are_rejected(string path)
    {
        Assert.False(TempCleanerService.IsPathSafeToClean(path));
    }

    [Fact]
    public void Traversal_out_of_temp_is_rejected()
    {
        var sneaky = Path.Combine(Path.GetTempPath(), "..", "..", "Windows", "System32", "x.dll");
        Assert.False(TempCleanerService.IsPathSafeToClean(sneaky));
    }
}
