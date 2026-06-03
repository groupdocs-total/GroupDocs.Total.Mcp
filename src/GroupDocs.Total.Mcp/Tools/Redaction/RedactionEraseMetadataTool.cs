using System.ComponentModel;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Redaction;
using GroupDocs.Redaction.Options;
using GroupDocs.Redaction.Redactions;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Redaction;

[McpServerToolType]
public static class RedactionEraseMetadataTool
{
    [McpServerTool, Description(
        "[GroupDocs.Redaction] Erases metadata fields from a document (author, title, company, keywords, dates, etc.) and saves the cleaned file to storage. " +
        "Use to remove personally identifiable or confidential information embedded in document properties before sharing. " +
        "Call this tool whenever the user asks to erase, strip, clean, or remove metadata from a document. " +
        "Accepted fields (comma-separated): All, Author, Title, Subject, Category, Keywords, Description, Creator, Producer, " +
        "CreatedTime, LastPrinted, LastSavedTime, TotalEditingTime, NameOfApplication, Manager, Company, ContentStatus, " +
        "Version, Revision, HyperlinkBase, ContentType, Template. Default is All. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> RedactionEraseMetadata(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Comma-separated metadata fields to erase (default: 'All'). E.g. 'Author,Company,CreatedTime'")] string fields = "All",
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var filters = ParseFilters(fields);

        var loadOptions = new LoadOptions { Password = password };
        using var redactor = new Redactor(resolved.Stream, loadOptions);

        var result = redactor.Apply(new EraseMetadataRedaction(filters));
        if (result.Status == RedactionStatus.Failed)
        {
            var errors = result.RedactionLog
                .Select(e => e.Result.ErrorMessage)
                .Where(m => !string.IsNullOrEmpty(m));
            return $"Metadata erasure failed: {string.Join("; ", errors)}";
        }

        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);
        var ext = Path.GetExtension(resolved.FileName);
        var outputName = $"{baseName}_redacted{ext}";

        var ms = new MemoryStream();
        try
        {
            redactor.Save(ms, new RasterizationOptions { Enabled = false });

            var savedPath = await storage.WriteFileAsync(outputName, ms.ToArray(), rewrite: true);
            var line = await output.BuildFileOutputAsync(savedPath, $"Metadata erased (fields: {fields}, status: {result.Status})");

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return prefix + line;
        }
        finally
        {
            await ms.DisposeAsync();
        }
    }

    private static MetadataFilters ParseFilters(string fields)
    {
        if (fields.Equals("All", StringComparison.OrdinalIgnoreCase))
            return MetadataFilters.All;

        var result = (MetadataFilters)0;
        foreach (var part in fields.Split(','))
        {
            if (Enum.TryParse<MetadataFilters>(part.Trim(), ignoreCase: true, out var filter))
                result |= filter;
        }
        return result == (MetadataFilters)0 ? MetadataFilters.All : result;
    }
}
