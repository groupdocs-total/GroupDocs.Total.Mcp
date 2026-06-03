using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Models;
using GroupDocs.Annotation.Models.AnnotationModels.Interfaces.Properties;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationUpdateAnnotationTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Updates an existing annotation's message and/or bounding box, then saves the result back to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Call get_annotations first to retrieve the annotation 'id' and current values. " +
        "Pass any subset of message/x/y/width/height — omitted parameters keep their current value. " +
        "Call this tool whenever the user asks to edit, modify, move, or resize an existing annotation. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Updated annotation <id> in <file>'). " +
        "On failure, the response text starts with 'Annotation update failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationUpdateAnnotation(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Annotation ID to update (from get_annotations)")] int id,
        [Description("New message/comment text. Omit to keep current.")] string? message = null,
        [Description("New X position. Omit to keep current.")] int? x = null,
        [Description("New Y position. Omit to keep current.")] int? y = null,
        [Description("New width. Omit to keep current.")] int? width = null,
        [Description("New height. Omit to keep current.")] int? height = null,
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
            var target = annotations.FirstOrDefault(a => a.Id == id);
            if (target == null)
                return $"Annotation id {id} not found in '{resolved.FileName}'. " +
                       $"Call get_annotations to see valid IDs: {string.Join(", ", annotations.Select(a => a.Id))}.";

            if (message != null)
                target.Message = message;

            if (target is IBox boxed && boxed.Box is { } currentBox && (x.HasValue || y.HasValue || width.HasValue || height.HasValue))
            {
                boxed.Box = new Rectangle(
                    x ?? (int)currentBox.X,
                    y ?? (int)currentBox.Y,
                    width  ?? (int)currentBox.Width,
                    height ?? (int)currentBox.Height);
            }

            annotator.Update(annotations);

            using var outputMs = new MemoryStream();
            annotator.Save(outputMs);

            var savedPath = await storage.WriteFileAsync(resolved.FileName, outputMs.ToArray(), rewrite: true);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + await output.BuildFileOutputAsync(savedPath,
                $"Updated annotation {id} in '{resolved.FileName}'");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName, id);
        }
    }

    private static string FormatException(Exception ex, string fileName, int id)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation update failed for '{fileName}' (id: {id}): ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
