using System.ComponentModel;
using System.Drawing;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Redaction;
using GroupDocs.Redaction.Options;
using GroupDocs.Redaction.Redactions;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Redaction;

[McpServerToolType]
public static class RedactionRedactImageAreaTool
{
    [McpServerTool, Description(
        "[GroupDocs.Redaction] Covers a rectangular area of a document page with a solid-color box, permanently hiding image content (e.g. faces, signatures, stamps). " +
        "Coordinates are in pixels from the top-left corner of the page. " +
        "Call this tool whenever the user asks to hide, cover, or black out an image area or region in a document. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> RedactionRedactImageArea(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("X coordinate (pixels) of the top-left corner of the area to redact")] int x,
        [Description("Y coordinate (pixels) of the top-left corner of the area to redact")] int y,
        [Description("Width (pixels) of the area to redact")] int width,
        [Description("Height (pixels) of the area to redact")] int height,
        [Description("Fill color for the redaction box — named color (e.g. 'Black', 'Red') or hex (e.g. '#FF0000'). Default: 'Black'")] string color = "Black",
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var fillColor = ParseColor(color);
        var redaction = new ImageAreaRedaction(
            new Point(x, y),
            new RegionReplacementOptions(fillColor, new Size(width, height)));

        var loadOptions = new LoadOptions { Password = password };
        using var redactor = new Redactor(resolved.Stream, loadOptions);

        var result = redactor.Apply(redaction);
        if (result.Status == RedactionStatus.Failed)
        {
            var errors = result.RedactionLog
                .Select(e => e.Result.ErrorMessage)
                .Where(m => !string.IsNullOrEmpty(m));
            return $"Image area redaction failed: {string.Join("; ", errors)}";
        }

        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);
        var ext = Path.GetExtension(resolved.FileName);
        var outputName = $"{baseName}_redacted{ext}";

        var ms = new MemoryStream();
        try
        {
            redactor.Save(ms, new RasterizationOptions { Enabled = false });

            var savedPath = await storage.WriteFileAsync(outputName, ms.ToArray(), rewrite: true);
            var line = await output.BuildFileOutputAsync(savedPath,
                $"Image area redacted at ({x},{y}) size {width}×{height} with {color} (status: {result.Status})");

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + line;
        }
        finally
        {
            await ms.DisposeAsync();
        }
    }

    private static Color ParseColor(string color)
    {
        try { return ColorTranslator.FromHtml(color); }
        catch { return Color.Black; }
    }
}
