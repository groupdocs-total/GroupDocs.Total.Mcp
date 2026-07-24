# Claude Code

```bash
claude mcp add groupdocs-total -- docker run --rm -i -v /path/to/documents:/data ghcr.io/groupdocs-total/total-net-mcp:latest
```

With a GroupDocs license:

```bash
claude mcp add groupdocs-total -- docker run --rm -i -v /path/to/documents:/data -v /path/to/license-folder:/license -e GROUPDOCS_LICENSE_PATH=/license/GroupDocs.Total.lic ghcr.io/groupdocs-total/total-net-mcp:latest
```

Pin a version by replacing `:latest` with `:26.7.1` in the image tag.
