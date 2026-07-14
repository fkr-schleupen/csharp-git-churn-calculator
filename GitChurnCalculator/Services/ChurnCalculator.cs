using GitChurnCalculator.Models;
using System.Text.RegularExpressions;

namespace GitChurnCalculator.Services;

public sealed class ChurnCalculator : IChurnCalculator
{
    private readonly IGitDataProvider _gitDataProvider;
    private readonly ICoverageParser _coverageParser;

    public ChurnCalculator(IGitDataProvider gitDataProvider, ICoverageParser coverageParser)
    {
        _gitDataProvider = gitDataProvider;
        _coverageParser = coverageParser;
    }

    public async Task<IReadOnlyList<FileChurnResult>> AnalyzeAsync(
        ChurnAnalysisOptions options,
        IProgress<ChurnProgressUpdate>? progress = null,
        CancellationToken ct = default)
    {
        var repoPath = options.RepositoryPath;
        var now = options.AsOf ?? DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);
        var yearAgo = now.AddDays(-365);

        progress?.Report(new ChurnProgressUpdate("Fetching tracked files", 0, 3));
        var trackedFiles = ApplyPathFilters(
            await _gitDataProvider.GetTrackedFilesAsync(repoPath, ct),
            options.IncludePattern,
            options.ExcludePattern);

        progress?.Report(new ChurnProgressUpdate("Running git queries", 1, 3));
        // Run independent git queries in parallel.
        // When AsOf is set, use date-bounded variants so history is anchored to that point in time.
        Task<Dictionary<string, int>> commitCountsTask;
        Task<Dictionary<string, DateTime>> firstDatesTask;
        Task<Dictionary<string, DateTime>> lastDatesTask;
        Task<Dictionary<string, int>> commits7Task;
        Task<Dictionary<string, int>> commits30Task;
        Task<Dictionary<string, int>> commits365Task;
        Task<Dictionary<string, int>> authorsAllTask;
        Task<Dictionary<string, int>> authors7Task;
        Task<Dictionary<string, int>> authors30Task;
        Task<Dictionary<string, int>> authors365Task;
        Task<Dictionary<string, LineChangeTotals>> lineTotalsTask;
        Task<Dictionary<string, int>> totalLinesTask;

        if (options.AsOf.HasValue)
        {
            commitCountsTask = _gitDataProvider.GetCommitCountsUntilAsync(repoPath, now, ct);
            firstDatesTask = _gitDataProvider.GetFirstCommitDatesUntilAsync(repoPath, now, ct);
            lastDatesTask = _gitDataProvider.GetLastCommitDatesUntilAsync(repoPath, now, ct);
            commits7Task = _gitDataProvider.GetCommitCountsSinceUntilAsync(repoPath, sevenDaysAgo, now, ct);
            commits30Task = _gitDataProvider.GetCommitCountsSinceUntilAsync(repoPath, thirtyDaysAgo, now, ct);
            commits365Task = _gitDataProvider.GetCommitCountsSinceUntilAsync(repoPath, yearAgo, now, ct);
            authorsAllTask = _gitDataProvider.GetUniqueAuthorCountsUntilAsync(repoPath, now, ct);
            authors7Task = _gitDataProvider.GetUniqueAuthorCountsSinceUntilAsync(repoPath, sevenDaysAgo, now, ct);
            authors30Task = _gitDataProvider.GetUniqueAuthorCountsSinceUntilAsync(repoPath, thirtyDaysAgo, now, ct);
            authors365Task = _gitDataProvider.GetUniqueAuthorCountsSinceUntilAsync(repoPath, yearAgo, now, ct);
            lineTotalsTask = _gitDataProvider.GetLineChangeTotalsUntilAsync(repoPath, now, ct);
            totalLinesTask =
                _gitDataProvider.GetTotalLinesUntilAsync(repoPath, now, ct)
                ?? Task.FromResult(new Dictionary<string, int>(StringComparer.Ordinal));
        }
        else
        {
            commitCountsTask = _gitDataProvider.GetCommitCountsAsync(repoPath, ct);
            firstDatesTask = _gitDataProvider.GetFirstCommitDatesAsync(repoPath, ct);
            lastDatesTask = _gitDataProvider.GetLastCommitDatesAsync(repoPath, ct);
            commits7Task = _gitDataProvider.GetCommitCountsSinceAsync(repoPath, sevenDaysAgo, ct);
            commits30Task = _gitDataProvider.GetCommitCountsSinceAsync(repoPath, thirtyDaysAgo, ct);
            commits365Task = _gitDataProvider.GetCommitCountsSinceAsync(repoPath, yearAgo, ct);
            authorsAllTask = _gitDataProvider.GetUniqueAuthorCountsAsync(repoPath, ct);
            authors7Task = _gitDataProvider.GetUniqueAuthorCountsSinceAsync(repoPath, sevenDaysAgo, ct);
            authors30Task = _gitDataProvider.GetUniqueAuthorCountsSinceAsync(repoPath, thirtyDaysAgo, ct);
            authors365Task = _gitDataProvider.GetUniqueAuthorCountsSinceAsync(repoPath, yearAgo, ct);
            lineTotalsTask = _gitDataProvider.GetLineChangeTotalsAsync(repoPath, ct);
            totalLinesTask =
                _gitDataProvider.GetTotalLinesAsync(repoPath, ct)
                ?? Task.FromResult(new Dictionary<string, int>(StringComparer.Ordinal));
        }

