using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class AppVersionTests
{
    // Release builds pass the tag's version; any other build must say it is a
    // development build rather than claim the SDK's default 1.0.0 or a past release.
    [Fact]
    public void SourceBuildsReportTheDevelopmentVersion()
    {
        Assert.Equal("0.0.0-dev", AppVersion.Current);
    }
}