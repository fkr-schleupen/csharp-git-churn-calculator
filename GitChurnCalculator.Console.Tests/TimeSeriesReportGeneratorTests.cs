using System.Text.Json;
using GitChurnCalculator.Console.Reporting;
using GitChurnCalculator.Models;
using Xunit;

namespace GitChurnCalculator.Console.Tests;

public class TimeSeriesReportGeneratorTests
{
    private static IReadOnlyList<TimeSeriesPoint> BuildTwoPoints()
    {
        var fooEarly = new FileChurnResult
        {
            FilePath = "src/Foo.cs",
            TotalCommits = 10,
            LinesAdded = 40,
            LinesRemoved = 12,
            FirstCommitDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastCommitDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            AgeDays = 14,
            ChangesPerWeek = 5.0,
            ChangesPerMonth = 21.43,
            ChangesPerYear = 260.71,
            CommitsLast7Days = 2,
            CommitsLast30Days = 8,
            CommitsLast365Days = 10,
            TotalUniqueAuthors = 2,
            UniqueAuthorsLast7Days = 1,
            UniqueAuthorsLast30Days = 2,
            UniqueAuthorsLast365Days = 2,
            CoveragePercent = 80.12,
            ChurnRiskScore = 4.0,
        };

        var barEarly = new FileChurnResult
        {
            FilePath = "src/Bar.cs",
            TotalCommits = 5,
            LinesAdded = 10,
            LinesRemoved = 4,
            FirstCommitDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastCommitDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            AgeDays = 14,
            ChangesPerWeek = 2.5,
            ChangesPerMonth = 10.71,
            ChangesPerYear = 130.35,
            CommitsLast7Days = 1,
            CommitsLast30Days = 4,
            CommitsLast365Days = 5,
            TotalUniqueAuthors = 1,
            UniqueAuthorsLast7Days = 1,
            UniqueAuthorsLast30Days = 1,
            UniqueAuthorsLast365Days = 1,
            CoveragePercent = null,
            ChurnRiskScore = 2.6,
        };

        var fooLate = new FileChurnResult
        {
            FilePath = fooEarly.FilePath,
            TotalCommits = fooEarly.TotalCommits,
            LinesAdded = 100,
            LinesRemoved = 50,
            FirstCommitDate = fooEarly.FirstCommitDate,
            LastCommitDate = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            AgeDays = fooEarly.AgeDays,
            ChangesPerWeek = fooEarly.ChangesPerWeek,
            ChangesPerMonth = fooEarly.ChangesPerMonth,
            ChangesPerYear = fooEarly.ChangesPerYear,
            CommitsLast7Days = fooEarly.CommitsLast7Days,
            CommitsLast30Days = fooEarly.CommitsLast30Days,
            CommitsLast365Days = fooEarly.CommitsLast365Days,
            TotalUniqueAuthors = fooEarly.TotalUniqueAuthors,
            UniqueAuthorsLast7Days = fooEarly.UniqueAuthorsLast7Days,
            UniqueAuthorsLast30Days = fooEarly.UniqueAuthorsLast30Days,
            UniqueAuthorsLast365Days = fooEarly.UniqueAuthorsLast365Days,
            CoveragePercent = fooEarly.CoveragePercent,
            ChurnRiskScore = 4.2,
        };

        var barLate = new FileChurnResult
        {
            FilePath = barEarly.FilePath,
            TotalCommits = barEarly.TotalCommits,
            LinesAdded = 30,
            LinesRemoved = 10,
            FirstCommitDate = barEarly.FirstCommitDate,
            LastCommitDate = barEarly.LastCommitDate,
            AgeDays = barEarly.AgeDays,
            ChangesPerWeek = barEarly.ChangesPerWeek,
            ChangesPerMonth = barEarly.ChangesPerMonth,
            ChangesPerYear = barEarly.ChangesPerYear,
            CommitsLast7Days = barEarly.CommitsLast7Days,
            CommitsLast30Days = barEarly.CommitsLast30Days,
            CommitsLast365Days = barEarly.CommitsLast365Days,
            TotalUniqueAuthors = barEarly.TotalUniqueAuthors,
            UniqueAuthorsLast7Days = barEarly.UniqueAuthorsLast7Days,
            UniqueAuthorsLast30Days = barEarly.UniqueAuthorsLast30Days,
            UniqueAuthorsLast365Days = barEarly.UniqueAuthorsLast365Days,
            CoveragePercent = barEarly.CoveragePercent,
            ChurnRiskScore = 3.2,
        };

        return new List<TimeSeriesPoint>
        {
            new() { AsOf = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc), Files = new[] { fooEarly, barEarly } },
            new() { AsOf = new DateTime(2024, 1, 14, 0, 0, 0, DateTimeKind.Utc), Files = new[] { fooLate, barLate } },
        };
    }

    // ── CSV ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CsvTimeSeries_TwoPoints_IncludesAsOfColumn()
    {
        var generator = new CsvTimeSeriesReportGenerator();
        var output = generator.Generate(BuildTwoPoints(), "repo");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header contains AsOf as first column
        Assert.StartsWith("AsOf,File,", lines[0]);

        Assert.Equal(5, lines.Length);

        Assert.StartsWith("2024-01-07,", lines[1]);
        Assert.StartsWith("2024-01-07,", lines[2]);
        Assert.StartsWith("2024-01-14,", lines[3]);
        Assert.StartsWith("2024-01-14,", lines[4]);
    }

    [Fact]
    public void CsvTimeSeries_EmptyPoints_OnlyHeader()
    {
        var generator = new CsvTimeSeriesReportGenerator();
        var output = generator.Generate(Array.Empty<TimeSeriesPoint>(), "repo");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("AsOf,", lines[0]);
    }

    // ── JSON ─────────────────────────────────────────────────────────────────

    [Fact]
    public void JsonTimeSeries_TwoPoints_SerializesAsOfAndFiles()
    {
        var generator = new JsonTimeSeriesReportGenerator();
        var output = generator.Generate(BuildTwoPoints(), "repo");

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());

        var firstPoint = root[0];
        Assert.True(firstPoint.TryGetProperty("asOf", out var asOfProp));
        Assert.Contains("2024-01-07", asOfProp.GetString());

        Assert.True(firstPoint.TryGetProperty("files", out var filesProp));
        Assert.Equal(JsonValueKind.Array, filesProp.ValueKind);
        Assert.Equal(2, filesProp.GetArrayLength());

        var firstFile = filesProp[0];
        Assert.True(firstFile.TryGetProperty("filePath", out var pathProp));
        Assert.Equal("src/Foo.cs", pathProp.GetString());
        Assert.Equal("src/Bar.cs", filesProp[1].GetProperty("filePath").GetString());

        var secondBucket = root[1].GetProperty("files");
        Assert.Equal(2, secondBucket.GetArrayLength());
    }

    [Fact]
    public void JsonTimeSeries_EmptyPoints_ReturnsEmptyArray()
    {
        var generator = new JsonTimeSeriesReportGenerator();
        var output = generator.Generate(Array.Empty<TimeSeriesPoint>(), "repo");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // ── HTML ─────────────────────────────────────────────────────────────────

    [Fact]
    public void HtmlTimeSeries_TwoPoints_ContainsBothSections()
    {
        var generator = new HtmlTimeSeriesReportGenerator();
        var output = generator.Generate(BuildTwoPoints(), "/my/repo");

        // Both asOf dates should appear as section labels
        Assert.Contains("2024-01-07", output);
        Assert.Contains("2024-01-14", output);

        // Both file paths should be present
        Assert.Contains("src/Foo.cs", output);
        Assert.Contains("src/Bar.cs", output);

        // Two <details> sections
        var detailsCount = CountOccurrences(output, "<details");
        Assert.Equal(2, detailsCount);

        // Per-section filter controls are present for table filtering
        Assert.Contains("data-filter-scope", output);
        Assert.Contains("data-table-filters", output);
        Assert.Contains("data-filter-file", output);
        Assert.Contains("data-filter-coverage-op", output);
        Assert.Contains("data-filter-coverage-value", output);
        Assert.Contains("data-filter-churn-op", output);
        Assert.Contains("data-filter-churn-value", output);
        Assert.Contains("data-filter-reset", output);
        Assert.Contains("const applyFilters = () =>", output);
    }

    [Fact]
    public void HtmlTimeSeries_SubtitleIsHtmlEncoded()
    {
        var generator = new HtmlTimeSeriesReportGenerator();
        var output = generator.Generate(BuildTwoPoints(), "<repo & path>");

        Assert.Contains("&lt;repo &amp; path&gt;", output);
        Assert.DoesNotContain("<repo & path>", output);
    }

    [Fact]
    public void HtmlTimeSeries_EmptyPoints_RendersValidPage()
    {
        var generator = new HtmlTimeSeriesReportGenerator();
        var output = generator.Generate(Array.Empty<TimeSeriesPoint>(), "repo");

        Assert.StartsWith("<!DOCTYPE html>", output.TrimStart());
        Assert.Contains("0 time points", output);
    }

    [Fact]
    public void GraphTimeSeries_TwoPoints_RendersOneSeriesPerFile()
    {
        var generator = new HtmlTimeSeriesGraphReportGenerator();
        var output = generator.Generate(BuildTwoPoints(), "/my/repo");

        Assert.StartsWith("<!DOCTYPE html>", output.TrimStart());
        Assert.Contains("Git churn risk graph", output);
        Assert.Contains("import * as d3", output);
        Assert.Contains("\"filePath\": \"src/Foo.cs\"", output);
        Assert.Contains("\"filePath\": \"src/Bar.cs\"", output);
        Assert.Contains("\"churnRiskScore\": 2.6", output);
        Assert.Contains("\"churnRiskScore\": 3.2", output);
        Assert.Contains("\"churnRiskScore\": 4.2", output);
        Assert.Contains("\"changesPerWeek\": 5", output);
        Assert.Contains("\"linesAdded\": 100,", output);
        Assert.Contains("\"avgDeltaLinesAddedPerBucket\": 60,", output);
        Assert.Contains("\"avgDeltaLinesAddedPerBucket\": 20,", output);
        Assert.Contains("\"coveragePercent\": 80.12,", output);
        Assert.Contains("\"linesAddedAvgPerCommit\": 10,", output);
        Assert.Contains("new ResizeObserver(render).observe(container)", output);
        Assert.Contains("classed('is-active'", output);
        Assert.Contains("legend-item", output);
        Assert.Contains("Coverage:", output);
        Assert.Contains("Lines added (cum.):", output);
        Assert.Contains("Avg Δ lines added:", output);
        Assert.Contains("Across time series (Δ between plotted steps)", output);
        Assert.Contains("Changes/week:", output);
        Assert.Contains("Churn risk:", output);
    }

    [Fact]
    public void GraphTimeSeries_EmptyPoints_RendersEmptyState()
    {
        var generator = new HtmlTimeSeriesGraphReportGenerator();
        var output = generator.Generate(Array.Empty<TimeSeriesPoint>(), "repo");

        Assert.Contains("0 time points, 0 top file series", output);
        Assert.Contains("No graph data to display.", output);
    }

    [Fact]
    public void GraphTimeSeries_MoreThanFiftyFiles_RendersTopFiftyOnly()
    {
        var files = Enumerable.Range(1, 51)
            .Select(index => new FileChurnResult
            {
                FilePath = $"src/File{index:D2}.cs",
                TotalCommits = index,
                LinesAdded = 0,
                LinesRemoved = 0,
                FirstCommitDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastCommitDate = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
                AgeDays = 7,
                ChangesPerWeek = index,
                ChangesPerMonth = index,
                ChangesPerYear = index,
                CommitsLast7Days = index,
                CommitsLast30Days = index,
                CommitsLast365Days = index,
                TotalUniqueAuthors = 1,
                UniqueAuthorsLast7Days = 1,
                UniqueAuthorsLast30Days = 1,
                UniqueAuthorsLast365Days = 1,
                CoveragePercent = null,
                ChurnRiskScore = index,
            })
            .ToArray();

        var generator = new HtmlTimeSeriesGraphReportGenerator();
        var output = generator.Generate(
            new[] { new TimeSeriesPoint { AsOf = new DateTime(2024, 1, 7), Files = files } },
            "repo");

        Assert.Contains("50 top file series", output);
        Assert.Contains("\"filePath\": \"src/File51.cs\"", output);
        Assert.Contains("\"filePath\": \"src/File02.cs\"", output);
        Assert.DoesNotContain("\"filePath\": \"src/File01.cs\"", output);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
