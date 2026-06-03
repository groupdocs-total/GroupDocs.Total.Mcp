using System.ComponentModel;
using System.Text;
using GroupDocs.Annotation;
using GroupDocs.Annotation.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Annotation;

[McpServerToolType]
public static class AnnotationImportAnnotationsTool
{
    [McpServerTool, Description(
        "[GroupDocs.Annotation] Imports annotations from another source (an XML annotations file or an annotated document) and saves the merged document back to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats for the target. " +
        "Source must be either an XML file produced by export_annotations, or another annotated document of the same engine format. " +
        "Call this tool whenever the user asks to import, merge, or apply annotations from one document to another. " +
        "Do NOT pre-check whether files exist — pass the filenames the user provided directly. " +
        "Returns a saved-path message ('Imported annotations from <source> into <file>'). " +
        "On failure, the response text starts with 'Annotation import failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> AnnotationImportAnnotations(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Source file containing annotations to import — an XML file (.xml) or an annotated document (.pdf, .docx, etc.).")] FileInput source,
        [Description("Password for protected target documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);
        using var resolvedSource = await resolver.ResolveAsync(source);

        try
        {
            using var inputMs = new MemoryStream();
            await resolved.Stream.CopyToAsync(inputMs);
            inputMs.Position = 0;

            var loadOptions = password != null ? new LoadOptions { Password = password } : null;
            using var annotator = loadOptions != null
                ? new Annotator(inputMs, loadOptions)
                : new Annotator(inputMs);

            // Engine has TWO entry points with confusing names:
            //   ImportAnnotationsFromDocument(path)  — merges annotations from another annotated document
            //   ExportAnnotationsFromXMLFile(path)   — IMPORTS annotations from an XML file (the name reflects the
            //                                          original XML side, not the document side)
            // Buffer source to a temp file once and dispatch by extension.
            var tempSource = Path.Combine(Path.GetTempPath(), $"gd_mcp_ann_{Guid.NewGuid()}{Path.GetExtension(resolvedSource.FileName)}");
            try
            {
                await using (var fs = File.Create(tempSource))
                    await resolvedSource.Stream.CopyToAsync(fs);

                var ext = Path.GetExtension(resolvedSource.FileName).ToLowerInvariant();
                if (ext == ".xml")
                    annotator.ExportAnnotationsFromXMLFile(tempSource);
                else
                    annotator.ImportAnnotationsFromDocument(tempSource);

                using var outputMs = new MemoryStream();
                annotator.Save(outputMs);

                var savedPath = await storage.WriteFileAsync(resolved.FileName, outputMs.ToArray(), rewrite: true);
                var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
                return prefix + await output.BuildFileOutputAsync(savedPath,
                    $"Imported annotations from '{resolvedSource.FileName}' into '{resolved.FileName}'");
            }
            finally
            {
                if (File.Exists(tempSource)) File.Delete(tempSource);
            }
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName, resolvedSource.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName, string sourceName)
    {
        var sb = new StringBuilder();
        sb.Append($"Annotation import failed for '{fileName}' (source: '{sourceName}'): ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
