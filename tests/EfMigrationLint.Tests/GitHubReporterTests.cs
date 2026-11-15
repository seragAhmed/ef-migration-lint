using EfMigrationLint.Reporting;

namespace EfMigrationLint.Tests;

public class GitHubReporterTests
{
    [Fact]
    public void Escapes_message_data()
    {
        Assert.Equal("50%25 done%0D%0Anext: a, b", GitHubReporter.EscapeData("50% done\r\nnext: a, b"));
    }

    [Fact]
    public void Escapes_property_values()
    {
        Assert.Equal("C%3A/repo/a%2Cb.cs", GitHubReporter.EscapeProperty("C:/repo/a,b.cs"));
        Assert.Equal("dir with spaces/x.cs", GitHubReporter.EscapeProperty("dir with spaces/x.cs"));
    }

    [Fact]
    public void Writes_one_line_per_finding()
    {
        var finding = new Finding("EFML002", "DropColumn", Severity.Error, "a,b/M.cs", 3, 5, "Line one.\nLine two.", "Do: this.", "https://example.test/EFML002.md");
        var result = new ScanResult([], [], [], [finding]);
        var output = new StringWriter();

        GitHubReporter.Write(output, result);

        var firstLine = output.ToString().Split(Environment.NewLine)[0];
        Assert.Equal(
            "::error file=a%2Cb/M.cs,line=3,col=5,title=EFML002 DropColumn::Line one.%0ALine two.%0AReview: Do: this.%0ADocs: https://example.test/EFML002.md",
            firstLine);
    }
}
