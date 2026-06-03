using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Models;
using GroupDocs.Annotation.Models.AnnotationModels;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationAddAnnotationTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Adds an annotation to a document and saves the annotated file as '<name>_annotated.<ext>'. " +
        "Supported types: textfield, area, point, arrow, highlight, underline, strikeout. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Call this tool whenever the user asks to annotate, comment, highlight, underline, strikeout, or mark up a document. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found. " +
        "Returns a saved-path message ('Added <type> annotation to <file> on page <n>') with the download URL or storage path. " +
        "On failure, the response text starts with 'Annotation failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationAddAnnotation(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Annotation type: textfield, area, point, arrow, highlight, underline, strikeout")] string type,
        [Description("Annotation text or comment")] string text,
        [Description("Page number (1-based)")] int page = 1,
        [Description("X position (document coordinates)")] int x = 100,
        [Description("Y position (document coordinates)")] int y = 100,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var outputName = $"{Path.GetFileNameWithoutExtension(resolved.FileName)}_annotated{Path.GetExtension(resolved.FileName)}";

        try
        {
            // Annotator requires a seekable stream — buffer input into MemoryStream.
            using var inputMs = new MemoryStream();
            await resolved.Stream.CopyToAsync(inputMs);
            inputMs.Position = 0;

            var loadOptions = password != null ? new LoadOptions { Password = password } : null;
            using var annotator = loadOptions != null
                ? new Annotator(inputMs, loadOptions)
                : new Annotator(inputMs);

            annotator.Add(CreateAnnotation(type, text, page - 1, x, y));

            using var outputMs = new MemoryStream();
            annotator.Save(outputMs);

            var savedPath = await storage.WriteFileAsync(outputName, outputMs.ToArray(), rewrite: false);

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            var description = $"{prefix}Added {type} annotation to '{resolved.FileName}' on page {page}";
            return await output.BuildFileOutputAsync(savedPath, description);
        }
        catch (Exception ex)
        {
            // Pitfall #18 — surface the underlying engine exception with a descriptive prefix
            // instead of letting it bubble to MCP's generic "An error occurred invoking 'add_annotation'".
            return FormatException(ex, resolved.FileName, type);
        }
    }

    private static AnnotationBase CreateAnnotation(string type, string text, int zeroBasedPage, int x, int y) =>
        type.ToLowerInvariant() switch
        {
            "textfield" or "text" => new TextFieldAnnotation { Message = text, Text = text, PageNumber = zeroBasedPage, Box = new Rectangle(x, y, 200, 50),  CreatedOn = DateTime.Now },
            "area"      => new AreaAnnotation      { Message = text, PageNumber = zeroBasedPage, Box = new Rectangle(x, y, 200, 100), CreatedOn = DateTime.Now, Opacity = 0.3, BackgroundColor = 16776960 },
            "point"     => new PointAnnotation     { Message = text, PageNumber = zeroBasedPage, Box = new Rectangle(x, y, 10, 10),   CreatedOn = DateTime.Now },
            "arrow"     => new ArrowAnnotation     { Message = text, PageNumber = zeroBasedPage, Box = new Rectangle(x, y, 200, 50),  CreatedOn = DateTime.Now },
            "highlight" => new HighlightAnnotation { Message = text, PageNumber = zeroBasedPage, Points = BoxToPoints(x, y, 200, 50), CreatedOn = DateTime.Now, Opacity = 0.5 },
            "underline" => new UnderlineAnnotation { Message = text, PageNumber = zeroBasedPage, Points = BoxToPoints(x, y, 200, 50), CreatedOn = DateTime.Now },
            "strikeout" => new StrikeoutAnnotation { Message = text, PageNumber = zeroBasedPage, Points = BoxToPoints(x, y, 200, 50), CreatedOn = DateTime.Now },
            _ => throw new ArgumentException($"Unknown annotation type '{type}'. Supported: textfield, area, point, arrow, highlight, underline, strikeout.")
        };

    private static List<Point> BoxToPoints(int x, int y, int width, int height) =>
        [new Point(x, y), new Point(x + width, y), new Point(x, y + height), new Point(x + width, y + height)];

    private static string FormatException(Exception ex, string fileName, string type)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation failed for '{fileName}' (type: '{type}'): ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
