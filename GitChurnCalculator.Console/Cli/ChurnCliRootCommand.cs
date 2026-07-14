using System.CommandLine;
using System.CommandLine.Invocation;
using GitChurnCalculator.Console.Reporting;

namespace GitChurnCalculator.Console.Cli;

public static class ChurnCliRootCommand
{
    public static RootCommand Create(ChurnAnalysisApp app)
    {
        var repoArgument = new Argument<DirectoryInfo>(
            "repo-path",
            "Path to the git repository to analyze");

        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "csv",
            description: $"Output format: {ChurnReportGeneratorFactory.SupportedFormatsList}. Time series also supports graph.");

        var coverageOption = new Option<FileInfo?>(
            "--coverage",
            description: "Path to a coverage XML file (Cobertura or VS coverage format, auto-detected)");

        var outputOption = new Option<FileInfo?>(
            "--output",
            description: "Output file path (defaults to stdout)");

        var includeOption = new Option<string?>(
            "--include",
            description: "Only include repo-relative file paths matching this regex or wildcard pattern (e.g. *.cs).");

        var excludeOption = new Option<string?>(
            "--exclude",
            description: "Exclude repo-relative file paths matching this regex or wildcard pattern (e.g. */Generated/*).");

        var seriesOption = new Option<string?>(
            "--series",
            description: "Produce a time series by stepping in 'week' or 'month' chunks. Requires --from.");

        var fromOption = new Option<string?>(
            "--from",
            description: "Start date for time series (yyyy-MM-dd). Required when --series is used.");

        var toOption = new Option<string?>(
            "--to",
            description: "End date for time series (yyyy-MM-dd). Defaults to today when --series is used.");

        var rootCommand = new RootCommand("Git Churn Risk Calculator - analyzes file churn, author spread, and optional test coverage")
        {
            repoArgument,
            formatOption,
            coverageOption,
            outputOption,
            includeOption,
            excludeOption,
            seriesOption,
            fromOption,
            toOption,
        };

        rootCommand.SetHandler(async context =>
        {
            var repo = context.ParseResult.GetValueForArgument(repoArgument);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "csv";
            var coverage = context.ParseResult.GetValueForOption(coverageOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var include = context.ParseResult.GetValueForOption(includeOption);
            var exclude = context.ParseResult.GetValueForOption(excludeOption);
            var series = context.ParseResult.GetValueForOption(seriesOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var to = context.ParseResult.GetValueForOption(toOption);

            await app.HandleAsync(repo, format, coverage, output, include, exclude, series, from, to);
        });

        return rootCommand;
    }
}
