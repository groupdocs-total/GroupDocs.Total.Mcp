# GroupDocs.Total MCP Server

Unified MCP server that exposes the full [GroupDocs.Total](https://products.groupdocs.com/total) document-processing toolset — annotation, comparison, conversion, markdown, merging, metadata, parsing, redaction, signing, watermarking, plus cross-product document inspection and page preview — as AI-callable tools for Claude, Cursor, GitHub Copilot, and other MCP agents. Designed to be used **standalone** — attaching the individual product MCPs (GroupDocs.Annotation.Mcp, GroupDocs.Signature.Mcp, …) alongside is unnecessary and would duplicate tools.

## Installation

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

**Run directly with `dnx` (recommended — no install step):**

```bash
dnx GroupDocs.Total.Mcp --yes
```

Pulls the latest stable release on every invocation. To pin to a specific
version (recommended for shared configs and CI), append `@<version>`:

```bash
dnx GroupDocs.Total.Mcp@26.5.0 --yes
```

**Or install as a global dotnet tool:**

```bash
dotnet tool install -g GroupDocs.Total.Mcp
groupdocs-total-mcp
```

**Or run via Docker:**

```bash
docker run --rm -i \
  -v $(pwd)/documents:/data \
  ghcr.io/groupdocs-total/total-net-mcp:latest
```

## Native prerequisites

Several bundled engines (Annotation, Watermark, Signature, Viewer, Conversion,
Comparison) rasterise document pages or signature/annotation glyphs via
`System.Drawing` (GDI+). When you run the server **natively** (via `dnx` or
the global dotnet tool) on Linux or macOS, install the native `libgdiplus`
library and a fonts package first:

| Platform | Setup |
|---|---|
| Windows | Nothing — GDI+ is built into the OS. |
| Linux | `sudo apt-get install -y libgdiplus libfontconfig1 ttf-mscorefonts-installer` |
| macOS | `brew install mono-libgdiplus` |
| Docker | Nothing — the image already bundles libgdiplus, libfontconfig1, and ttf-mscorefonts-installer. |

Skipping this on Linux/macOS surfaces as `DllNotFoundException: libgdiplus` in
the tool response. The simplest zero-setup option on Linux/macOS is the
**Docker image**.

## Available MCP Tools (38 total)

Tools follow `{Product}{Verb}{Noun}` PascalCase to disambiguate across domains. Cross-product tools stay short.

### Cross-product (2)

| Tool | Description |
|---|---|
| `GetDocumentInfo` | File type, page count, size as JSON for any document (170+ formats via Parser engine) |
| `GetDocumentPageImage` | Render up to 5 pages as inline PNG images (Viewer engine, 170+ formats) |

### Annotation (8)

| Tool | Description |
|---|---|
| `AnnotationAddAnnotation` | Add textfield / area / point / arrow / highlight / underline / strikeout annotation |
| `AnnotationGetAnnotations` | List all annotations as JSON (id, type, message, page, box, user, replies) |
| `AnnotationUpdateAnnotation` | Modify an existing annotation's message and/or bounding box by id |
| `AnnotationRemoveAnnotations` | Remove annotations by id list, or all |
| `AnnotationAddReply` | Add a reply / comment thread to an existing annotation |
| `AnnotationRemoveReplies` | Remove replies by id, by user name, or all |
| `AnnotationImportAnnotations` | Import annotations from XML or another annotated document |
| `AnnotationExportAnnotations` | Extract annotations to XML (re-importable via Import) |

### Signature (7)

| Tool | Description |
|---|---|
| `SignatureSign` | Sign with text / qrcode / barcode / digital certificate signature |
| `SignatureVerify` | Verify signatures, return JSON validity report |
| `SignatureSearchTextSignatures` | Find embedded text signatures with optional substring filter |
| `SignatureSearchBarcodes` | Find barcode signatures with optional decoded-text filter |
| `SignatureSearchQrCodes` | Find QR code signatures with optional decoded-text filter |
| `SignatureSearchDigitalSignatures` | Find digital certificate signatures (signer, issuer, validity) |
| `SignatureSearchImageSignatures` | Find embedded image signatures, return as base64 PNGs |

### Comparison (1) · Conversion (2) · Markdown (2)

| Tool | Description |
|---|---|
| `ComparisonCompare` | Compare two documents, produce annotated diff document |
| `ConversionConvert` | Convert between PDF, Office, image, HTML, and 50+ more formats |
| `ConversionGetSupportedFormats` | List every output format the document can be converted TO |
| `MarkdownConvertToMarkdown` | Convert PDF / Office / EPUB / MOBI to clean Markdown |
| `MarkdownComposeFromMarkdown` | Compose DOCX / PDF / HTML from Markdown source |

### Merger (2) · Metadata (2)

| Tool | Description |
|---|---|
| `MergerMerge` | Combine up to 4 documents into one (PDF / Office / HTML / images) |
| `MergerSplit` | Split a document into separate files by page numbers |
| `MetadataReadMetadata` | Read all metadata properties (author, title, EXIF, XMP, IPTC, custom) as JSON |
| `MetadataRemoveMetadata` | Strip all metadata and save a clean copy |

### Parser (5) · Redaction (4) · Watermark (3)

| Tool | Description |
|---|---|
| `ParserExtractText` | Extract plain text (page-by-page or whole document) |
| `ParserExtractImages` | Extract embedded images and save them to storage |
| `ParserExtractTables` | Extract tables as Markdown or JSON |
| `ParserExtractMetadata` | Extract metadata fields (separate from MetadataReadMetadata; Parser-format-aware) |
| `ParserExtractBarcodes` | Find and decode embedded barcodes in document pages |
| `RedactionRedactText` | Redact text matching a regex with a replacement string |
| `RedactionRedactImageArea` | Black out a rectangular region of a document page |
| `RedactionRedactAnnotations` | Redact or delete annotations matching a regex |
| `RedactionEraseMetadata` | Strip metadata via the Redaction engine (handles formats Metadata can't) |
| `WatermarkAddWatermark` | Add a text watermark with configurable font, rotation, opacity |
| `WatermarkSearchWatermarks` | List existing watermarks (type, text, position, page) as JSON |
| `WatermarkRemoveWatermarks` | Remove watermarks by text filter or all |

## Example prompts

- "Convert contract.docx to PDF, then sign it with a QR code containing 'Signed by Alice', then watermark every page with 'CONFIDENTIAL'"
- "Compare old.pdf and new.pdf, list every annotation in the result, and preview page 1"
- "Extract all tables from report.xlsx as Markdown and redact every email address"
- "Read the metadata of every PDF in /uploads, then strip it, then preview the cleaned page 1 of each"
- "Find every barcode in invoice.pdf, then merge invoice.pdf + receipt.pdf into combined.pdf"

## Configuration

| Variable | Description | Default |
|---|---|---|
| `GROUPDOCS_MCP_STORAGE_PATH` | Base folder for input and output files | current directory |
| `GROUPDOCS_MCP_OUTPUT_PATH` | *(Optional)* separate folder for output files | `GROUPDOCS_MCP_STORAGE_PATH` |
| `GROUPDOCS_LICENSE_PATH` | Path to GroupDocs license file | (evaluation mode) |

## Usage with Claude Desktop

```json
{
  "mcpServers": {
    "groupdocs-total": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Total.Mcp", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/path/to/documents"
      }
    }
  }
}
```

> To pin to a specific version, replace `"GroupDocs.Total.Mcp"` with
> `"GroupDocs.Total.Mcp@26.5.0"` in `args`. Pinning is recommended for
> shared / committed configs to avoid surprise upgrades.

## Usage with VS Code / GitHub Copilot

NuGet.org generates a ready-to-use `mcp.json` snippet on the [package page](https://www.nuget.org/packages/GroupDocs.Total.Mcp).
Copy it directly into your `.vscode/mcp.json`.

Alternatively, add manually to `.vscode/mcp.json`:

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "storage_path",
      "description": "Base folder for input and output files.",
      "password": false
    }
  ],
  "servers": {
    "groupdocs-total": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Total.Mcp", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "${input:storage_path}"
      }
    }
  }
}
```

> Same pinning rule as above — swap `"GroupDocs.Total.Mcp"` for
> `"GroupDocs.Total.Mcp@26.5.0"` to lock to a specific release.

## Usage with Docker Compose

```bash
cd docker
docker compose up
```

Edit `docker/docker-compose.yml` to point volumes at your local documents folder.

## License

MIT — see [LICENSE](LICENSE)

<!-- mcp-name: io.github.groupdocs-total/groupdocs-total-mcp -->
