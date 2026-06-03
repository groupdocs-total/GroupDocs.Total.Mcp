using GroupDocs.Merger;
using System.ComponentModel;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Merger;

[McpServerToolType]
public static class MergerMergeTool
{
    [McpServerTool, Description(
        "[GroupDocs.Merger] Merges 2–4 documents into a single file and saves the result to storage. Supports PDF, DOCX, XLSX, PPTX and more. " +
        "Call this tool immediately whenever the user asks to merge, combine, or join documents together. " +
        "Do NOT pre-check whether files exist — just pass the filenames the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> MergerMerge(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        [Description("First document")] FileInput file1,
        [Description("Second document")] FileInput file2,
        [Description("Third document (optional)")] FileInput? file3 = null,
        [Description("Fourth document (optional)")] FileInput? file4 = null)
    {
        licenseManager.SetLicense();

        var inputs = new List<FileInput> { file1, file2 };
        if (file3 != null) inputs.Add(file3);
        if (file4 != null) inputs.Add(file4);

        var tempFiles = new List<string>();
        var resolvedNames = new List<string>();
        var tempOutput = string.Empty;

        try
        {
            foreach (var input in inputs)
            {
                using var resolved = await resolver.ResolveAsync(input);
                resolvedNames.Add(resolved.FileName);
                var tempPath = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
                await using (var fs = File.Create(tempPath))
                    await resolved.Stream.CopyToAsync(fs);
                tempFiles.Add(tempPath);
            }

            var ext = Path.GetExtension(resolvedNames[0]);
            var outputName = $"{Path.GetFileNameWithoutExtension(resolvedNames[0])}_merged{ext}";
            tempOutput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");

            using (var merger = new GroupDocs.Merger.Merger(tempFiles[0]))
            {
                for (int i = 1; i < tempFiles.Count; i++)
                    merger.Join(tempFiles[i]);
                merger.Save(tempOutput);
            }

            var bytes = await File.ReadAllBytesAsync(tempOutput);
            var savedPath = await storage.WriteFileAsync(outputName, bytes, rewrite: false);

            var names = string.Join(" + ", resolvedNames);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return await output.BuildFileOutputAsync(savedPath, $"{prefix}Merged {names} into '{outputName}'");
        }
        finally
        {
            foreach (var t in tempFiles)
                if (File.Exists(t)) File.Delete(t);
            if (!string.IsNullOrEmpty(tempOutput) && File.Exists(tempOutput))
                File.Delete(tempOutput);
        }
    }
}
