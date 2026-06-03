using System.ComponentModel;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Watermark;
using GroupDocs.Watermark.Common;
using GroupDocs.Watermark.Options;
using GroupDocs.Watermark.Watermarks;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Watermark;

[McpServerToolType]
public static class WatermarkAddWatermarkTool
{
    [McpServerTool, Description(
        "[GroupDocs.Watermark] Adds a text watermark to a document and saves the watermarked file to storage. " +
        "Call this tool immediately whenever the user asks to add a watermark, stamp text onto a document, or mark a document as draft/confidential. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> WatermarkAddWatermark(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Watermark text to add")] string text,
        [Description("Font size (default 36)")] int fontSize = 36,
        [Description("Rotation angle in degrees (default -45)")] int rotation = -45,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var ext = Path.GetExtension(resolved.FileName);
        var outputName = $"{Path.GetFileNameWithoutExtension(resolved.FileName)}_watermarked{ext}";
        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var loadOptions = password != null ? new LoadOptions(password) : null;
            using var watermarker = loadOptions != null
                ? new Watermarker(tempInput, loadOptions)
                : new Watermarker(tempInput);

            var watermark = new TextWatermark(text, new Font("Arial", fontSize))
            {
                ForegroundColor = Color.FromArgb(128, 192, 192, 192),
                Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RotateAngle = rotation
            };

            watermarker.Add(watermark);
            watermarker.Save(tempOutput);

            var bytes = await File.ReadAllBytesAsync(tempOutput);
            var savedPath = await storage.WriteFileAsync(outputName, bytes, rewrite: false);

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return await output.BuildFileOutputAsync(savedPath, $"{prefix}Added text watermark '{text}' to '{resolved.FileName}'");
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }
}
