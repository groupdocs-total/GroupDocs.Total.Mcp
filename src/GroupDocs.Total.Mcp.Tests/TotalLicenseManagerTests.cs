using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Total.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GroupDocs.Total.Mcp.Tests;

public class TotalLicenseManagerTests
{
    [Fact]
    public void IsLicensed_WithoutLicensePath_ReturnsFalse()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new McpConfig());
        var manager = new TotalLicenseManager(options, NullLogger<LicenseManager>.Instance);

        Assert.False(manager.IsLicensed);
    }

    [Fact]
    public void SetLicense_WithoutLicensePath_DoesNotThrow()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new McpConfig());
        var manager = new TotalLicenseManager(options, NullLogger<LicenseManager>.Instance);

        var ex = Record.Exception(() => manager.SetLicense());
        Assert.Null(ex);
    }
}
