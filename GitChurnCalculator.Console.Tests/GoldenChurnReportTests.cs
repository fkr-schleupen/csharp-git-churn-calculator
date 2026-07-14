using System.Text.Json;
using GitChurnCalculator.Console.Reporting;
using GitChurnCalculator.Models;
using Xunit;

namespace GitChurnCalculator.Console.Tests;

/// <summary>
/// Golden-style assertions for report output (fixed UTC dates, invariant numeric formatting).
/// </summary>
public class GoldenChurnReportTests
{
    private static readonly DateTime T0 = new(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<FileChurnResult> GoldenRows() =>
        new[]
        {
            new FileChurnResult
            {
                FilePath = "src/X.cs",
                TotalCommits = 3,
                LinesAdded = 247,
                LinesRemoved = 189,
                FirstCommitDate = T0.AddDays(-9),
                LastCommitDate = T0,
                AgeDays = 9,
                ChangesPerWeek = 2.3333,
                ChangesPerMonth = 10.0,
                ChangesPerYear = 121.67,
                CommitsLast7Days = 1,
                CommitsLast30Days = 2,
                CommitsLast365Days = 3,
                TotalUniqueAuthors = 2,
                UniqueAuthorsLast7Days = 1,
                UniqueAuthorsLast30Days = 2,
                UniqueAuthorsLast365Days = 2,
                CoveragePercent = null,
                ChurnRiskScore = 4.6666,
            },
        };

    [Fact]
    public void CsvGolden_MatchesExpectedHeaderAndRow()
    {
        var gen = new CsvChurnReportGenerator();
        var csv = gen.Generate(GoldenRows(), "ignored");

        var expectedHeader =
            "File,TotalCommits,LinesAdded,LinesRemoved,FirstCommitDate,LastCommitDate,AgeDays,ChangesPerWeek,ChangesPerMonth,ChangesPerYear,CommitsLast7Days,CommitsLast30Days,CommitsLast365Days,TotalUniqueAuthors,UniqueAuthorsLast7Days,UniqueAuthorsLast30Days,UniqueAuthorsLast365Days,CoveragePercent,ChurnRiskScore";
        var lines = csv.TrimEnd().Split('\n');
        Assert.Equal(expectedHeader, lines[0].TrimEnd('\r'));
        Assert.StartsWith("\"src/X.cs\",3,247,189,2024-03-01,2024-03-10,9,2.33", lines[1].TrimEnd('\r'), StringComparison.Ordinal);
        Assert.EndsWith(",4.6666", lines[1].TrimEnd('\r'), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonGolden_RoundTripsFilePathAndScore()
    {
        var gen = new JsonChurnReportGenerator();
        var json = gen.Generate(GoldenRows(), "ignored");
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement[0];
        Assert.Equal("src/X.cs", el.GetProperty("filePath").GetString());
        Assert.Equal(247, el.GetProperty("linesAdded").GetInt32());
        Assert.Equal(189, el.GetProperty("linesRemoved").GetInt32());
        Assert.Equal(4.6666, el.GetProperty("churnRiskScore").GetDouble(), 4);
    }

    [Fact]
    public void HtmlGolden_ContainsStableTitleAndEncodedSubtitle()
    {
        var gen = new HtmlTableChurnReportGenerator();
        var html = gen.Generate(GoldenRows(), "<repo & test>");

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("Git churn risk report", html);
        Assert.Contains("&lt;repo &amp; test&gt;", html);
        Assert.Contains("src/X.cs", html);
        Assert.Contains("4.6666", html);
        Assert.Contains("data-sortable=\"true\"", html);
        Assert.Contains("data-sort-type=\"number\"><button", html);
        Assert.Contains("querySelectorAll('table[data-sortable=\"true\"]')", html);
        Assert.Contains("data-filter-scope", html);
        Assert.Contains("data-table-filters", html);
        Assert.Contains("data-filter-file", html);
        Assert.Contains("data-filter-coverage-op", html);
        Assert.Contains("data-filter-coverage-value", html);
        Assert.Contains("data-filter-churn-op", html);
        Assert.Contains("data-filter-churn-value", html);
        Assert.Contains("data-filter-reset", html);
        Assert.Contains("const applyFilters = () =>", html);
    }
}
