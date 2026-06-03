using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationRemoveAnnotationsTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Removes annotations from a document by ID and saves the result back to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Call get_annotations first to retrieve annotation IDs, then pass them here. " +
        "Omit 'ids' to remove every annotation in the document. " +
        "Call this tool whenever the user asks to delete, remove, or clear annotations. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Removed N annotation(s) from <file>'). " +
        "On failure, the response text starts with 'Annotation removal failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationRemoveAnnotations(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Comma-separated annotation IDs to remove, e.g. '1,3,5'. Omit to remove all.")] string? ids = null,
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

            var existing = annotator.Get();
            if (existing.Count == 0)
                return $"No annotations found in '{resolved.FileName}'.";

            List<int> toRemove;
            if (string.IsNullOrWhiteSpace(ids))
            {
                toRemove = existing.Select(a => a.Id).ToList();
            }
            else
            {
                toRemove = ParseIds(ids);
                var missing = toRemove.Except(existing.Select(a => a.Id)).ToList();
                if (missing.Count > 0)
                    return $"IDs not found: {string.Join(", ", missing)}. " +
                           $"Call get_annotations to see valid IDs: {string.Join(", ", existing.Select(a => a.Id))}.";
            }

            annotator.Remove(toRemove);

            using var outputMs = new MemoryStream();
            annotator.Save(outputMs);

            var savedPath = await storage.WriteFileAsync(resolved.FileName, outputMs.ToArray(), rewrite: true);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + await output.BuildFileOutputAsync(savedPath,
                $"Removed {toRemove.Count} annotation(s) from '{resolved.FileName}'");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
    }

    private static List<int> ParseIds(string ids) =>
        ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(s => int.TryParse(s, out var id) ? id : -1)
           .Where(id => id >= 0)
           .ToList();

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation removal failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
