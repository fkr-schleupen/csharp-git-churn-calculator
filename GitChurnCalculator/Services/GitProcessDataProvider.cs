using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Buffers;
using GitChurnCalculator.Models;

namespace GitChurnCalculator.Services;

public sealed class GitProcessDataProvider : IGitDataProvider
{
    public async Task<IReadOnlyList<string>> GetTrackedFilesAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "ls-files", ct);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    public async Task<Dictionary<string, int>> GetCommitCountsAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "log --pretty=format: --name-only", ct);
        return ParseFileCountsFromNameOnlyLog(output);
    }

    public async Task<Dictionary<string, int>> GetCommitCountsUntilAsync(string repoPath, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, $"log{UntilClause(until)} --pretty=format: --name-only", ct);
        return ParseFileCountsFromNameOnlyLog(output);
    }

    public async Task<Dictionary<string, int>> GetCommitCountsSinceAsync(string repoPath, DateTime since, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, $"log{SinceClause(since)} --pretty=format: --name-only", ct);
        return ParseFileCountsFromNameOnlyLog(output);
    }

    public async Task<Dictionary<string, int>> GetCommitCountsSinceUntilAsync(string repoPath, DateTime since, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(
            repoPath,
            $"log{SinceClause(since)}{UntilClause(until)} --pretty=format: --name-only",
            ct);
        return ParseFileCountsFromNameOnlyLog(output);
    }

    public async Task<Dictionary<string, DateTime>> GetFirstCommitDatesAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "log --reverse --format=%ai --name-only", ct);
        return ParseFirstDatePerFile(output);
    }

    public async Task<Dictionary<string, DateTime>> GetFirstCommitDatesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, $"log{UntilClause(until)} --reverse --format=%ai --name-only", ct);
        return ParseFirstDatePerFile(output);
    }

    public async Task<Dictionary<string, DateTime>> GetLastCommitDatesAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "log --format=%ai --name-only", ct);
        return ParseFirstDatePerFile(output);
    }

    public async Task<Dictionary<string, DateTime>> GetLastCommitDatesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, $"log{UntilClause(until)} --format=%ai --name-only", ct);
        return ParseFirstDatePerFile(output);
    }

    public async Task<Dictionary<string, int>> GetUniqueAuthorCountsAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "log --pretty=format:\"COMMIT %ae\" --name-only", ct);
        return ParseUniqueAuthorCounts(output);
    }

    public async Task<Dictionary<string, int>> GetUniqueAuthorCountsUntilAsync(string repoPath, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(
            repoPath,
            $"log{UntilClause(until)} --pretty=format:\"COMMIT %ae\" --name-only",
            ct);
        return ParseUniqueAuthorCounts(output);
    }

    public async Task<Dictionary<string, int>> GetUniqueAuthorCountsSinceAsync(string repoPath, DateTime since, CancellationToken ct = default)
    {
        var output = await RunGitAsync(
            repoPath,
            $"log{SinceClause(since)} --pretty=format:\"COMMIT %ae\" --name-only",
            ct);
        return ParseUniqueAuthorCounts(output);
    }

    public async Task<Dictionary<string, int>> GetUniqueAuthorCountsSinceUntilAsync(string repoPath, DateTime since, DateTime until, CancellationToken ct = default)
    {
        var output = await RunGitAsync(
            repoPath,
            $"log{SinceClause(since)}{UntilClause(until)} --pretty=format:\"COMMIT %ae\" --name-only",
            ct);
        return ParseUniqueAuthorCounts(output);
    }

    public static Dictionary<string, int> ParseFileCountsFromNameOnlyLog(string output)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            counts.TryGetValue(trimmed, out var current);
            counts[trimmed] = current + 1;
        }
        return counts;
    }

    /// <summary>
    /// Parses git log output with date lines followed by file name lines.
    /// Returns the first date encountered for each path in traversal order:
    /// with <c>--reverse</c> that is the oldest commit touching the file; without <c>--reverse</c>
    /// (newest-first log) it is the most recent commit touching the file.
    /// </summary>
    public static Dictionary<string, DateTime> ParseFirstDatePerFile(string output)
    {
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        DateTime? currentDate = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            if (TryParseGitDate(trimmed, out var date))
            {
                currentDate = date;
                continue;
            }

            if (currentDate.HasValue && !dates.ContainsKey(trimmed))
            {
                dates[trimmed] = currentDate.Value;
            }
        }
        return dates;
    }

    public static Dictionary<string, int> ParseUniqueAuthorCounts(string output)
    {
        var fileAuthors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        string? currentAuthor = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim().Trim('"');
            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith("COMMIT ", StringComparison.Ordinal))
            {
                currentAuthor = trimmed["COMMIT ".Length..];
                continue;
            }

            if (currentAuthor is not null && trimmed.Length > 0)
            {
                if (!fileAuthors.TryGetValue(trimmed, out var authors))
                {
                    authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    fileAuthors[trimmed] = authors;
                }
                authors.Add(currentAuthor);
            }
        }

        return fileAuthors.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);
    }

    public async Task<Dictionary<string, LineChangeTotals>> GetLineChangeTotalsAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, NumstatLogArgs(""), ct);
        return ParseLineChangeTotalsFromNumstatLog(output);
    }

    public async Task<Dictionary<string, LineChangeTotals>> GetLineChangeTotalsUntilAsync(
        string repoPath,
        DateTime until,
        CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, NumstatLogArgs(UntilClause(until)), ct);
        return ParseLineChangeTotalsFromNumstatLog(output);
    }

    public Task<Dictionary<string, int>> GetTotalLinesAsync(string repoPath, CancellationToken ct = default) =>
        GetTotalLinesAtRevisionAsync(repoPath, "HEAD", null, ct);

    public Task<Dictionary<string, int>> GetTotalLinesForFilesAsync(
        string repoPath,
        IReadOnlyList<string> gitRelativePaths,
        CancellationToken ct = default) =>
        GetTotalLinesAtRevisionAsync(repoPath, "HEAD", gitRelativePaths, ct);

    public async Task<Dictionary<string, int>> GetTotalLinesUntilAsync(string repoPath, DateTime until, CancellationToken ct = default)
    {
        var rev = (await RunGitAsync(
            repoPath,
            $"rev-list -1 --before=\"{FormatLogDate(until)} 23:59:59\" HEAD",
            ct)).Trim();

        if (rev.Length == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        return await GetTotalLinesAtRevisionAsync(repoPath, rev, null, ct);
    }

    public async Task<Dictionary<string, int>> GetTotalLinesForFilesUntilAsync(
        string repoPath,
        IReadOnlyList<string> gitRelativePaths,
        DateTime until,
        CancellationToken ct = default)
    {
        var rev = (await RunGitAsync(
            repoPath,
            $"rev-list -1 --before=\"{FormatLogDate(until)} 23:59:59\" HEAD",
            ct)).Trim();

        if (rev.Length == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        return await GetTotalLinesAtRevisionAsync(repoPath, rev, gitRelativePaths, ct);
    }

    /// <summary>
    /// Parses <c>git log --numstat</c> output: lines are <c>added\\tremoved\\tpath</c>. Binary rows use <c>-</c> for counts.
    /// </summary>
    public static Dictionary<string, LineChangeTotals> ParseLineChangeTotalsFromNumstatLog(string output)
    {
        var totals = new Dictionary<string, LineChangeTotals>(StringComparer.Ordinal);

        foreach (var raw in output.Split('\n'))
        {
            if (raw.Length == 0)
                continue;

            var parts = raw.Split('\t', 3, StringSplitOptions.None);
            if (parts.Length != 3)
                continue;

            var path = parts[2].Trim();
            if (path.Length == 0)
                continue;

            var addedCol = parts[0].Trim();
            var removedCol = parts[1].Trim();

            var added = int.TryParse(addedCol, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;
            var removed = int.TryParse(removedCol, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;

            totals.TryGetValue(path, out var cur);
            totals[path] = new LineChangeTotals(cur.Added + added, cur.Removed + removed);
        }

        return totals;
    }

    private static string NumstatLogArgs(string dateClause) =>
        $"-c core.quotepath=false log{dateClause} --pretty=format: --numstat";

    private static async Task<Dictionary<string, int>> GetTotalLinesAtRevisionAsync(
        string repoPath,
        string revision,
        IReadOnlyList<string>? candidatePaths,
        CancellationToken ct)
    {
        List<string> paths;
        if (candidatePaths is not null)
        {
            paths = candidatePaths
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => p.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        else
        {
            var filesOutput = await RunGitAsync(
                repoPath,
                $"-c core.quotepath=false ls-tree -r --name-only --full-tree {revision}",
                ct);

            paths = filesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .ToList();
        }

        if (paths.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var objectSpecs = paths.Select(path => $"{revision}:{path}");
        return await ReadLineCountsWithBatchCatFileAsync(repoPath, objectSpecs, paths, ct);
    }

    private static async Task<Dictionary<string, int>> ReadLineCountsWithBatchCatFileAsync(
        string repoPath,
        IEnumerable<string> objectSpecs,
        IReadOnlyList<string> paths,
        CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "cat-file --batch",
            WorkingDirectory = repoPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        process.Start();

        var errorTask = process.StandardError.ReadToEndAsync(ct);

        var specs = objectSpecs.ToList();
        if (specs.Count != paths.Count)
            throw new InvalidOperationException("Mismatch between object specs and path count for cat-file batching.");

        await using var stdin = process.StandardInput;
        stdin.AutoFlush = true;

        var stdout = process.StandardOutput.BaseStream;
        var result = new Dictionary<string, int>(paths.Count, StringComparer.Ordinal);

        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            await stdin.WriteLineAsync(specs[i].AsMemory(), ct);

            var header = await ReadAsciiLineAsync(stdout, ct)
                ?? throw new InvalidOperationException("Unexpected end of git cat-file output.");

            if (header.EndsWith(" missing", StringComparison.Ordinal))
            {
                result[path] = 0;
                continue;
            }

            var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            {
                throw new InvalidOperationException($"Unexpected git cat-file header: '{header}'.");
            }

            result[path] = await CountLinesFromObjectPayloadAsync(stdout, size, ct);
        }

        stdin.Close();

        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"git cat-file --batch failed (exit code {process.ExitCode}): {error}");
        }

        return result;
    }

    private static async Task<string?> ReadAsciiLineAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), ct);
            if (read == 0)
            {
                if (buffer.Length == 0)
                    return null;
                break;
            }

            if (one[0] == (byte)'\n')
                break;

            buffer.WriteByte(one[0]);
        }

        var bytes = buffer.ToArray();
        if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
            Array.Resize(ref bytes, bytes.Length - 1);

        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task<int> CountLinesFromObjectPayloadAsync(Stream stream, int size, CancellationToken ct)
    {
        if (size < 0)
            throw new InvalidOperationException($"Invalid object size '{size}' from git cat-file output.");

        var remaining = size;
        var newlineCount = 0;
        byte lastByte = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(Math.Min(64 * 1024, Math.Max(size, 1)));
        try
        {
            while (remaining > 0)
            {
                var toRead = Math.Min(buffer.Length, remaining);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                if (read == 0)
                    throw new InvalidOperationException("Unexpected end of stream while reading git object content.");

                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == (byte)'\n')
                        newlineCount++;
                }

                lastByte = buffer[read - 1];
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var delimiter = new byte[1];
        var delimiterRead = await stream.ReadAsync(delimiter.AsMemory(0, 1), ct);
        if (delimiterRead == 0)
            throw new InvalidOperationException("Unexpected end of stream after git object content.");

        if (size == 0)
            return 0;

        return newlineCount + (lastByte == (byte)'\n' ? 0 : 1);
    }

    private static string FormatLogDate(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string UntilClause(DateTime until) => $" --until=\"{FormatLogDate(until)}\"";

    private static string SinceClause(DateTime since) => $" --since=\"{FormatLogDate(since)}\"";

    private static bool TryParseGitDate(string value, out DateTime result)
    {
        // Git date format: 2024-01-15 10:30:45 +0100
        if (value.Length >= 10 && char.IsDigit(value[0]) && value[4] == '-')
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
        result = default;
        return false;
    }

    private static async Task<string> RunGitAsync(string repoPath, string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"git {arguments} failed (exit code {process.ExitCode}): {error}");
        }

        return await outputTask;
    }
}