        await Task.WhenAll(
            commitCountsTask, firstDatesTask, lastDatesTask,
            commits7Task, commits30Task, commits365Task,
            authorsAllTask, authors7Task, authors30Task, authors365Task,
            lineTotalsTask, totalLinesTask);

        var commitCounts = commitCountsTask.Result;
        var firstDates = firstDatesTask.Result;
        var lastDates = lastDatesTask.Result;
        var commits7 = commits7Task.Result;
        var commits30 = commits30Task.Result;
        var commits365 = commits365Task.Result;
        var authorsAll = authorsAllTask.Result;
        var authors7 = authors7Task.Result;
        var authors30 = authors30Task.Result;
        var authors365 = authors365Task.Result;
        var lineTotals = lineTotalsTask.Result;
        var totalLinesByFile = totalLinesTask.Result ?? new Dictionary<string, int>(StringComparer.Ordinal);

        progress?.Report(new ChurnProgressUpdate("Processing results", 2, 3));
        // Parse coverage if provided
        Dictionary<string, double>? coverageMap = null;
        if (!string.IsNullOrEmpty(options.CoverageFilePath))
        {
            var rawCoverage = _coverageParser.Parse(options.CoverageFilePath);
            coverageMap = _coverageParser.MapToTrackedFiles(rawCoverage, trackedFiles);
        }

        var results = new List<FileChurnResult>(trackedFiles.Count);

        foreach (var file in trackedFiles)
        {
            var totalCommits = commitCounts.GetValueOrDefault(file, 0);
            if (totalCommits == 0)
                continue;

            firstDates.TryGetValue(file, out var firstDate);
            lastDates.TryGetValue(file, out var lastDate);

            var ageDays = firstDate != default
                ? Math.Max(1, (int)(now - firstDate).TotalDays)
                : 1;

            var ageWeeks = ageDays / 7.0;
            var ageMonths = ageDays / 30.44;
            var ageYears = ageDays / 365.25;

            var changesPerWeek = totalCommits / ageWeeks;
            var changesPerMonth = totalCommits / ageMonths;
            var changesPerYear = totalCommits / ageYears;

            var totalUniqueAuthors = authorsAll.GetValueOrDefault(file, 0);

            double? coveragePercent = null;
            if (coverageMap is not null && coverageMap.TryGetValue(file, out var mappedCoverage))
                coveragePercent = mappedCoverage;

            var churnRiskScore = CalculateChurnRiskScore(
                changesPerWeek, totalUniqueAuthors, coveragePercent);

            var lines = lineTotals.GetValueOrDefault(file);
            results.Add(new FileChurnResult
            {
                FilePath = file,
                TotalCommits = totalCommits,
                LinesAdded = lines.Added,
                LinesRemoved = lines.Removed,
                TotalLines = totalLinesByFile.TryGetValue(file, out var totalLines) ? totalLines : null,
                FirstCommitDate = firstDate != default ? firstDate : null,
                LastCommitDate = lastDate != default ? lastDate : null,
                AgeDays = ageDays,
                ChangesPerWeek = Math.Round(changesPerWeek, 2),
                ChangesPerMonth = Math.Round(changesPerMonth, 2),
                ChangesPerYear = Math.Round(changesPerYear, 2),
                CommitsLast7Days = commits7.GetValueOrDefault(file, 0),
                CommitsLast30Days = commits30.GetValueOrDefault(file, 0),
                CommitsLast365Days = commits365.GetValueOrDefault(file, 0),
                TotalUniqueAuthors = totalUniqueAuthors,
                UniqueAuthorsLast7Days = authors7.GetValueOrDefault(file, 0),
                UniqueAuthorsLast30Days = authors30.GetValueOrDefault(file, 0),
                UniqueAuthorsLast365Days = authors365.GetValueOrDefault(file, 0),
                CoveragePercent = coveragePercent.HasValue ? Math.Round(coveragePercent.Value, 2) : null,
                ChurnRiskScore = churnRiskScore,
            });
        }

