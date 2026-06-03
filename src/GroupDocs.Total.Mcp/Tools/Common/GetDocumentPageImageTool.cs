using System.ComponentModel;
using System.Text;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Viewer;
using GroupDocs.Viewer.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Common;

/// <summary>
/// Cross-product page-rendering tool. Backed by GroupDocs.Viewer (170+ format
/// support). Returns inline base64 PNG content blocks so AI clients display
/// the rendered pages directly. Replaces what each per-product MCP would
/// otherwise ship as its own preview tool.
/// </summary>
[McpServerToolType]
public static class GetDocumentPageImageTool
{
    // Max pages rendered per call. Beyond this the response payload grows past
    // most MCP clients' practical size limit; callers should batch.
    private const int MaxPagesPerCall = 5;

    [McpServerTool, Description(
        "Renders one or more document pages as PNG images and returns them INLINE so AI clients can display them directly. " +
        "Backed by GroupDocs.Viewer — supports 170+ formats including PDF, DOCX, XLSX, PPTX, ODT, JPEG, PNG, TIFF, CAD, EPUB, and more. " +
        "Pass 'pages' as a comma-separated list (e.g. '1,3,5') or a range ('1-3'); omit to render only page 1. " +
        "Use whenever the user asks to preview, show, render, view, or visualise document pages. " +
        "Maximum 5 pages per call to stay under MCP client message size limits; batch further pages in additional calls. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a CallToolResult with one TextContentBlock summary followed by one ImageContentBlock per rendered page. " +
        "On failure, the response contains a single TextContentBlock starting with 'Preview generation failed for' and IsError=true.")]
    public static async Task<CallToolResult> GetDocumentPageImage(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Pages to render, e.g. '1,3,5' or '1-3'. Omit for page 1 only.")] string? pages = null,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        try
        {
            var requested = ParsePages(pages).Distinct().Take(MaxPagesPerCall).OrderBy(n => n).ToArray();
            if (requested.Length == 0)
                requested = new[] { 1 };

            var loadOptions = new LoadOptions { Password = password };
            using var viewer = new GroupDocs.Viewer.Viewer(resolved.Stream, loadOptions);

            var streams = new Dictionary<int, MemoryStream>();
            try
            {
                var pngOptions = new PngViewOptions(pageNumber =>
                {
                    var ms = new MemoryStream();
                    streams[pageNumber] = ms;
                    return ms;
                });
                viewer.View(pngOptions, requested);

                var prefix = licenseManager.IsLicensed
                    ? string.Empty
                    : "[Evaluation mode] Output may include watermarks.\n\n";

                var content = new List<ContentBlock>
                {
                    new TextContentBlock { Text = $"{prefix}Rendered {streams.Count} page(s) of '{resolved.FileName}': {string.Join(", ", streams.Keys.OrderBy(n => n))}" }
                };
                foreach (var n in streams.Keys.OrderBy(n => n))
                    content.Add(ImageContentBlock.FromBytes(streams[n].ToArray(), "image/png"));

                return new CallToolResult { Content = content };
            }
            finally
            {
                foreach (var s in streams.Values) s.Dispose();
            }
        }
        catch (Exception ex)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = FormatException(ex, resolved.FileName) }],
                IsError = true,
            };
        }
    }

    private static IEnumerable<int> ParsePages(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            yield break;

        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-') && part.Split('-', 2) is [string a, string b] && int.TryParse(a, out var start) && int.TryParse(b, out var end))
            {
                if (start <= end)
                    for (var n = start; n <= end; n++) yield return n;
            }
            else if (int.TryParse(part, out var n))
            {
                yield return n;
            }
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Preview generation failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
