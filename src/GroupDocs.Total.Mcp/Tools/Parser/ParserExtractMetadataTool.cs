using System.ComponentModel;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Parser.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Parser;

[McpServerToolType]
public static class ParserExtractMetadataTool
{
    [McpServerTool, Description(
        "[GroupDocs.Parser] Extracts metadata from a document file (author, title, creation date, page count, etc.) and returns it as JSON. " +
        "Call this tool immediately whenever the user asks to extract metadata or get document properties from a file. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> ParserExtractMetadata(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var loadOptions = password != null ? new LoadOptions(password) : new LoadOptions();

        using var parser = new GroupDocs.Parser.Parser(resolved.Stream, loadOptions);
        var metadata = parser.GetMetadata();

        if (metadata == null)
            return "No metadata found in this document.";

        var dict = metadata.ToDictionary(m => m.Name, m => m.Value);
        return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
    }
}
