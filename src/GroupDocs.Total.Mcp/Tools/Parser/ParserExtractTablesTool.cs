using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Parser.Data;
using GroupDocs.Parser.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Parser;

[McpServerToolType]
public static class ParserExtractTablesTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Parser] Extracts tables from a document and returns them as Markdown (default) or JSON. " +
        "Markdown tables render instantly in VS Code, GitHub, and AI chat — use this format to visually review table data right in the response. " +
        "Use format='json' to get a structured array of rows for programmatic processing or further manipulation. " +
        "Call this tool immediately whenever the user asks to extract, read, or view tables from a document. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> ParserExtractTables(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Output format: 'markdown' (default — renders in IDE and chat) or 'json' (structured data)")] string format = "markdown",
        [Description("Page number to extract tables from (1-based). Omit for all pages.")] int? page = null,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var loadOptions = password != null ? new LoadOptions(password) : new LoadOptions();
        using var parser = new GroupDocs.Parser.Parser(resolved.Stream, loadOptions);

        if (!parser.Features.Tables)
            return "Table extraction is not supported for this document format.";

        var options = new PageTableAreaOptions(null);
        var tables = (page.HasValue
            ? parser.GetTables(page.Value - 1, options)
            : parser.GetTables(options)).ToList();

        if (tables.Count == 0)
            return page.HasValue
                ? $"No tables found on page {page} of '{resolved.FileName}'."
                : $"No tables found in '{resolved.FileName}'.";

        var useJson = format.Equals("json", StringComparison.OrdinalIgnoreCase);
        var result = useJson
            ? RenderJson(tables)
            : RenderMarkdown(tables, resolved.FileName);

        var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may be limited.\n\n";
        return prefix + output.TruncateText(result,
            "Use 'page' parameter to extract tables from a specific page, or use format='json' for compact output.");
    }

    private static string RenderMarkdown(List<PageTableArea> tables, string fileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Extracted {tables.Count} table(s) from '{fileName}':\n");

        for (var t = 0; t < tables.Count; t++)
        {
            var table = tables[t];
            var pageNum = table.Page?.Index + 1;
            sb.AppendLine($"### Table {t + 1}  _(page {pageNum}, {table.RowCount} rows × {table.ColumnCount} cols)_\n");

            if (table.RowCount == 0) continue;

            // Read all cell text up front and track column widths
            var cells = new string[table.RowCount, table.ColumnCount];
            var widths = new int[table.ColumnCount];
            for (var r = 0; r < table.RowCount; r++)
                for (var c = 0; c < table.ColumnCount; c++)
                {
                    var text = table[r, c]?.Text?.Trim() ?? string.Empty;
                    cells[r, c] = text;
                    widths[c] = Math.Max(widths[c], Math.Max(text.Length, 3));
                }

            // Header row (row 0)
            for (var c = 0; c < table.ColumnCount; c++)
                sb.Append($"| {cells[0, c].PadRight(widths[c])} ");
            sb.AppendLine("|");

            // Separator
            for (var c = 0; c < table.ColumnCount; c++)
                sb.Append($"|{new string('-', widths[c] + 2)}");
            sb.AppendLine("|");

            // Data rows
            for (var r = 1; r < table.RowCount; r++)
            {
                for (var c = 0; c < table.ColumnCount; c++)
                    sb.Append($"| {cells[r, c].PadRight(widths[c])} ");
                sb.AppendLine("|");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderJson(List<PageTableArea> tables)
    {
        var result = tables.Select((table, t) => new
        {
            table   = t + 1,
            page    = table.Page?.Index + 1,
            rows    = table.RowCount,
            columns = table.ColumnCount,
            data    = Enumerable.Range(0, table.RowCount)
                .Select(r => Enumerable.Range(0, table.ColumnCount)
                    .Select(c => table[r, c]?.Text?.Trim() ?? string.Empty)
                    .ToArray())
                .ToArray()
        });

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
