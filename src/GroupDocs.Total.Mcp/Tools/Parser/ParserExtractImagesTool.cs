using System.ComponentModel;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Parser.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Parser;

[McpServerToolType]
public static class ParserExtractImagesTool
{    
    [McpServerTool, Description(
        "[GroupDocs.Parser] Extracts all images from a document and saves them to storage. Returns a list of saved image file paths or download URLs. " +
        "Call this tool immediately whenever the user asks to extract images, get images, or save images from a document. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]

    public static async Task<string> ParserExtractImages(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Password for protected documents")] string? password = null,
        [Description("Page number to extract images from (1-based). Omit for all pages.")] int? page = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var loadOptions = password != null ? new LoadOptions(password) : new LoadOptions();

        using var parser = new GroupDocs.Parser.Parser(resolved.Stream, loadOptions);

        var images = page.HasValue
            ? parser.GetImages(page.Value - 1)
            : parser.GetImages();

        if (images == null)
            return "Image extraction is not supported for this document format.";

        var imageList = images.ToList();
        if (imageList.Count == 0)
            return "No images found in this document.";

        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);
        var results = new List<string>();
        var index = 1;

        foreach (var image in imageList)
        {
            var ext = image.FileType?.Extension ?? ".png";
            var imageName = $"{baseName}_image{index}{ext}";

            using var imageStream = image.GetImageStream();
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);

            var savedPath = await storage.WriteFileAsync(imageName, ms.ToArray(), rewrite: false);
            var line = await output.BuildFileOutputAsync(savedPath, $"Image {index}");
            results.Add($"- {line}");
            index++;
        }

        var prefix = licenseManager.IsLicensed
            ? string.Empty
            : "[Evaluation mode] Output may be limited.\n\n";

        return $"{prefix}Extracted {imageList.Count} image(s):\n{string.Join("\n", results)}";
    }
}
