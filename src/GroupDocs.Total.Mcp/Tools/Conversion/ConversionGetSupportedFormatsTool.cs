using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Conversion;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Conversion;

[McpServerToolType]
public static class ConversionGetSupportedFormatsTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Conversion] Lists every output format the document can be converted TO via ConversionConvert. " +
        "Returns the source format plus the full list of viable target extensions (pdf, docx, xlsx, pptx, html, jpg, png, …). " +
        "Use BEFORE ConversionConvert when the user asks 'what can I convert this to?' or to validate a target extension is supported. " +
        "Do NOT pre-check whether files exist — pass the filename the user provided directly. " +
        "Returns a JSON object with `source` (extension), `count`, and `targets` (array of supported target extensions). " +
        "On failure, the response text starts with 'Supported-formats lookup failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> ConversionGetSupportedFormats(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        // Converter requires a seekable file path on disk.
        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            using var converter = new Converter(tempInput);
            var possible = converter.GetPossibleConversions();

            // possible is a PossibleConversions object. Its .All property exposes
            // every TargetConversion in the source-format's conversion group.
            // Extract unique target extensions via reflection — keeps this code
            // resilient to engine type-name changes (TargetConversion vs Format
            // namespaces have shuffled between engine major versions).
            var targets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var allProp = possible.GetType().GetProperty("All");
            if (allProp?.GetValue(possible) is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is null) continue;
                    var formatProp = item.GetType().GetProperty("Format");
                    var format = formatProp?.GetValue(item);
                    var extProp = format?.GetType().GetProperty("Extension");
                    if (extProp?.GetValue(format) is string ext && !string.IsNullOrEmpty(ext))
                        targets.Add(ext.TrimStart('.').ToLowerInvariant());
                }
            }

            // Pitfall #16: return raw JSON, never via OutputHelper.TruncateText.
            return JsonSerializer.Serialize(new
            {
                source  = Path.GetExtension(resolved.FileName).TrimStart('.').ToLowerInvariant(),
                count   = targets.Count,
                targets = targets.ToArray(),
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Supported-formats lookup failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
