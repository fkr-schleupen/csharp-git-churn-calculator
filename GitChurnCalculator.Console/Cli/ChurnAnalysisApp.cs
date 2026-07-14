using GitChurnCalculator.Console.Reporting;
using GitChurnCalculator.Models;
using GitChurnCalculator.Services;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace GitChurnCalculator.Console.Cli;

public sealed class ChurnAnalysisApp
{
    private readonly IChurnCalculator _calculator;

    public ChurnAnalysisApp()
        : this(new ChurnCalculator(new GitProcessDataProvider(), new AutoDetectCoverageParser()))
    {
    }

    public ChurnAnalysisApp(IChurnCalculator calculator) => _calculator = calculator;

    public async Task HandleAsync(
        DirectoryInfo repo,
        string format,
        FileInfo? coverage,
        FileInfo? output,
        string? include,
        string? exclude,
        string? series,
        string? from,
        string? to)
    {
        if (!repo.Exists)
        {
            Fail($"Error: Repository path '{repo.FullName}' does not exist.");
            return;
        }

        if (coverage is not null && !coverage.Exists)
        {
            Fail($"Error: Coverage file '{coverage.FullName}' does not exist.");
            return;
        }

        if (!ValidateFilterPattern(include, "--include") || !ValidateFilterPattern(exclude, "--exclude"))
            return;

        LogAnalysisStart(repo, coverage);

        if (series is null)
        {
            await RunSnapshotAsync(repo, format, coverage, output, include, exclude);
            return;
        }

        await RunTimeSeriesAsync(repo, format, coverage, output, include, exclude, series, from, to);
    }

