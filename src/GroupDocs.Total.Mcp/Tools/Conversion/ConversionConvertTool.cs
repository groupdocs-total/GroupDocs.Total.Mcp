using System.ComponentModel;
using GroupDocs.Conversion;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Conversion;

[McpServerToolType]
public static class ConversionConvertTool
{
    [McpServerTool, Description(
        "[GroupDocs.Conversion] Converts a document to a different format and saves the result to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, HTML, PNG, JPG, and 50+ more formats. " +
        "Call this tool immediately whenever the user asks to convert, change format, export, or save as a different file type. " +
        "Do NOT pre-check whether files exist — just pass the filenames the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> ConversionConvert(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Target format: pdf, docx, xlsx, pptx, html, png, jpg, csv, txt, rtf")] string format,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var targetExt = format.TrimStart('.').ToLowerInvariant();
        var outputName = Path.ChangeExtension(resolved.FileName, targetExt);

        // GroupDocs.Conversion v26 works with file paths.
        // Write input stream to a temp file, convert, then save output to storage.
        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}.{targetExt}");

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var converter = new Converter(tempInput);
            var possibleConversions = converter.GetPossibleConversions();
            var targetConversion = possibleConversions[targetExt];
            if (targetConversion == null)
                return $"Format '{targetExt}' is not supported for '{Path.GetExtension(resolved.FileName)}' input.";
            converter.Convert(tempOutput, targetConversion.ConvertOptions);

            if (!File.Exists(tempOutput))
            {
                return licenseManager.IsLicensed
                    ? $"Conversion failed — output file was not created. " +
                      $"The format '{targetExt}' may not be supported for '{Path.GetExtension(resolved.FileName)}' input."
                    : $"[Evaluation mode] Conversion did not produce output. " +
                      "This may be an evaluation limitation. " +
                      "Set GROUPDOCS_LICENSE_PATH for full conversion support.";
            }

            var outputBytes = await File.ReadAllBytesAsync(tempOutput);
            var savedPath = await storage.WriteFileAsync(outputName, outputBytes, rewrite: false);

            var prefix = licenseManager.IsLicensed
                ? string.Empty
                : "[Evaluation mode] Output may be limited and include watermarks.\n\n";

            var description = $"{prefix}Converted '{resolved.FileName}' to {targetExt.ToUpperInvariant()}";
            return await output.BuildFileOutputAsync(savedPath, description);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }
}
