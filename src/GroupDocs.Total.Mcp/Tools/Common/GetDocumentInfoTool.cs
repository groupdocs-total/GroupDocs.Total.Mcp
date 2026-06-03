using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Parser.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Common;

/// <summary>
/// Cross-product document-info tool. Backed by GroupDocs.Parser because it has
/// the widest format coverage of the bundled engines (170+ document formats —
/// PDF, all Office variants, OpenOffice, images, eBooks, CAD, mail, etc.).
/// Replaces what each per-product MCP would have shipped as its own
/// `{Product}GetDocumentInfo`.
/// </summary>
[McpServerToolType]
public static class GetDocumentInfoTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "Returns basic information about ANY document: file type, page count, and size as JSON. " +
        "Backed by GroupDocs.Parser — supports 170+ formats including PDF, DOCX, XLSX, PPTX, ODT, JPEG, PNG, TIFF, CAD, EPUB, EML, MSG, and more. " +
        "Use this as the first call when the user asks 'what is this file?' or 'how many pages?' before invoking format-specific tools. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a JSON object with `fileName`, `fileType` (extension), `fileTypeName` (human-readable), `pageCount`, `size`. " +
        "On failure, the response text starts with 'Document-info lookup failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> GetDocumentInfo(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        try
        {
            var loadOptions = password != null ? new LoadOptions(password) : new LoadOptions();

            using var parser = new GroupDocs.Parser.Parser(resolved.Stream, loadOptions);
            var info = parser.GetDocumentInfo();

            if (info == null)
                return $"Document-info lookup failed for '{resolved.FileName}': engine returned null.";

            return JsonSerializer.Serialize(new
            {
                fileName     = resolved.FileName,
                fileType     = info.FileType?.Extension,
                fileTypeName = info.FileType?.ToString(),
                pageCount    = info.PageCount,
                size         = info.Size,
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Document-info lookup failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
