using System.ComponentModel;
using System.Text;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Watermark;
using GroupDocs.Watermark.Options;
using GroupDocs.Watermark.Search;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Watermark;

[McpServerToolType]
public static class WatermarkRemoveWatermarksTool
{
    [McpServerTool, Description(
        "[GroupDocs.Watermark] Removes watermarks from a document and saves the cleaned file to storage as '<name>_unwatermarked.<ext>'. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 50+ more document and image formats. " +
        "Filter mode: pass 'text' to remove only watermarks whose text contains this string; omit to remove ALL watermarks found by Search(). " +
        "Call this tool whenever the user asks to remove a watermark, strip watermarks, clean / unstamp a document, or remove a specific tagged watermark. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a saved-path message ('Removed N watermark(s) from <file>'). " +
        "On failure, the response text starts with 'Watermark removal failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> WatermarkRemoveWatermarks(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Remove only watermarks whose text contains this string (case-insensitive). Omit to remove every watermark in the document.")] string? text = null,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var ext = Path.GetExtension(resolved.FileName);
        var outputName = $"{Path.GetFileNameWithoutExtension(resolved.FileName)}_unwatermarked{ext}";
        var tempInput  = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var loadOptions = password != null ? new LoadOptions(password) : null;
            using var watermarker = loadOptions != null
                ? new Watermarker(tempInput, loadOptions)
                : new Watermarker(tempInput);

            var found = watermarker.Search();
            if (found.Count == 0)
                return $"No watermarks found in '{resolved.FileName}'.";

            var toRemove = string.IsNullOrWhiteSpace(text)
                ? found.ToList()
                : found.Where(w => !string.IsNullOrEmpty(w.Text) && w.Text.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

            if (toRemove.Count == 0)
                return $"No watermarks matching '{text}' found in '{resolved.FileName}'. " +
                       $"Found {found.Count} watermark(s) but none matched the filter.";

            foreach (var watermark in toRemove)
                watermarker.Remove(watermark);

            watermarker.Save(tempOutput);

            var bytes = await File.ReadAllBytesAsync(tempOutput);
            var savedPath = await storage.WriteFileAsync(outputName, bytes, rewrite: false);

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            var filterNote = string.IsNullOrWhiteSpace(text) ? "" : $" (filter: '{text}')";
            return await output.BuildFileOutputAsync(savedPath,
                $"{prefix}Removed {toRemove.Count} watermark(s) from '{resolved.FileName}'{filterNote}");
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
        finally
        {
            if (File.Exists(tempInput))  File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Watermark removal failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
