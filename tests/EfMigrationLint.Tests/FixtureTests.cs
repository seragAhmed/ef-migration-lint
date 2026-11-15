using EfMigrationLint.Analysis;
using static EfMigrationLint.Tests.TestHelpers;

namespace EfMigrationLint.Tests;

/// <summary>
/// Expected rule IDs for every file under samples/. When you add a fixture, add a row here.
/// </summary>
public class FixtureTests
{
    public static TheoryData<string, string[]> Fixtures => new()
    {
        { "Safe/Migrations/20260110090000_AddCustomerNickname.cs", [] },
        { "Safe/Migrations/20260111090000_RenameBioToBiography.cs", [] },
        { "Safe/Migrations/20260112090000_AddOrderTotals.cs", [] },
        { "Destructive/Migrations/20260111090000_RenameBioToBiography.cs", ["EFML002"] },
        { "Destructive/Migrations/20260113090000_DropLegacyOrders.cs", ["EFML001"] },
        { "NeedsReview/Migrations/20260114090000_AddRequiredTenantId.cs", ["EFML003"] },
        { "NeedsReview/Migrations/20260114093000_NormalizeCustomerEmails.cs", ["EFML004"] },
        { "EdgeCases/Migrations/20260115090000_CreateAuditLog.cs", [] },
        { "EdgeCases/Migrations/20260115093000_UnrelatedMatchingNames.cs", [] },
        { "Paths With Spaces/Migrations/20260116090000_Drop Audit Log.cs", ["EFML001"] },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_produces_expected_rule_ids(string relativePath, string[] expectedRuleIds)
    {
        var fullPath = SamplePath(relativePath.Split('/'));
        var analysis = new MigrationAnalyzer().AnalyzeSource(File.ReadAllText(fullPath), relativePath);

        Assert.Null(analysis.Error);
        Assert.True(analysis.MigrationCount >= 1, "Fixture should contain a migration Up() method.");
        Assert.Equal(expectedRuleIds, analysis.Findings.Select(f => f.RuleId).ToArray());
    }

    [Fact]
    public void Every_sample_migration_has_an_expectation()
    {
        var listed = Fixtures.Select(row => (string)row[0]).ToHashSet(StringComparer.Ordinal);
        var samplesRoot = SamplePath();

        var unlisted = Directory.EnumerateFiles(samplesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !InputCollector.IsGeneratedFile(path))
            .Select(path => Path.GetRelativePath(samplesRoot, path).Replace('\\', '/'))
            .Where(path => !listed.Contains(path))
            .ToList();

        Assert.Empty(unlisted);
    }

    [Fact]
    public void Generated_files_in_samples_are_skipped()
    {
        var inputs = InputCollector.Collect([SamplePath("Safe")], RepoRoot);

        Assert.Empty(inputs.Errors);
        Assert.Equal(
            ["samples/Safe/Migrations/20260110090000_AddCustomerNickname.Designer.cs", "samples/Safe/Migrations/ShopDbContextModelSnapshot.cs"],
            inputs.Skipped.Order(StringComparer.Ordinal));
        Assert.Equal(3, inputs.Files.Count);
    }
}
