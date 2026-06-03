using System.ComponentModel;
using GroupDocs.Merger;
using GroupDocs.Merger.Domain.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Merger;

[McpServerToolType]
public static class MergerSplitTool
{
    [McpServerTool, Description(
        "[GroupDocs.Merger] Splits a document into separate files by extracting specific pages, saving each as an individual document to storage. " +
        "Call this tool immediately whenever the user asks to split, extract pages, or separate a document into parts. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> MergerSplit(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Page numbers to extract as separate documents (1-based), e.g. '3,6,8'")] string pages,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var pageNumbers = pages
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        if (pageNumbers.Length == 0)
            return "Provide at least one page number, e.g. pages='3,6,8'.";

        var ext = Path.GetExtension(resolved.FileName);
        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);
        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");
        var tempOutputDir = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempOutputDir);

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var outputPattern = Path.Combine(tempOutputDir, $"{baseName}_{{0}}.{{1}}");
            var splitOptions = new SplitOptions(outputPattern, pageNumbers);

            using var merger = password != null
                ? new GroupDocs.Merger.Merger(tempInput, new LoadOptions(password))
                : new GroupDocs.Merger.Merger(tempInput);

            merger.Split(splitOptions);

            var outputFiles = Directory.GetFiles(tempOutputDir).OrderBy(f => f).ToList();
            var savedPaths = new List<string>();
            foreach (var outputFile in outputFiles)
            {
                var bytes = await File.ReadAllBytesAsync(outputFile);
                var savedPath = await storage.WriteFileAsync(Path.GetFileName(outputFile), bytes, rewrite: false);
                savedPaths.Add(savedPath);
            }

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            var fileList = string.Join("\n", savedPaths);
            return $"{prefix}Split '{resolved.FileName}' into {savedPaths.Count} file(s):\n{fileList}";
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (Directory.Exists(tempOutputDir)) Directory.Delete(tempOutputDir, recursive: true);
        }
    }
}
