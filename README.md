# Git Churn Calculator

It calculates a **Churn Risk Score** for every file in a git repository by combining change frequency, author spread, and optional test coverage data.

## Documentation

- **[How churn is calculated](docs/churn-calculation.md)** — formulas, git-derived inputs, and Cobertura-to-repo file mapping.
- **[Build, test, and publish](docs/build.md)** — solution layout, prerequisites, `dotnet` / `make.ps1` workflows, single-file publish, and coverage for this repo.

## Usage

```bash
dotnet run --project GitChurnCalculator.Console -- <repo-path> [options]
```

### Options

| Option | Default | Description |
|--------|---------|-------------|
| `<repo-path>` | *(required)* | Path to the git repository to analyze |
| `--format <csv\|json\|html\|graph>` | `csv` | Output format (`html` = Bootstrap-styled table page, `graph` = D3 time-series chart) |
| `--coverage <path>` | *(none)* | Path to a Cobertura XML coverage file |
| `--output <path>` | stdout | Write output to a file instead of stdout |
| `--include <pattern>` | *(none)* | Only include repo-relative file paths matching this regular expression or wildcard pattern (for example `*.cs`) |
| `--exclude <pattern>` | *(none)* | Exclude repo-relative file paths matching this regular expression or wildcard pattern |
| `--series <week\|month>` | *(none)* | Produce a time series by stepping in week or month chunks. Requires `--from`. |
| `--from <yyyy-MM-dd>` | *(none)* | Start date for time series (inclusive). Required when `--series` is used. |
| `--to <yyyy-MM-dd>` | today | End date for time series (inclusive). Defaults to today when `--series` is used. |

### Examples

Analyze a repository and print CSV to stdout:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo
```

Output as JSON to a file:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo --format json --output churn.json
```

Include Cobertura coverage data:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo --coverage coverage.cobertura.xml
```

HTML table report (Bootstrap via CDN; open the file in a browser):

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo --format html --output churn.html
```

Limit analysis to selected paths:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo \
  --include "^(src|tests)/.*\\.cs$" --exclude "(bin|obj|coverage-report)/"
```

Or use wildcard patterns:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo \
  --include "*.cs" --exclude "*/Generated/*"
```

### Time series

Produce a weekly time series for a date range and save as JSON:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo \
  --series week --from 2024-01-01 --to 2024-03-01 \
  --format json --output series.json
```

Monthly time series as HTML (one collapsible section per month):

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo \
  --series month --from 2024-01-01 \
  --format html --output series.html
```

D3 multi-line graph with one line per top-risk file:

```bash
dotnet run --project GitChurnCalculator.Console -- /path/to/repo \
  --series week --from 2024-01-01 \
  --format graph --output churn-graph.html
```

Time series output includes all the same per-file columns as the single-snapshot report, with an additional `AsOf` column (CSV) or `asOf` field (JSON) identifying the bucket end date. Each time point's metrics — including rolling windows like `CommitsLast7Days` — are calculated relative to that point's date, not the current date. The `--format` flag selects the format for both single-snapshot and time series modes.

The `graph` format draws only the top 50 file series, ranked by each file's highest churn risk score across the selected time range. Hovering a line or file label shows the file path, nearest date, churn risk score, and change frequency.

## Output Fields

| Column | Description |
|--------|-------------|
| File | Tracked file path |
| TotalCommits | Total commits touching this file |
| FirstCommitDate | Date of the first commit |
| LastCommitDate | Date of the most recent commit |
| AgeDays | Days since first commit |
| ChangesPerWeek | Average commits per week over the file's lifetime |
| ChangesPerMonth | Average commits per month |
| ChangesPerYear | Average commits per year |
| CommitsLast7Days | Commits in the last 7 days |
| CommitsLast30Days | Commits in the last 30 days |
| CommitsLast365Days | Commits in the last 365 days |
| TotalUniqueAuthors | Distinct authors (by email) who ever touched the file |
| UniqueAuthorsLast7Days | Distinct authors in the last 7 days |
| UniqueAuthorsLast30Days | Distinct authors in the last 30 days |
| UniqueAuthorsLast365Days | Distinct authors in the last 365 days |
| CoveragePercent | Line coverage from Cobertura XML (empty if not provided) |
| ChurnRiskScore | Computed risk score, sorted descending |

Results are always sorted by **ChurnRiskScore descending** so the highest-risk files appear first.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
