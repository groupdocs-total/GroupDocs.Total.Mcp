using System.ComponentModel;
using System.Text;
using GroupDocs.Markdown;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Markdown;

[McpServerToolType]
public static class MarkdownConvertToMarkdownTool
{
    [McpServerTool, Description(
        "[GroupDocs.Markdown] Converts a document to clean, structured Markdown (.md). " +
        "Supports PDF, DOCX/DOC/RTF/ODT, XLSX/XLS/ODS/CSV/TSV, EPUB, MOBI, TXT, CHM and other formats. " +
        "By default, images are embedded as base64 data URIs so the output is fully self-contained. " +
        "Use 'images' to control image handling: 'base64' (default), 'file' (save images alongside the .md and reference by path), or 'skip' (omit images — text-only). " +
        "Use 'pages' to limit output to specific 1-based pages or worksheets. " +
        "The generated Markdown is saved to storage AND the content is returned inline. " +
        "Call this tool immediately whenever the user asks to convert a document to Markdown, export as MD, or extract as Markdown. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> MarkdownConvertToMarkdown(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Image handling: 'base64' (default — embed as data URIs, self-contained), 'file' (save images alongside .md), or 'skip' (text-only, no images)")] string images = "base64",
        [Description("Comma-separated 1-based page or worksheet numbers to convert, e.g. '1,3,5'. Omit for the whole document.")] string? pages = null,
        [Description("Include YAML front matter (title, author, format, page count) at the top of the output")] bool frontMatter = false,
        [Description("Markdown dialect: 'github' (default) or 'commonmark'")] string flavor = "github",
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);
        var mdName = $"{baseName}.md";

        var imagesMode = NormalizeImageMode(images);
        var imagesDirName = $"{baseName}_images";
        var tempImagesDir = Path.Combine(Path.GetTempPath(), $"gd_mcp_md_{Guid.NewGuid():N}");

        var options = new ConvertOptions
        {
            ImageExportStrategy = imagesMode switch
            {
                "file" => new ExportImagesToFileSystemStrategy(tempImagesDir)
                {
                    ImagesRelativePath = imagesDirName
                },
                "skip" => (IImageExportStrategy)new SkipImagesStrategy(),
                _ => new ExportImagesAsBase64Strategy()
            },
            IncludeFrontMatter = frontMatter,
            Flavor = NormalizeFlavor(flavor)
        };

        var pageNumbers = ParsePages(pages);
        if (pageNumbers != null)
            options.PageNumbers = pageNumbers;

        var loadOptions = new LoadOptions { Password = password };

        try
        {
            using var converter = new MarkdownConverter(resolved.Stream, loadOptions);
            var result = converter.Convert(options);

            if (!result.IsSuccess)
                return $"Conversion failed: {result.ErrorMessage ?? "unknown error"}";

            var markdown = result.Content ?? string.Empty;
            var savedPath = await storage.WriteFileAsync(mdName, Encoding.UTF8.GetBytes(markdown), rewrite: true);

            if (imagesMode == "file" && Directory.Exists(tempImagesDir))
            {
                foreach (var imgFile in Directory.EnumerateFiles(tempImagesDir))
                {
                    var imgBytes = await File.ReadAllBytesAsync(imgFile);
                    var storedImgPath = $"{imagesDirName}/{Path.GetFileName(imgFile)}";
                    await storage.WriteFileAsync(storedImgPath, imgBytes, rewrite: true);
                }
            }

            var fileInfo = await output.BuildFileOutputAsync(savedPath,
                $"Converted '{resolved.FileName}' to Markdown (images: {imagesMode})");

            var prefix = licenseManager.IsLicensed
                ? string.Empty
                : "[Evaluation mode] Output may be limited and include watermarks.\n\n";

            var mdSection = output.TruncateText(markdown,
                "Use the saved .md file for the full content or set 'pages' to convert specific pages.");

            return $"{prefix}{fileInfo}\n\n{mdSection}";
        }
        finally
        {
            if (Directory.Exists(tempImagesDir))
                Directory.Delete(tempImagesDir, recursive: true);
        }
    }

    private static string NormalizeImageMode(string value)
    {
        var v = (value ?? "base64").Trim().ToLowerInvariant();
        return v switch
        {
            "none" or "no" or "off" or "skip" or "without" => "skip",
            "file" or "files" or "filesystem" or "disk" => "file",
            _ => "base64"
        };
    }

    private static MarkdownFlavor NormalizeFlavor(string value)
    {
        var v = (value ?? "github").Trim().ToLowerInvariant();
        return v switch
        {
            "commonmark" or "common" or "cm" => MarkdownFlavor.CommonMark,
            _ => MarkdownFlavor.GitHub
        };
    }

    private static int[]? ParsePages(string? pages)
    {
        if (string.IsNullOrWhiteSpace(pages))
            return null;

        var parts = pages.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<int>(parts.Length);
        foreach (var part in parts)
            if (int.TryParse(part.Trim(), out var n) && n >= 1)
                result.Add(n);

        return result.Count == 0 ? null : result.ToArray();
    }
}
