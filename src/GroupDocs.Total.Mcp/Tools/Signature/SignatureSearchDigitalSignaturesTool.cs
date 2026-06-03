using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using ModelContextProtocol.Server;

namespace GroupDocs.Total.Mcp.Tools.Signature;

[McpServerToolType]
public static class SignatureSearchDigitalSignaturesTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "[GroupDocs.Signature] Searches a document for digital certificate signatures and returns details for each: " +
        "signer name, issuer, certificate serial number, validity period, sign timestamp, validity status, " +
        "and any comments or reason attached to the signature. " +
        "Supports PDF and Office documents (DOCX, XLSX, PPTX). " +
        "Do NOT pre-check whether the file exists — pass the filename the user provided directly. " +
        "Returns a JSON object with `found` (count) and `signatures` (array with `signTime`, `isValid`, `comments`, `thumbprint`, and a nested `certificate` object). " +
        "On failure, the response text starts with 'Digital signature search failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> SignatureSearchDigitalSignatures(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Password for protected documents.")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var loadOptions = password != null ? new LoadOptions { Password = password } : null;
            using var sig = loadOptions != null
                ? new GroupDocs.Signature.Signature(tempInput, loadOptions)
                : new GroupDocs.Signature.Signature(tempInput);

            var signatures = sig.Search<DigitalSignature>(SignatureType.Digital);

            var prefix = licenseManager.IsLicensed
                ? string.Empty
                : "[Evaluation mode] Results may be limited.\n\n";

            if (signatures.Count == 0)
                return $"{prefix}No digital signatures found in '{resolved.FileName}'.";

            var results = signatures.Select(d => new
            {
                signTime = d.SignTime,
                isValid = d.IsValid,
                comments = d.Comments,
                thumbprint = d.Thumbprint,
                certificate = d.Certificate == null ? null : new
                {
                    subject = d.Certificate.Subject,
                    issuer = d.Certificate.Issuer,
                    serialNumber = d.Certificate.SerialNumber,
                    validFrom = d.Certificate.NotBefore,
                    validTo = d.Certificate.NotAfter,
                    thumbprint = d.Certificate.Thumbprint
                }
            }).ToArray();

            // Pitfall #16: return raw JSON.
            return prefix + JsonSerializer.Serialize(new { found = signatures.Count, signatures = results }, JsonOptions);
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
        sb.Append($"Digital signature search failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
