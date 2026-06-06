using WsFiler.Infra.Updates;

public sealed class GitHubReleaseCheckerTests
{
    [Theory]
    [InlineData("v1.2.4", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.3.0", false)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("1.2.3-beta.1", "1.2.2", true)]
    [InlineData("not-a-version", "1.2.3", false)]
    public void IsNewerVersion_ComparesReleaseTags(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, GitHubReleaseChecker.IsNewerVersion(candidate, current));
    }
}
