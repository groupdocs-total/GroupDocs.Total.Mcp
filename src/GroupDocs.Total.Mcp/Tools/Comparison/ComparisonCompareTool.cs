using System.ComponentModel;
using GroupDocs.Comparison.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Comparison;

[McpServerToolType]
public static class ComparisonCompareTool
{
    [McpServerTool, Description(
        "[GroupDocs.Comparison] Compares two documents and highlights the differences between them. " +
        "Call this tool immediately whenever the user asks to compare, diff, or check differences between two files. " +
        "Do NOT pre-check whether files exist — just pass the filenames the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found. " +
        "Returns a change count summary and saves the marked-up result document to storage.")]
    public static async Task<string> ComparisonCompare(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        [Description("Source (original) document — provide the filename as given by the user, e.g. 'source.pdf'")] FileInput sourceFile,
        [Description("Target (modified) document to compare against — provide the filename as given by the user, e.g. 'target.pdf'")] FileInput targetFile,
        [Description("Password for source document, if password-protected")] string? sourcePassword = null,
        [Description("Password for target document, if password-protected")] string? targetPassword = null)
    {
        licenseManager.SetLicense();
        using var source = await resolver.ResolveAsync(sourceFile);
        using var target = await resolver.ResolveAsync(targetFile);

        var outputName = $"{Path.GetFileNameWithoutExtension(source.FileName)}_compared{Path.GetExtension(source.FileName)}";

        using var outputMs = new MemoryStream();
        using var comparer = sourcePassword != null
            ? new GroupDocs.Comparison.Comparer(source.Stream, new LoadOptions { Password = sourcePassword })
            : new GroupDocs.Comparison.Comparer(source.Stream);

        comparer.Add(target.Stream, targetPassword != null
            ? new LoadOptions { Password = targetPassword }
            : new LoadOptions());

        comparer.Compare(outputMs);

        var changes = comparer.GetChanges();
        var summary = changes.Length > 0
            ? $"{changes.Length} change(s) detected"
            : "No changes detected";

        var savedPath = await storage.WriteFileAsync(outputName, outputMs.ToArray(), rewrite: false);

        var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
        var description = $"{prefix}Compared '{source.FileName}' vs '{target.FileName}' — {summary}";
        return await output.BuildFileOutputAsync(savedPath, description);
    }
}
