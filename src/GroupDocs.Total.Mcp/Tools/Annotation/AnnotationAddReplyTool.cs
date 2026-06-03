using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Models;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationAddReplyTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Adds a reply to an existing annotation and saves the result back to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Call get_annotations first to retrieve the target annotation 'id'. " +
        "Call this tool whenever the user asks to reply to an annotation, add a comment thread, or respond to a review note. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Added reply to annotation <id> in <file>'). " +
        "On failure, the response text starts with 'Reply add failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationAddReply(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Annotation ID to reply to (from get_annotations)")] int annotationId,
        [Description("Reply comment text")] string comment,
        [Description("Name of the user posting the reply. Defaults to 'mcp-user'.")] string userName = "mcp-user",
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
            var target = annotations.FirstOrDefault(a => a.Id == annotationId);
            if (target == null)
                return $"Annotation id {annotationId} not found in '{resolved.FileName}'. " +
                       $"Call get_annotations to see valid IDs: {string.Join(", ", annotations.Select(a => a.Id))}.";

            target.Replies ??= new List<Reply>();
            target.Replies.Add(new Reply
            {
                Comment   = comment,
                RepliedOn = DateTime.Now,
                User      = new User { Name = userName },
            });

            annotator.Update(annotations);

            using var outputMs = new MemoryStream();
            annotator.Save(outputMs);

            var savedPath = await storage.WriteFileAsync(resolved.FileName, outputMs.ToArray(), rewrite: true);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + await output.BuildFileOutputAsync(savedPath,
                $"Added reply to annotation {annotationId} in '{resolved.FileName}' by {userName}");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName, annotationId);
        }
    }

    private static string FormatException(Exception ex, string fileName, int annotationId)
    {
        var sb = new StringBuilder();
        sb.Append($"Reply add failed for '{fileName}' (annotationId: {annotationId}): ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
