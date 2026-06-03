using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GroupDocs.Total.Mcp;

/// <summary>
/// Applies a GroupDocs.Total license to every bundled product's License class.
/// One license file covers all products, but each product engine still needs
/// its own License.SetLicense call to leave evaluation mode. We try each
/// product in a try/catch so one engine's failure (DLL load, version mismatch)
/// doesn't block the others.
/// </summary>
public class TotalLicenseManager : LicenseManager
{
    private readonly ILogger<LicenseManager> _logger;

    public TotalLicenseManager(IOptions<McpConfig> config, ILogger<LicenseManager> logger)
        : base(config, logger)
    {
        _logger = logger;
    }

    protected override void SetLicenseFromPath(string licensePath)
    {
        // 11 bundled product engines (Editor / Assembly / Search skipped per scope).
        // Conversion uses an env-var convention (GROUPDOCS_LIC_PATH) rather than a
        // License() class — kept verbatim from the framework subproject.
        TrySet(() => new GroupDocs.Annotation.License().SetLicense(licensePath),  "Annotation");
        TrySet(() => new GroupDocs.Comparison.License().SetLicense(licensePath),  "Comparison");
        TrySet(() => Environment.SetEnvironmentVariable("GROUPDOCS_LIC_PATH", licensePath), "Conversion");
        TrySet(() => new GroupDocs.Markdown.License().SetLicense(licensePath),    "Markdown");
        TrySet(() => new GroupDocs.Merger.License().SetLicense(licensePath),      "Merger");
        TrySet(() => new GroupDocs.Metadata.License().SetLicense(licensePath),    "Metadata");
        TrySet(() => new GroupDocs.Parser.License().SetLicense(licensePath),      "Parser");
        TrySet(() => new GroupDocs.Redaction.License().SetLicense(licensePath),   "Redaction");
        TrySet(() => new GroupDocs.Signature.License().SetLicense(licensePath),   "Signature");
        TrySet(() => new GroupDocs.Viewer.License().SetLicense(licensePath),      "Viewer");
        TrySet(() => new GroupDocs.Watermark.License().SetLicense(licensePath),   "Watermark");
    }

    private void TrySet(Action setter, string productName)
    {
        try
        {
            setter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set license for GroupDocs.{Product}", productName);
        }
    }
}
