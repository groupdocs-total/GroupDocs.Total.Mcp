using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using Moq;
using Xunit;

namespace GroupDocs.Total.Mcp.Tests;

// MarkdownComposeFromMarkdown does not take a positional `file` parameter — it
// takes `outputFileName` first, then either inline `markdown` text or a
// `sourceFile` FileInput. To exercise the resolver-throws path we must pass
// sourceFile and let the tool call resolver.ResolveAsync(sourceFile).
public class MarkdownComposeFromMarkdownToolTests
{
    private readonly Mock<IFileResolver> _resolver = new();
    private readonly Mock<ILicenseManager> _licenseManager = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly OutputHelper _output;

    public MarkdownComposeFromMarkdownToolTests()
    {
        _output = new OutputHelper(_storage.Object, Microsoft.Extensions.Options.Options.Create(new McpConfig()));
    }

    [Fact]
    public async Task MarkdownComposeFromMarkdown_WhenResolverThrows_PropagatesException()
    {
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<FileInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("missing.md"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            GroupDocs.Total.Mcp.Tools.Markdown.MarkdownComposeFromMarkdownTool.MarkdownComposeFromMarkdown(
                _resolver.Object,
                _storage.Object,
                _licenseManager.Object,
                _output,
                "out.docx",
                markdown: null,
                sourceFile: new FileInput { FilePath = "missing.md" }));
    }

    [Fact]
    public async Task MarkdownComposeFromMarkdown_WhenResolverThrows_DoesNotWriteToStorage()
    {
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<FileInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("missing.md"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            GroupDocs.Total.Mcp.Tools.Markdown.MarkdownComposeFromMarkdownTool.MarkdownComposeFromMarkdown(
                _resolver.Object,
                _storage.Object,
                _licenseManager.Object,
                _output,
                "out.docx",
                markdown: null,
                sourceFile: new FileInput { FilePath = "missing.md" }));

        _storage.Verify(
            s => s.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkdownComposeFromMarkdown_EmptyInputs_ReturnsValidationMessage()
    {
        // Tool short-circuits on missing-output / missing-input WITHOUT invoking
        // resolver — exercises the validation-shortcut branch.
        var result = await GroupDocs.Total.Mcp.Tools.Markdown.MarkdownComposeFromMarkdownTool.MarkdownComposeFromMarkdown(
            _resolver.Object,
            _storage.Object,
            _licenseManager.Object,
            _output,
            outputFileName: "",
            markdown: null,
            sourceFile: null);

        Assert.False(string.IsNullOrWhiteSpace(result),
            "Tool should return a validation message when outputFileName is empty.");
    }
}