    private async Task RunSnapshotAsync(
        DirectoryInfo repo,
        string format,
        FileInfo? coverage,
        FileInfo? output,
        string? include,
        string? exclude)
    {
        if (!ChurnReportGeneratorFactory.TryGet(format, out var generator) || generator is null)
        {
            Fail($"Error: Unsupported format '{format}'. Use {ChurnReportGeneratorFactory.SupportedFormatsList}.");
            return;
        }

        var options = new ChurnAnalysisOptions
        {
            RepositoryPath = repo.FullName,
            CoverageFilePath = coverage?.FullName,
            IncludePattern = include,
            ExcludePattern = exclude,
        };

        IReadOnlyList<FileChurnResult> results;
        var progressConsole = CreateProgressConsole();
        if (progressConsole is not null)
        {
            results = await progressConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("Analyzing repository", maxValue: 3);
                    var progress = new Progress<ChurnProgressUpdate>(update =>
                    {
                        task.Description = update.Description;
                        task.Value = update.Completed;
                        if (update.Total > 0)
                            task.MaxValue = update.Total;
                    });
                    return await _calculator.AnalyzeAsync(options, progress);
                });
        }
        else
        {
            results = await _calculator.AnalyzeAsync(options);
        }

        global::System.Console.Error.WriteLine($"Found {results.Count} files with commit history.");

        if (coverage is not null)
        {
            var matchedCoverageCount = results.Count(r => r.CoveragePercent.HasValue);
            var nonZeroCoverageCount = results.Count(r => r.CoveragePercent is > 0);
            global::System.Console.Error.WriteLine(
                $"Coverage mapped to {matchedCoverageCount} files ({nonZeroCoverageCount} with non-zero coverage).");
        }

        var text = generator.Generate(results, repo.FullName);
        await ChurnOutputWriter.WriteAsync(output, text);
    }

    private async Task RunTimeSeriesAsync(
        DirectoryInfo repo,
        string format,
        FileInfo? coverage,
        FileInfo? output,
        string? include,
        string? exclude,
        string series,
        string? from,
        string? to)
    {
        if (!TimeSeriesReportGeneratorFactory.TryGet(format, out var tsGenerator) || tsGenerator is null)
        {
            Fail($"Error: Unsupported format '{format}'. Use {TimeSeriesReportGeneratorFactory.SupportedFormatsList}.");
            return;
        }

        if (!TimeSeriesArguments.TryValidate(series, from, to, out var validationError, out var parsed))
        {
            Fail(validationError!);
            return;
        }

        var bucketEnds = TimeSeriesBucketEndCalculator.BuildEnds(parsed!.From, parsed.To, parsed.GranularityLower);
        global::System.Console.Error.WriteLine(
            $"Time series mode: {parsed.GranularityLower} chunks from {parsed.From:yyyy-MM-dd} to {parsed.To:yyyy-MM-dd} ({bucketEnds.Count} points).");

        List<TimeSeriesPoint> points;
        var progressConsole = CreateProgressConsole();
        if (progressConsole is not null)
        {
            points = await progressConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var overallTask = ctx.AddTask("Overall progress", maxValue: bucketEnds.Count);
                    var bucketTask = ctx.AddTask("Analyzing...", maxValue: 3);

                    Func<DateTime, IProgress<ChurnProgressUpdate>> progressFactory = asOf =>
                        new Progress<ChurnProgressUpdate>(update =>
                        {
                            bucketTask.Description = $"[{asOf:yyyy-MM-dd}] {update.Description}";
                            bucketTask.Value = update.Completed;
                            if (update.Total > 0)
                                bucketTask.MaxValue = update.Total;
                            if (update.Completed == update.Total && update.Total > 0)
                            {
                                overallTask.Increment(1);
                                bucketTask.Value = 0;
                            }
                        });

                    return await CollectTimeSeriesPointsAsync(repo, coverage, include, exclude, bucketEnds, progressFactory);
                });
        }
        else
        {
            points = await CollectTimeSeriesPointsAsync(repo, coverage, include, exclude, bucketEnds);
        }

        global::System.Console.Error.WriteLine($"Found data across {points.Count} time points.");

        var outputText = tsGenerator.Generate(points, repo.FullName);
        await ChurnOutputWriter.WriteAsync(output, outputText);
    }

    private async Task<List<TimeSeriesPoint>> CollectTimeSeriesPointsAsync(
        DirectoryInfo repo,
        FileInfo? coverage,
        string? include,
        string? exclude,
        IReadOnlyList<DateTime> bucketEnds,
        Func<DateTime, IProgress<ChurnProgressUpdate>>? progressFactory = null)
    {
        var points = new List<TimeSeriesPoint>(bucketEnds.Count);
        foreach (var asOf in bucketEnds)
        {
            var options = new ChurnAnalysisOptions
            {
                RepositoryPath = repo.FullName,
                CoverageFilePath = coverage?.FullName,
                IncludePattern = include,
                ExcludePattern = exclude,
                AsOf = asOf,
            };
            var results = await _calculator.AnalyzeAsync(options, progressFactory?.Invoke(asOf));
            points.Add(new TimeSeriesPoint { AsOf = asOf, Files = results });
        }

        return points;
    }

    private static void LogAnalysisStart(DirectoryInfo repo, FileInfo? coverage)
    {
        global::System.Console.Error.WriteLine($"Analyzing repository: {repo.FullName}");
        if (coverage is not null)
            global::System.Console.Error.WriteLine($"Using coverage file: {coverage.FullName}");
    }

    private static IAnsiConsole? CreateProgressConsole() =>
        global::System.Console.IsErrorRedirected
            ? null
            : AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(global::System.Console.Error),
            });

    private static void Fail(string message)
    {
        global::System.Console.Error.WriteLine(message);
        Environment.ExitCode = 1;
    }

    private static bool ValidateFilterPattern(string? pattern, string optionName)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        if (LooksLikeGlobPattern(pattern.Trim()))
            return true;

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant);
            return true;
        }
        catch (ArgumentException ex)
        {
            Fail($"Error: Invalid {optionName} filter '{pattern}': {ex.Message}. Use a valid regex or wildcard (e.g. *.cs).");
            return false;
        }
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

        return normalized.IndexOfAny(['[', ']', '(', ')', '{', '}', '|', '+', '\\']) < 0;
    }
}
