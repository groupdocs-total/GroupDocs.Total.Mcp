# Backlog & Known Issues

Running list of ideas, planned work, and known limitations for the
GroupDocs.Total MCP server. Grouped by topic. Terse on purpose — each line is
a ticket, not an essay. `[ ]` = open, `[x]` = shipped (kept for context).

**Current surface (26.9.0):** 38 tools across 10 product families, consistently family-prefixed
(`watermark_add_watermark`, `parser_extract_text`, `signature_sign`, …) plus shared
`get_document_info` / `get_document_page_image`.

**Channel: Docker/GHCR only (OCI).** The packed tool is ≈393 MiB, over NuGet.org's 250 MB limit,
so there is no `dnx` path. Registry entry is registered as `oci`.

---

## Confirmed defects — external audit, 2026-08-16

Source: black-box test round against `ghcr.io/groupdocs-total/total-net-mcp:latest`
(26.7.3, 1.75 GB, licensed), 46 family-wide defects reported and all 46 independently reproduced
with control calls. A later validation round found **zero false positives**.

`S#` = shared core (`GroupDocs.Mcp.Core`) · `M#` = this repo · `P#` = underlying product libraries

**Verdict: the bundle works and is a faithful aggregation, not a fork** — response wording and
behaviour match the standalone servers exactly. It therefore also **inherits every per-product
issue**. Nothing Total-specific is broken beyond the shared core items.

### Shared core — fixed once in `GroupDocs.Mcp.Core`, lands here on the next bump

- [ ] **S1** Passing `fileName` crashes any tool — **High**. *Proof:*
      `get_document_info {"file":{"fileName":"03_pages_text.pdf"}}` → opaque error; unhandled
      `ArgumentException` at `FileResolver.ResolveAsync` → `Tools/Common/GetDocumentInfoTool.cs:37`.
- [ ] **S2** Missing files return an opaque error — **High**; listing capped at 20 entries.
- [ ] **S3** `isError` is set on crashes but not on real failures — **Med**.
- [ ] **S2c** A missing **required parameter** is just as opaque — **Low in cause, high in impact
      here**. *Proof:* calling `conversion_convert` with `targetFormat` (the real name is `format`)
      returned only `An error occurred invoking 'conversion_convert'`, while stderr held the
      answer: `The arguments dictionary is missing a value for the required parameter 'format'.`
      Same for `merger_merge` without `file1`.
      *Impact:* **with 38 similarly-named tools, wrong parameter names are the most likely client
      mistake** — and the one message that would let an agent self-correct never arrives. Total is
      where this hurts most.
      *Fix:* the same Core call-tool boundary handler as S2 surfaces argument-validation messages
      as text. **P1 — highest Total-specific value, and it comes free with the Core fix.**

### Inherited product issues

Total is a faithful aggregation, so it carries the defects in the per-product backlogs. The ones
that matter most inside the bundle:

| Family | Inherited issue | Owner repo |
|---|---|---|
| Signature | QR payload corruption (data integrity); `verify` unusable; expired certs report valid | `GroupDocs.Signature.Mcp` |
| Redaction | `erase_metadata` + `redact_image_area` dead on PDF; zero-match reports success | `GroupDocs.Redaction.Mcp` |
| Annotation | cannot edit annotations in produced files; preview dead on Linux | `GroupDocs.Annotation.Mcp` |
| Watermark | search invents hyperlink watermarks, cannot see image ones | `GroupDocs.Watermark.Mcp` |
| Metadata | PPTX/PPT dead (missing native library) — **verify whether Total's image has it** | `GroupDocs.Metadata.Mcp` |
| Viewer / Markdown | out-of-range page silently "succeeds" | respective repos |

- [ ] **Re-run the per-product checks inside Total once the standalone fixes land** — especially
      Metadata PPTX, since packaging differs per image and Total's was never verified for that
      path. **P1**

---

## Known issues & limitations

- **Docker-first**: no NuGet/`dnx` channel by design (size). Any documentation or install snippet
  implying `dnx` is wrong for this product.
- Licensing fans out to 11 bundled engines via `TotalLicenseManager`, each in its own `try/catch`
  so one engine's failure does not block the others. Editor / Assembly / Search are out of scope.
- **Licensing quirk:** Conversion is licensed by setting the `GROUPDOCS_LIC_PATH` environment
  variable rather than a `License()` call (`TotalLicenseManager.cs:32`). **There is no env-var
  equivalent for metered keys** — the metered path must call
  `new GroupDocs.Conversion.Metered().SetMeteredKey(...)` directly.
- Output collisions dedup to `' (N)'`, matching the standalone servers.
- Wording is byte-identical to the standalone servers, so behaviour learned against one applies to
  the other. Preserve that on every change — it is the bundle's main value.

---

## Tools & functionality

- [ ] **S2c** surface missing-parameter messages (comes with the Core fix). **P1**
- [ ] Keep the 38-tool surface in lockstep with the standalone servers on every product bump.
      **P1**
- [ ] Consider a `list_families` / capability-discovery tool — with 38 tools, helping an agent
      narrow down is worth more here than anywhere else. **P2**

## Testing & CI

- [ ] **The companion test repo currently validates nothing.** Its harness launches
      `dnx GroupDocs.Total.Mcp@<ver>` from nuget.org — a package that was **never published**
      (Docker-first). Total's own CI documents this and *skips the test step*. **P1 — this is the
      single most important item in this file.**
- [ ] Replace it with container-based tests against the published GHCR image. **P1**
- [ ] **26 of the 38 tools were never exercised** in the audit (one spot check per family) —
      annotation, redaction and markdown families plus `merger_split` and the signature searches.
      **P1**
- [ ] Per-tool Linux smoke test in image CI — 38 calls, and the only way to catch packaging drift
      between Total's image and the standalone ones. **P1**
- [ ] Add the two mandatory probes: the **`fileName`-only form**, and a **missing file**. **P1**
- [ ] Parity test: assert Total's response wording matches the standalone server for a sample tool
      per family, so the "faithful aggregation" property is enforced rather than assumed. **P2**
- [ ] Fix the copy-pasted README in the Tests repo (describes Signature classes that do not exist
      here). **P2**

## Documentation & discoverability

- [ ] Make the Docker-only channel unmistakable in README and the Registry description. **P1**
- [ ] Licensing section covering the metered option once it ships, including the 11-engine
      fan-out. **P1**

## Platform & infra (longer-term)

- [ ] Metered licensing via `GroupDocs.Mcp.Core` — **11-engine fan-out**, mirroring
      `SetLicenseFromPath`, with the Conversion special case above. Plus `get_license_status`;
      decide whether it reports per-engine consumption or an aggregate. **P1**
- [ ] Image size (1.75 GB) — worth a layer audit at some point. **P2**
- [ ] HTTP/SSE transport for shared/team deploys (stdio stays default). **P2**

---

*Evidence: `TEMP_ThirdPartyAnalysis/total.md` (per-product findings),
`ALL-PRODUCTS-REPORT.md` (10-product sweep), `VALIDATION-REPORT.md` (the dnx-404 finding is
"New findings #1" there). Conventions: any behaviour change ships with a `changelog/NNN-*.md`
entry and a CalVer bump.*
