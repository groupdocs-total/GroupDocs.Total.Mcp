using System.ComponentModel;
using System.Text;
using GroupDocs.Markdown;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Markdown;

[McpServerToolType]
public static class MarkdownComposeFromMarkdownTool
{
    [McpServerTool, Description(
        "[GroupDocs.Markdown] Composes a document (DOCX, PDF, HTML, RTF, ODT, etc.) FROM Markdown. " +
        "Provide the Markdown either as inline text ('markdown' parameter) OR as a file from storage ('sourceFile' parameter — pointing to an existing .md). " +
        "The target format is inferred from the extension of 'outputFileName'. " +
        "Call this tool whenever the user asks to export, render, compose, or convert Markdown to a document format like Word, PDF, or HTML.")]
    public static async Task<string> MarkdownComposeFromMarkdown(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        [Description("Output filename whose extension determines the target format, e.g. 'report.docx', 'readme.pdf', 'notes.html'")] string outputFileName,
        [Description("Inline Markdown text. Leave empty when passing 'sourceFile' instead.")] string? markdown = null,
        [Description("Source Markdown file from storage (used when 'markdown' is not provided)")] FileInput? sourceFile = null)
    {
        licenseManager.SetLicense();

        if (string.IsNullOrWhiteSpace(outputFileName))
            return "Output filename is required (must include extension, e.g. 'readme.docx').";

        string markdownText;
        if (!string.IsNullOrEmpty(markdown))
        {
            markdownText = markdown;
        }
        else if (sourceFile != null && (sourceFile.FilePath != null || sourceFile.FileContent != null))
        {
            using var resolved = await resolver.ResolveAsync(sourceFile);
            using var reader = new StreamReader(resolved.Stream, Encoding.UTF8);
            markdownText = await reader.ReadToEndAsync();
        }
        else
        {
            return "Provide either 'markdown' (inline text) or 'sourceFile' (existing .md file in storage).";
        }

        var targetFormat = InferFormat(outputFileName);
        if (targetFormat == FileFormat.Unknown)
            return $"Cannot infer output format from '{outputFileName}'. Supported extensions include: .docx, .doc, .rtf, .odt, .pdf, .html, .epub, .mobi, .txt, .md.";

        var exportOptions = new ExportOptions(targetFormat);

        using var ms = new MemoryStream();
        try
        {
            MarkdownConverter.FromMarkdownString(markdownText, ms, exportOptions);
        }
        catch (NotImplementedException)
        {
            return "Markdown-to-document composition is not yet implemented in this version of GroupDocs.Markdown. " +
                   "For now, use the GroupDocs.Conversion MCP (convert tool) with a .md source file to produce DOCX, PDF, or HTML.";
        }
        catch (GroupDocsMarkdownException ex)
        {
            return $"Composition failed: {ex.Message}";
        }

        var savedPath = await storage.WriteFileAsync(outputFileName, ms.ToArray(), rewrite: true);

        var description = $"Composed '{outputFileName}' from Markdown ({targetFormat})";
        var prefix = licenseManager.IsLicensed
            ? string.Empty
            : "[Evaluation mode] Output may include watermarks or be limited.\n\n";

        return prefix + await output.BuildFileOutputAsync(savedPath, description);
    }

    private static FileFormat InferFormat(string outputFileName)
    {
        var ext = Path.GetExtension(outputFileName).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "docx" => FileFormat.Docx,
            "doc" => FileFormat.Doc,
            "docm" => FileFormat.Docm,
            "dot" => FileFormat.Dot,
            "dotx" => FileFormat.Dotx,
            "dotm" => FileFormat.Dotm,
            "rtf" => FileFormat.Rtf,
            "odt" => FileFormat.Odt,
            "ott" => FileFormat.Ott,
            "pdf" => FileFormat.Pdf,
            "epub" => FileFormat.Epub,
            "mobi" => FileFormat.Mobi,
            "txt" => FileFormat.Txt,
            "md" or "markdown" => FileFormat.Md,
            "xlsx" => FileFormat.Xlsx,
            "xls" => FileFormat.Xls,
            "xlsb" => FileFormat.Xlsb,
            "xlsm" => FileFormat.Xlsm,
            "csv" => FileFormat.Csv,
            "tsv" => FileFormat.Tsv,
            "ods" => FileFormat.Ods,
            "ots" => FileFormat.Ots,
            "chm" => FileFormat.Chm,
            _ => FileFormat.Unknown
        };
    }
}
