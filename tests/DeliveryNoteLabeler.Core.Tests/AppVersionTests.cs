using DeliveryNoteLabeler.Core.Updates;

namespace DeliveryNoteLabeler.Core.Tests;

public class AppVersionTests
{
    [Fact]
    public void CoreAssembly_HasReleaseVersion()
    {
        var coreVersion = typeof(AppVersion).Assembly.GetName().Version;

        Assert.NotNull(coreVersion);
        Assert.True(coreVersion!.Major >= 1);
        Assert.True(coreVersion.Minor >= 0);
    }

    [Fact]
    public void Display_IsThreePartVersion()
    {
        var parts = AppVersion.Display.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var major));
        Assert.True(major >= 1);
    }
}
