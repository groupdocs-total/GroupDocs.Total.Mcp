using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Models.AnnotationModels.Interfaces.Properties;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationGetAnnotationsTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Annotation] Lists all annotations in a document as JSON. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Each entry includes 'id' (use this with remove_annotations / update_annotation / add_reply), 'type', 'message', 'page' (1-based), bounding box, user, and replies. " +
        "Call this tool first whenever the user asks to list, show, inspect, or review annotations or comments. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a JSON object with `found` (count) and `annotations` (array). " +
        "On failure, the response text starts with 'Annotation lookup failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationGetAnnotations(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        try
        {
            using var inputMs = new MemoryStream();
            await resolved.Stream.CopyToAsync(inputMs);
            inputMs.Position = 0;

            var loadOptions = password != null ? new LoadOptions { Password = password } : null;
            using var annotator = loadOptions != null
                ? new Annotator(inputMs, loadOptions)
                : new Annotator(inputMs);

            var annotations = annotator.Get();

            if (annotations.Count == 0)
                return $"No annotations found in '{resolved.FileName}'.";

            var results = annotations.Select(a => new
            {
                id      = a.Id,
                type    = a.Type.ToString(),
                message = a.Message,
                page    = a.PageNumber + 1,
                box     = (a as IBox)?.Box is { } box
                    ? new { x = (int)box.X, y = (int)box.Y, width = (int)box.Width, height = (int)box.Height }
                    : null as object,
                user    = a.User?.Name,
                replies = a.Replies?.Select(r => new { id = r.Id, comment = r.Comment, by = r.User?.Name }).ToList()
            }).ToArray();

            // Pitfall #16: return raw JSON, never via OutputHelper.TruncateText.
            return JsonSerializer.Serialize(new { found = annotations.Count, annotations = results }, JsonOptions);
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation lookup failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
