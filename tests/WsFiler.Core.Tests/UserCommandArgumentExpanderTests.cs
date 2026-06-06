using WsFiler.Core.Commands;

namespace WsFiler.Core.Tests;

public sealed class UserCommandArgumentExpanderTests
{
    [Fact]
    public void Expand_ReplacesScalarMacros()
    {
        var context = new UserCommandContext(
            @"C:\Work Dir",
            new UserCommandItem("sample file.txt", @"C:\Work Dir\sample file.txt"),
            []);

        var result = UserCommandArgumentExpander.Expand(
            "--cwd \"{currentDir}\" --file \"{currentFileName}\" --path \"{currentFullPath}\"",
            context);

        Assert.Equal(
            ["--cwd", @"C:\Work Dir", "--file", "sample file.txt", "--path", @"C:\Work Dir\sample file.txt"],
            result);
    }

    [Fact]
    public void Expand_ExpandsMarkedFullPathsAsSeparateArguments()
    {
        var context = new UserCommandContext(
            "/tmp",
            new UserCommandItem("current.txt", "/tmp/current.txt"),
            [
                new UserCommandItem("one.txt", "/tmp/one.txt"),
                new UserCommandItem("two words.txt", "/tmp/two words.txt"),
            ]);

        var result = UserCommandArgumentExpander.Expand("--open {markedFullPaths}", context);

        Assert.Equal(["--open", "/tmp/one.txt", "/tmp/two words.txt"], result);
    }

    [Fact]
    public void Expand_UsesCurrentItemForMarkedMacrosWhenNothingIsMarked()
    {
        var context = new UserCommandContext(
            "/tmp",
            new UserCommandItem("current.txt", "/tmp/current.txt"),
            []);

        var result = UserCommandArgumentExpander.Expand("{markedFileNames} {markedFullPaths}", context);

        Assert.Equal(["current.txt", "/tmp/current.txt"], result);
    }

    [Fact]
    public void Expand_LeavesUnknownMacrosUnchanged()
    {
        var context = new UserCommandContext("/tmp", null, []);

        var result = UserCommandArgumentExpander.Expand("--flag {unknown}", context);

        Assert.Equal(["--flag", "{unknown}"], result);
    }
}
