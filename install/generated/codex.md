# Codex CLI (OpenAI)

```bash
codex mcp add groupdocs-total -- docker run --rm -i -v /path/to/documents:/data ghcr.io/groupdocs-total/total-net-mcp:latest
```

Or add to `~/.codex/config.toml`:

```toml
[mcp_servers.groupdocs-total]
command = "docker"
args = ["run", "--rm", "-i", "-v", "/path/to/documents:/data", "ghcr.io/groupdocs-total/total-net-mcp:latest"]
```

Pin a version by replacing `:latest` with `:26.7.2` in the image tag.
