using System.ComponentModel;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Watermark;
using GroupDocs.Watermark.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Watermark;

[McpServerToolType]
public static class WatermarkSearchWatermarksTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Watermark] Searches for watermarks in a document and returns their details (type, text, position, size) as JSON. " +
        "Call this tool immediately whenever the user asks to search for watermarks, find watermarks, or check if a document has watermarks. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> WatermarkSearchWatermarks(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var loadOptions = password != null ? new LoadOptions(password) : null;
            using var watermarker = loadOptions != null
                ? new Watermarker(tempInput, loadOptions)
                : new Watermarker(tempInput);

            var watermarks = watermarker.Search();

            var results = watermarks.Select(w => new
            {
                type = w.ImageData != null ? "image" : "text",
                text = w.Text,
                page = w.PageNumber,
                x = w.X,
                y = w.Y,
                width = w.Width,
                height = w.Height,
                rotateAngle = w.RotateAngle
            }).ToList();

            // Pitfall #16: return raw JSON, never via OutputHelper.TruncateText.
            return JsonSerializer.Serialize(new { count = results.Count, watermarks = results }, JsonOptions);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
        }
    }
}
