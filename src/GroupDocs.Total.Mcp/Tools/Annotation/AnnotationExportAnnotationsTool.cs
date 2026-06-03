using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Models.AnnotationModels;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationExportAnnotationsTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Extracts annotations from a document and saves them as an XML file (suitable for re-import via import_annotations). " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Call this tool whenever the user asks to export, extract, serialize, or save annotations to XML. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Exported N annotation(s) from <file> to <name>.annotations.xml'). " +
        "On failure, the response text starts with 'Annotation export failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationExportAnnotations(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
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

            using var xmlMs = new MemoryStream();
            new XmlSerializer(typeof(List<AnnotationBase>)).Serialize(xmlMs, annotations);

            var outputName = $"{Path.GetFileNameWithoutExtension(resolved.FileName)}.annotations.xml";
            var savedPath = await storage.WriteFileAsync(outputName, xmlMs.ToArray(), rewrite: true);
            return await output.BuildFileOutputAsync(savedPath,
                $"Exported {annotations.Count} annotation(s) from '{resolved.FileName}' to '{outputName}'");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation export failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
