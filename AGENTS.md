# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

A **unified MCP server** for [GroupDocs.Total for .NET](https://products.groupdocs.com/total) — bundles 10 GroupDocs product engines (Annotation, Comparison, Conversion, Markdown, Merger, Metadata, Parser, Redaction, Signature, Watermark) plus cross-product document-info and page-preview tools, exposed via a single MCP endpoint to AI agents.

Published to NuGet as `GroupDocs.Total.Mcp` with the `McpServer` package type, and to `ghcr.io/groupdocs-total/total-net-mcp` + `docker.io/groupdocs/total-net-mcp` as a container image.

**Designed to be used standalone.** Do not attach the per-product MCPs (GroupDocs.Annotation.Mcp, GroupDocs.Signature.Mcp, …) alongside — they ship overlapping tools.

## MCP tools exposed (38 total)

Tool names follow `{Product}{Verb}{Noun}` PascalCase to disambiguate across the 10 bundled domains. Cross-product tools stay short. Every `[Description]` opens with `[GroupDocs.<Product>]` so AI agents can identify the owning product.

| Domain | # | Tools |
|---|--:|---|
| Cross-product | 2 | `GetDocumentInfo` (Parser engine), `GetDocumentPageImage` (Viewer engine) |
| Annotation | 8 | `AnnotationAddAnnotation`, `AnnotationGetAnnotations`, `AnnotationUpdateAnnotation`, `AnnotationRemoveAnnotations`, `AnnotationAddReply`, `AnnotationRemoveReplies`, `AnnotationImportAnnotations`, `AnnotationExportAnnotations` |
| Signature | 7 | `SignatureSign`, `SignatureVerify`, `SignatureSearchTextSignatures`, `SignatureSearchBarcodes`, `SignatureSearchQrCodes`, `SignatureSearchDigitalSignatures`, `SignatureSearchImageSignatures` |
| Comparison | 1 | `ComparisonCompare` |
| Conversion | 2 | `ConversionConvert`, `ConversionGetSupportedFormats` |
| Markdown | 2 | `MarkdownConvertToMarkdown`, `MarkdownComposeFromMarkdown` |
| Merger | 2 | `MergerMerge`, `MergerSplit` |
| Metadata | 2 | `MetadataReadMetadata`, `MetadataRemoveMetadata` |
| Parser | 5 | `ParserExtractText`, `ParserExtractImages`, `ParserExtractTables`, `ParserExtractMetadata`, `ParserExtractBarcodes` |
| Redaction | 4 | `RedactionRedactText`, `RedactionRedactImageArea`, `RedactionRedactAnnotations`, `RedactionEraseMetadata` |
| Watermark | 3 | `WatermarkAddWatermark`, `WatermarkSearchWatermarks`, `WatermarkRemoveWatermarks` |

**Skipped from the GroupDocs.Total bundle:** Editor / Assembly / Search MCP tools. Their parameter shapes (DataSource models, persistent index paths, schema-heavy edit DTOs) don't translate well to AI agent prompts.

## Folder layout

```
src/                                                       ← all projects + sln + Directory.Build.props
  GroupDocs.Total.Mcp/
    Program.cs                                             ← host bootstrap + stdio transport
    TotalLicenseManager.cs                                 ← applies one Total license to all 10 product engines
    Tools/
      Common/                                              ← cross-product tools
        GetDocumentInfoTool.cs
        GetDocumentPageImageTool.cs
      Annotation/                                          ← 8 Annotation tools (vendored from standalone)
      Signature/                                           ← 7 Signature tools (vendored from standalone)
      Comparison/    Conversion/    Markdown/
      Merger/        Metadata/      Parser/
      Redaction/     Watermark/                            ← vendored from framework subprojects
    .mcp/server.json                                       ← NuGet.org reads this to generate mcp.json snippet
    GroupDocs.Total.Mcp.csproj                             ← PackageType=McpServer + ToolCommandName
  GroupDocs.Total.Mcp.Tests/                               ← xUnit + Moq unit tests
  GroupDocs.Total.Mcp.sln
build/dependencies.props                                   ← single source of truth for all versions
docker/Dockerfile                                          ← multi-stage, libgdiplus + fonts on Linux
```

## Dependencies

- `GroupDocs.Total` (single metapackage) — transitively pulls all 14 GroupDocs engines including the 10 we expose.
- `GroupDocs.Mcp.Core` + `GroupDocs.Mcp.Local.Storage` — infrastructure NuGet packages.
- `ModelContextProtocol` 1.1.0 — MCP SDK for .NET.
- `Microsoft.Extensions.Hosting` — host builder for the stdio server.
- `SkiaSharp` + `SkiaSharp.NativeAssets.Linux.NoDependencies` — pinned to matching `3.119.4`. Total 26.6.0's transitive SkiaSharp is already 3.x but some older transitives may pull 2.x native assets; the explicit pin keeps Linux resolution coherent.

We deliberately do NOT reference the per-product `GroupDocs.{Product}.Mcp` NuGet packages — they are `PackAsTool=true` packages and cannot be consumed as libraries. Tools live as vendored sources inside this project.

## Why this is a unified MCP server, not a meta-attach

Standalone per-product MCPs (`GroupDocs.Annotation.Mcp`, `GroupDocs.Signature.Mcp`, …) each ship their own MCP server. Attaching multiple of them to one AI client gives the agent duplicate tools (each server exposes its own `GetDocumentInfo`, each registers as a distinct server in the client's UI). Total takes the opposite approach: ONE server, ONE tool catalog, all engines licensable from ONE `GroupDocs.Total.lic`. Use Total when you want the full toolkit; use a per-product MCP when you only need one product's tools.

## Pre-shipped pitfall remediations

- **Pitfall #16** (raw JSON, not TruncateText) — all JSON-returning tools (`GetDocumentInfo`, `MetadataReadMetadata`, `WatermarkSearchWatermarks`, `AnnotationGetAnnotations`, `ConversionGetSupportedFormats`, etc.) call `JsonSerializer.Serialize` directly.
- **Pitfall #18** (descriptive exception wrapping) — the 11 tools we wrote or rewrote (cross-product, Annotation, Signature, Watermark Remove, Conversion GetSupportedFormats) have try/catch + `FormatException` helpers. The 20 framework-vendored tools (Comparison, Conversion Convert, Markdown ×2, Merger ×2, Metadata ×2, Parser ×5, Redaction ×4, Watermark Add/Search) inherit the framework's pre-Pitfall-#18 shape and surface engine errors via MCP's generic wrapper. Tracked as a known finding for the post-publish review pass.

## Native-deps note

On Linux, several bundled engines (Annotation, Watermark, Signature, Viewer, Conversion, Comparison) rasterise pages via `System.Drawing.Common` — requires `libgdiplus` + `libfontconfig1` + `ttf-mscorefonts-installer` (with debconf EULA accept + `fc-cache`). The `System.Drawing.EnableUnixSupport` runtime host config option is set in the csproj.

## House rules

1. **Tool descriptions are AI-facing** — open with `[GroupDocs.<Product>]`, then state what it does, supported formats, response shape, failure prefix.
2. **Never add new env vars beyond** `GROUPDOCS_MCP_STORAGE_PATH`, `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH` without updating `server.json`, `docker-compose.yml`, and `README.md` together.
3. **Tests use xUnit + Moq** — mock `IFileResolver`, `IFileStorage`, `ILicenseManager`, `OutputHelper`.
4. **Changelog entries required** — any PR that changes behaviour adds `changelog/NNN-slug.md`.
5. **Target framework is `net10.0` only**.
6. **Cross-product naming is intentional** — never collapse `AnnotationGetAnnotations` to just `GetAnnotations`; the namespacing keeps the agent grounded in which product it's invoking.

## Release flow

See [RELEASE.md](RELEASE.md) for the exact per-release checklist.
