using System.Globalization;
using System.Net;
using System.Text;
using GitChurnCalculator.Models;

namespace GitChurnCalculator.Console.Reporting;

/// <summary>
/// Full HTML document with one collapsible section per time point, styled with Bootstrap from a CDN.
/// </summary>
public sealed class HtmlTimeSeriesReportGenerator : ITimeSeriesReportGenerator
{
    private const string BootstrapCssCdn =
        "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css";

    public string Generate(IReadOnlyList<TimeSeriesPoint> points, string subtitle)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(128_000);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  <title>Git churn risk — time series</title>");
        sb.Append("  <link href=\"").Append(BootstrapCssCdn).Append("\" rel=\"stylesheet\"");
        sb.AppendLine(" crossorigin=\"anonymous\" referrerpolicy=\"no-referrer-when-downgrade\" />");
        HtmlSortableTableAssets.AppendStyles(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body class=\"bg-light\">");
        sb.AppendLine("  <div class=\"container-fluid py-4\">");
        sb.AppendLine("    <header class=\"mb-4\">");
        sb.AppendLine("      <h1 class=\"h2\">Git churn risk — time series</h1>");
        sb.Append("      <p class=\"text-muted mb-0\"><code>").Append(WebUtility.HtmlEncode(subtitle)).AppendLine("</code></p>");
        sb.Append("      <p class=\"small text-secondary\">")
            .Append(WebUtility.HtmlEncode(points.Count.ToString(inv)))
            .AppendLine(" time points</p>");
        sb.AppendLine("    </header>");

        foreach (var point in points)
        {
            var label = point.AsOf.ToString("yyyy-MM-dd", inv);
            sb.AppendLine("    <details class=\"mb-3\" open>");
            sb.AppendLine("      <section data-filter-scope>");
            sb.Append("      <summary class=\"fw-semibold fs-5 mb-2\">")
                .Append(WebUtility.HtmlEncode(label))
                .Append(" <span class=\"badge bg-secondary fw-normal\">")
                .Append(WebUtility.HtmlEncode(point.Files.Count.ToString(inv)))
                .AppendLine(" files</span></summary>");
            sb.AppendLine("      <div class=\"report-filters p-3 mb-3\" data-table-filters>");
            sb.AppendLine("        <div class=\"d-flex flex-column flex-lg-row gap-3 align-items-start align-items-lg-end\">");
            sb.AppendLine("          <div>");
            sb.AppendLine("            <label class=\"filter-label d-block mb-1\" for=\"filter-file-" + label + "\">Class / filename contains</label>");
            sb.AppendLine("            <input id=\"filter-file-" + label + "\" type=\"search\" class=\"form-control form-control-sm\" data-filter-file placeholder=\"e.g. BillingService\" />");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"filter-comparison\">");
            sb.AppendLine("            <label class=\"filter-label d-block mb-1\" for=\"filter-coverage-op-" + label + "\">Coverage (%)</label>");
            sb.AppendLine("            <div class=\"input-group input-group-sm\">");
            sb.AppendLine("              <select id=\"filter-coverage-op-" + label + "\" class=\"form-select\" data-filter-coverage-op>");
            sb.AppendLine("                <option value=\"\">No filter</option>");
            sb.AppendLine("                <option value=\"gt\">Greater than</option>");
            sb.AppendLine("                <option value=\"lt\">Less than</option>");
            sb.AppendLine("              </select>");
            sb.AppendLine("              <input id=\"filter-coverage-value-" + label + "\" type=\"number\" step=\"0.01\" min=\"0\" max=\"100\" class=\"form-control\" data-filter-coverage-value placeholder=\"e.g. 70\" />");
            sb.AppendLine("            </div>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"filter-comparison\">");
            sb.AppendLine("            <label class=\"filter-label d-block mb-1\" for=\"filter-churn-op-" + label + "\">Churn risk</label>");
            sb.AppendLine("            <div class=\"input-group input-group-sm\">");
            sb.AppendLine("              <select id=\"filter-churn-op-" + label + "\" class=\"form-select\" data-filter-churn-op>");
            sb.AppendLine("                <option value=\"\">No filter</option>");
            sb.AppendLine("                <option value=\"gt\">Greater than</option>");
            sb.AppendLine("                <option value=\"lt\">Less than</option>");
            sb.AppendLine("              </select>");
            sb.AppendLine("              <input id=\"filter-churn-value-" + label + "\" type=\"number\" step=\"0.0001\" min=\"0\" class=\"form-control\" data-filter-churn-value placeholder=\"e.g. 4.0\" />");
            sb.AppendLine("            </div>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"filter-actions\">");
            sb.AppendLine("            <label class=\"filter-label d-block mb-1\" aria-hidden=\"true\">Actions</label>");
            sb.AppendLine("            <button type=\"button\" class=\"btn btn-outline-secondary btn-sm w-100\" data-filter-reset>Reset filters</button>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div class=\"table-responsive shadow-sm bg-white rounded\">");
            sb.AppendLine("        <table class=\"table table-striped table-hover table-sm table-bordered align-middle mb-0\" data-sortable=\"true\">");
            sb.AppendLine("          <thead class=\"table-dark\">");
            sb.AppendLine("            <tr>");
            AppendTh(sb, "File", "text");
            AppendTh(sb, "Churn risk", "number");
            AppendTh(sb, "Changes / wk", "number");
            AppendTh(sb, "Authors", "number");
            AppendTh(sb, "Coverage %", "number");
            AppendTh(sb, "Total commits", "number");
            AppendTh(sb, "Lines added", "number");
            AppendTh(sb, "Lines removed", "number");
            AppendTh(sb, "First commit", "date");
            AppendTh(sb, "Last commit", "date");
            AppendTh(sb, "Age (days)", "number");
            AppendTh(sb, "Commits 7d", "number");
            AppendTh(sb, "Commits 30d", "number");
            AppendTh(sb, "Commits 365d", "number");
            AppendTh(sb, "Authors 7d", "number");
            AppendTh(sb, "Authors 30d", "number");
            AppendTh(sb, "Authors 365d", "number");
            AppendTh(sb, "Changes / mo", "number");
            AppendTh(sb, "Changes / yr", "number");
            sb.AppendLine("            </tr>");
            sb.AppendLine("          </thead>");
            sb.AppendLine("          <tbody>");

