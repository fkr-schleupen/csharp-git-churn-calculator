using GitChurnCalculator.Models;

namespace GitChurnCalculator.Services;

public interface IGitDataProvider
{
    /// <summary>
    /// Returns all tracked file paths in the repository.
    /// </summary>
    Task<IReadOnlyList<string>> GetTrackedFilesAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file commit count (all time).
    /// </summary>
    Task<Dictionary<string, int>> GetCommitCountsAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file commit count for all commits up to and including the given date.
    /// </summary>
    Task<Dictionary<string, int>> GetCommitCountsUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file commit count since the given date.
    /// </summary>
    Task<Dictionary<string, int>> GetCommitCountsSinceAsync(string repoPath, DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file commit count for commits in the half-open interval (since, until].
    /// </summary>
    Task<Dictionary<string, int>> GetCommitCountsSinceUntilAsync(string repoPath, DateTime since, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns the first (oldest) commit date per file.
    /// </summary>
    Task<Dictionary<string, DateTime>> GetFirstCommitDatesAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns the first (oldest) commit date per file for commits up to and including the given date.
    /// </summary>
    Task<Dictionary<string, DateTime>> GetFirstCommitDatesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns the last (newest) commit date per file.
    /// </summary>
    Task<Dictionary<string, DateTime>> GetLastCommitDatesAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns the last (newest) commit date per file for commits up to and including the given date.
    /// </summary>
    Task<Dictionary<string, DateTime>> GetLastCommitDatesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file unique author count (all time).
    /// </summary>
    Task<Dictionary<string, int>> GetUniqueAuthorCountsAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file unique author count for commits up to and including the given date.
    /// </summary>
    Task<Dictionary<string, int>> GetUniqueAuthorCountsUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file unique author count since the given date.
    /// </summary>
    Task<Dictionary<string, int>> GetUniqueAuthorCountsSinceAsync(string repoPath, DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file unique author count for commits in the half-open interval (since, until].
    /// </summary>
    Task<Dictionary<string, int>> GetUniqueAuthorCountsSinceUntilAsync(string repoPath, DateTime since, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file cumulative insertions/deletions (<c>--numstat</c>) across full history.
    /// </summary>
    Task<Dictionary<string, LineChangeTotals>> GetLineChangeTotalsAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file cumulative insertions/deletions (<c>--numstat</c>) for commits on or before <paramref name="until"/>.
    /// </summary>
    Task<Dictionary<string, LineChangeTotals>> GetLineChangeTotalsUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns per-file total line counts at repository HEAD.
    /// </summary>
    Task<Dictionary<string, int>> GetTotalLinesAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns total line counts at repository HEAD for the provided git-relative paths.
    /// </summary>
    Task<Dictionary<string, int>> GetTotalLinesForFilesAsync(
        string repoPath,
        IReadOnlyList<string> gitRelativePaths,
        CancellationToken ct = default);

    /// <summary>
    /// Returns per-file total line counts at the latest commit on or before <paramref name="until"/>.
    /// </summary>
    Task<Dictionary<string, int>> GetTotalLinesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Returns total line counts at the latest commit on or before <paramref name="until"/>
    /// for the provided git-relative paths.
    /// </summary>
    Task<Dictionary<string, int>> GetTotalLinesForFilesUntilAsync(
        string repoPath,
        IReadOnlyList<string> gitRelativePaths,
        DateTime until,
        CancellationToken ct = default);
}
