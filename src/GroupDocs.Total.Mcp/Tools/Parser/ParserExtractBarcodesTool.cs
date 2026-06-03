using System.ComponentModel;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Parser.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Parser;

[McpServerToolType]
public static class ParserExtractBarcodesTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Parser] Extracts all barcodes and QR codes from a document and returns their decoded values and type names as JSON. " +
        "Detects Code128, QR Code, PDF417, DataMatrix, EAN-13, EAN-8, UPC, Aztec, and many more symbologies. " +
        "Call this tool immediately whenever the user asks to read, scan, or extract barcodes or QR codes from a document. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> ParserExtractBarcodes(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Page number to scan (1-based). Omit for all pages.")] int? page = null,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var loadOptions = password != null ? new LoadOptions(password) : new LoadOptions();
        using var parser = new GroupDocs.Parser.Parser(resolved.Stream, loadOptions);

        if (!parser.Features.Barcodes)
            return "Barcode extraction is not supported for this document format.";

        var barcodes = page.HasValue
            ? parser.GetBarcodes(page.Value - 1)
            : parser.GetBarcodes();

        var list = barcodes.ToList();
        if (list.Count == 0)
            return page.HasValue
                ? $"No barcodes found on page {page} of '{resolved.FileName}'."
                : $"No barcodes found in '{resolved.FileName}'.";

        var result = list.Select((b, i) => new
        {
            index      = i + 1,
            value      = b.Value,
            type       = b.CodeTypeName,
            page       = b.Page?.Index + 1,
            confidence = b.Confidence,
            angle      = b.Angle
        });

        var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may be limited.\n\n";
        return $"{prefix}Found {list.Count} barcode(s) in '{resolved.FileName}':\n\n" +
               JsonSerializer.Serialize(result, JsonOptions);
    }
}
