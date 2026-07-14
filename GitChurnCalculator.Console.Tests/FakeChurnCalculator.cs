using GitChurnCalculator.Models;
using GitChurnCalculator.Services;

namespace GitChurnCalculator.Console.Tests;

internal sealed class FakeChurnCalculator : IChurnCalculator
{
    public List<FileChurnResult> Results { get; } = new();
    public List<ChurnAnalysisOptions> Calls { get; } = new();

    public Task<IReadOnlyList<FileChurnResult>> AnalyzeAsync(
        ChurnAnalysisOptions options,
        IProgress<ChurnProgressUpdate>? progress = null,
        CancellationToken ct = default)
    {
        Calls.Add(options);
        return Task.FromResult<IReadOnlyList<FileChurnResult>>(Results);
    }
}