        results.Sort((a, b) => b.ChurnRiskScore.CompareTo(a.ChurnRiskScore));
        progress?.Report(new ChurnProgressUpdate("Complete", 3, 3));
        return results;
    }

    /// <summary>
    /// ChurnRiskScore = ChangesPerWeek * TotalUniqueAuthors * (1 - CoveragePercent / 100)
    /// When no coverage data is available, the risk multiplier is 1.0.
    /// </summary>
    public static double CalculateChurnRiskScore(
        double changesPerWeek,
        int totalUniqueAuthors,
        double? coveragePercent)
    {
        var riskMultiplier = coveragePercent.HasValue
            ? 1.0 - (coveragePercent.Value / 100.0)
            : 1.0;

        var score = changesPerWeek * totalUniqueAuthors * riskMultiplier;
        return Math.Round(score, 4);
    }

    private static IReadOnlyList<string> ApplyPathFilters(
        IReadOnlyList<string> files,
        string? includePattern,
        string? excludePattern)
    {
        var include = CreateRegexOrGlob(includePattern);
        var exclude = CreateRegexOrGlob(excludePattern);

        return files
            .Where(file => include is null || include.IsMatch(file))
            .Where(file => exclude is null || !exclude.IsMatch(file))
            .ToList();
    }

    private static Regex? CreateRegexOrGlob(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        if (TryBuildGlobRegex(pattern, out var globRegexPattern))
            return new Regex(globRegexPattern, RegexOptions.CultureInvariant);

        if (TryCreateRegex(pattern, out var regex))
            return regex;

        throw new ArgumentException(
            $"Invalid file path filter '{pattern}'. Use a valid regular expression or wildcard pattern like '*.cs'.",
            nameof(pattern));
    }

    private static bool TryCreateRegex(string pattern, out Regex regex)
    {
        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant);
            return true;
        }
        catch (ArgumentException)
        {
            regex = null!;
            return false;
        }
    }

    private static bool TryBuildGlobRegex(string pattern, out string globRegexPattern)
    {
        var candidate = pattern.Trim();

        // Treat shell-style wildcard patterns as globs first.
        // This avoids regex edge cases like '^*.cs' (valid regex in .NET but unintended for file extension filtering).
        if (!LooksLikeGlobPattern(candidate))
        {
            globRegexPattern = string.Empty;
            return false;
        }

        // Users sometimes prefix wildcard filters with '^' (regex anchor habit), e.g. ^*.cs.
        if (candidate.StartsWith('^'))
            candidate = candidate[1..];

        if (candidate.EndsWith('$'))
            candidate = candidate[..^1];

        globRegexPattern = "^"
            + Regex.Escape(candidate)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
            + "$";

        return true;
    }

    private static bool LooksLikeGlobPattern(string pattern)
    {
        if (pattern.IndexOfAny(['*', '?']) < 0)
            return false;

        var normalized = pattern;
        if (normalized.StartsWith('^'))
            normalized = normalized[1..];
        if (normalized.EndsWith('$'))
            normalized = normalized[..^1];

        if (normalized.Length == 0)
            return false;

        // Regex-specific syntax indicates the user likely intended regex, not wildcard.
        return normalized.IndexOfAny(['[', ']', '(', ')', '{', '}', '|', '+', '\\']) < 0;
    }
}
