using GitChurnCalculator.Models;

namespace GitChurnCalculator.Services;

public interface IChurnCalculator
{
    Task<IReadOnlyList<FileChurnResult>> AnalyzeAsync(
        ChurnAnalysisOptions options,
        IProgress<ChurnProgressUpdate>? progress = null,
        CancellationToken ct = default);
}
