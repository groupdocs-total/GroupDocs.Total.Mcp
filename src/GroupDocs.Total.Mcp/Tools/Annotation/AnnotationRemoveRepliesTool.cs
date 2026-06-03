using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationRemoveRepliesTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Removes replies from annotations and saves the result back to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Filter mode: pass 'replyIds' (comma-separated) to remove by reply ID, OR pass 'userName' to remove all replies from one user. Pass neither to remove ALL replies in the document. " +
        "Call this tool whenever the user asks to delete a reply, remove a comment thread, or clear replies from one author. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Removed N reply / replies from <file>'). " +
        "On failure, the response text starts with 'Reply removal failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationRemoveReplies(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Comma-separated reply IDs to remove, e.g. '12,34'. Omit to fall back to userName / remove-all.")] string? replyIds = null,
        [Description("Remove all replies authored by this user name. Mutually exclusive with replyIds.")] string? userName = null,
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
            var totalReplies = annotations.Sum(a => a.Replies?.Count ?? 0);
            if (totalReplies == 0)
                return $"No replies found in '{resolved.FileName}'.";

            HashSet<int>? idSet = null;
            if (!string.IsNullOrWhiteSpace(replyIds))
            {
                idSet = new HashSet<int>(replyIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var n) ? n : -1)
                    .Where(n => n >= 0));
            }

            int removed = 0;
            foreach (var annotation in annotations)
            {
                if (annotation.Replies is null || annotation.Replies.Count == 0)
                    continue;

                var before = annotation.Replies.Count;
                if (idSet is not null)
                    annotation.Replies = annotation.Replies.Where(r => !idSet.Contains(r.Id)).ToList();
                else if (!string.IsNullOrWhiteSpace(userName))
                    annotation.Replies = annotation.Replies.Where(r => !string.Equals(r.User?.Name, userName, StringComparison.OrdinalIgnoreCase)).ToList();
                else
                    annotation.Replies = new List<GroupDocs.Annotation.Models.Reply>();

                removed += before - annotation.Replies.Count;
            }

            if (removed == 0)
                return $"No matching replies found in '{resolved.FileName}'.";

            annotator.Update(annotations);

            using var outputMs = new MemoryStream();
            annotator.Save(outputMs);

            var savedPath = await storage.WriteFileAsync(resolved.FileName, outputMs.ToArray(), rewrite: true);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + await output.BuildFileOutputAsync(savedPath,
                $"Removed {removed} reply / replies from '{resolved.FileName}'");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Reply removal failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