            foreach (var r in point.Files)
            {
                sb.AppendLine("            <tr>");
                AppendTdCode(sb, r.FilePath);
                AppendTd(sb, r.ChurnRiskScore.ToString("F4", inv));
                AppendTd(sb, r.ChangesPerWeek.ToString("F2", inv));
                AppendTd(sb, r.TotalUniqueAuthors.ToString(inv));
                AppendTd(sb, r.CoveragePercent?.ToString("F2", inv) ?? "—");
                AppendTd(sb, r.TotalCommits.ToString(inv));
                AppendTd(sb, r.LinesAdded.ToString(inv));
                AppendTd(sb, r.LinesRemoved.ToString(inv));
                AppendTd(sb, r.FirstCommitDate?.ToString("yyyy-MM-dd", inv) ?? "");
                AppendTd(sb, r.LastCommitDate?.ToString("yyyy-MM-dd", inv) ?? "");
                AppendTd(sb, r.AgeDays.ToString(inv));
                AppendTd(sb, r.CommitsLast7Days.ToString(inv));
                AppendTd(sb, r.CommitsLast30Days.ToString(inv));
                AppendTd(sb, r.CommitsLast365Days.ToString(inv));
                AppendTd(sb, r.UniqueAuthorsLast7Days.ToString(inv));
                AppendTd(sb, r.UniqueAuthorsLast30Days.ToString(inv));
                AppendTd(sb, r.UniqueAuthorsLast365Days.ToString(inv));
                AppendTd(sb, r.ChangesPerMonth.ToString("F2", inv));
                AppendTd(sb, r.ChangesPerYear.ToString("F2", inv));
                sb.AppendLine("            </tr>");
            }

            sb.AppendLine("          </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      </section>");
            sb.AppendLine("    </details>");
        }

        sb.AppendLine("    <footer class=\"mt-3 small text-secondary\">Generated by GitChurnCalculator</footer>");
        sb.AppendLine("  </div>");
        HtmlSortableTableAssets.AppendScript(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendTh(StringBuilder sb, string text, string sortType)
    {
        sb.Append("              <th scope=\"col\" class=\"text-nowrap\" data-sort-type=\"")
            .Append(sortType)
            .Append("\"><button type=\"button\" class=\"btn btn-link btn-sm p-0 text-decoration-none text-nowrap\">")
            .Append(WebUtility.HtmlEncode(text))
            .AppendLine("</button></th>");
    }

    private static void AppendTd(StringBuilder sb, string text)
    {
        sb.Append("              <td class=\"text-nowrap\">")
            .Append(WebUtility.HtmlEncode(text))
            .AppendLine("</td>");
    }

    private static void AppendTdCode(StringBuilder sb, string path)
    {
        sb.Append("              <td><code class=\"small\">")
            .Append(WebUtility.HtmlEncode(path))
            .AppendLine("</code></td>");
    }
}
