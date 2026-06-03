---
id: 001
date: 2026-05-31
version: 26.5.0
type: feature
---

# Initial release of GroupDocs.Total.Mcp

## What changed

- New unified MCP server packaging the full GroupDocs.Total document toolset for AI agents, published as `GroupDocs.Total.Mcp` on NuGet (PackageType=`McpServer`, ToolCommandName=`groupdocs-total-mcp`) and as `ghcr.io/groupdocs-total/total-net-mcp` + `docker.io/groupdocs/total-net-mcp` Docker images. Target framework `net10.0`, `dnx`-launchable.
- **38 tools across 10 product domains** in a single MCP endpoint:
  - **Cross-product (2)**: `GetDocumentInfo` (Parser engine, 170+ formats), `GetDocumentPageImage` (Viewer engine, inline PNG content blocks, 1–5 pages).
  - **Annotation (8)**: `AnnotationAddAnnotation`, `AnnotationGetAnnotations`, `AnnotationUpdateAnnotation`, `AnnotationRemoveAnnotations`, `AnnotationAddReply`, `AnnotationRemoveReplies`, `AnnotationImportAnnotations`, `AnnotationExportAnnotations` (vendored from `GroupDocs.Annotation.Mcp@26.5.0`).
  - **Signature (7)**: `SignatureSign`, `SignatureVerify`, `SignatureSearchTextSignatures`, `SignatureSearchBarcodes`, `SignatureSearchQrCodes`, `SignatureSearchDigitalSignatures`, `SignatureSearchImageSignatures` (vendored from `GroupDocs.Signature.Mcp@26.5.0`).
  - **Comparison (1)**: `ComparisonCompare`.
  - **Conversion (2)**: `ConversionConvert`, `ConversionGetSupportedFormats` (rewritten to actually list possible conversion targets — the framework's variant was misnamed and returned document info).
  - **Markdown (2)**: `MarkdownConvertToMarkdown`, `MarkdownComposeFromMarkdown`.
  - **Merger (2)**: `MergerMerge`, `MergerSplit`.
  - **Metadata (2)**: `MetadataReadMetadata`, `MetadataRemoveMetadata`.
  - **Parser (5)**: `ParserExtractText`, `ParserExtractImages`, `ParserExtractTables`, `ParserExtractMetadata`, `ParserExtractBarcodes`.
  - **Redaction (4)**: `RedactionRedactText`, `RedactionRedactImageArea`, `RedactionRedactAnnotations`, `RedactionEraseMetadata`.
  - **Watermark (3)**: `WatermarkAddWatermark`, `WatermarkSearchWatermarks`, `WatermarkRemoveWatermarks` (Remove is new in this MCP — written from the engine API; framework subproject had only Add + Search).
- **Naming convention**: `{Product}{Verb}{Noun}` PascalCase prefix for every per-product tool so AI agents disambiguate across the 10 domains. Cross-product tools stay short. Each `[McpServerTool, Description]` opens with `[GroupDocs.<Product>]` to anchor the agent in the owning product.
- **Skipped from bundle**: Editor, Assembly, Search engines. Their parameter shapes (DataSource models, persistent index paths, schema-heavy edit DTOs) don't translate well to AI agent prompts. Users who need them should attach the standalone per-product MCPs.
- **Engine bundle**: single `<PackageReference Include="GroupDocs.Total" Version="26.4.0" />` — transitively pulls all 14 GroupDocs product engines. We deliberately do NOT reference per-product `GroupDocs.{Product}.Mcp` NuGets — they are `PackAsTool=true` packages, not consumable as libraries. Tools live as vendored sources inside this project.
- **TotalLicenseManager** applies one `GroupDocs.Total.lic` to every bundled product's `License` class. Each product's `License.SetLicense(...)` call is wrapped in try/catch so one failing engine (DLL load, version mismatch) doesn't block the others.
- **SkiaSharp 3.119.4 explicit pin** + matching `SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.4` to override potential 2.x native-asset transitives. `System.Drawing.EnableUnixSupport` set in csproj. Native dependencies on Linux: `libgdiplus` + `libfontconfig1` + `ttf-mscorefonts-installer` (annotation, watermark, signature, viewer, conversion, comparison engines all rasterise pages via System.Drawing).
- **Pitfall #18 (descriptive exception wrapping)**: the 11 tools we wrote / rewrote in this release (cross-product 2, Annotation 8, Signature 7 — but those latter 15 inherited from already-Pitfall-#18 standalones; plus Watermark Remove 1, Conversion GetSupportedFormats 1) ship with try/catch + `FormatException` helpers. The 20 framework-vendored tools (Comparison, Conversion Convert, Markdown ×2, Merger ×2, Metadata ×2, Parser ×5, Redaction ×4, Watermark Add/Search) inherit the framework's pre-Pitfall-#18 shape — known finding to address in a follow-up release.
- **Pitfall #16 (raw JSON, not TruncateText)** remediated at clone time on `MetadataReadMetadataTool` and `WatermarkSearchWatermarksTool` which the framework versions piped through `OutputHelper.TruncateText` (would break JSON parsing).
- **Unit tests**: xUnit + Moq, one test class per tool, **100 tests total** (37 generic Resolver-throws / SetsLicense / DoesNotWriteToStorage shape templates + 3 hand-written tests for `MarkdownComposeFromMarkdown` which has a non-standard signature).
- Environment variables: `GROUPDOCS_MCP_STORAGE_PATH`, optional `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH`.

## Why

GroupDocs.Total customers pay for the full toolset bundle. They expect one server, one license, one tool catalog — not 14 individual MCP server attachments. This MCP exposes the most-AI-friendly subset of each bundled engine through a unified endpoint while keeping engine choice transparent via the `{Product}` prefix in tool names.

## Migration / impact

First release of this repository — no migration. CalVer `26.5.0` (YY.M.N, first month-release uses N=0); engine `GroupDocs.Total 26.4.0`.

Known follow-up: apply Pitfall #18 wrappers to the 20 framework-vendored tools. Tracked in the verification report.
