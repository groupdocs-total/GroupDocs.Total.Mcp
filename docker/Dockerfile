FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GroupDocs.Total.Mcp/GroupDocs.Total.Mcp.csproj \
    -c Release -o /app --no-self-contained

FROM base AS final

# Native graphics deps that multiple bundled GroupDocs engines need on Linux.
# Annotation, Watermark, Signature, Viewer, Conversion, and Comparison
# rasterise document pages / signature glyphs / watermarks via
# System.Drawing.Common — that path P/Invokes libgdiplus (the GDI+
# implementation) and uses libfontconfig1 to discover fonts. Without both,
# any tool that touches a page-rendering path throws PlatformNotSupportedException
# on Linux. Because the engines RENDER text glyphs (annotation messages baked
# onto pages, signed-text overlays, watermark text, comment threads, etc.),
# Microsoft core fonts (Arial / Times New Roman) must also be installed —
# ttf-mscorefonts-installer ships them, with debconf-set-selections to auto-accept
# the EULA and fc-cache to refresh the font cache so freshly-installed faces are
# discoverable. Pair this with <RuntimeHostConfigurationOption Include=
# "System.Drawing.EnableUnixSupport" Value="true" /> in the csproj.
USER root
RUN apt-get update \
 && echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" | debconf-set-selections \
 && apt-get install -y --no-install-recommends \
      libgdiplus libfontconfig1 ttf-mscorefonts-installer \
 && fc-cache -f -v > /dev/null \
 && rm -rf /var/lib/apt/lists/*

# OCI image labels. VERSION is supplied by the publish workflow via
# --build-arg VERSION=YY.M.N. Default 'dev' makes local `docker build` runs
# self-identify instead of inheriting the base image's misleading version.
ARG VERSION=dev
LABEL org.opencontainers.image.title="GroupDocs.Total.Mcp" \
      org.opencontainers.image.description="Unified MCP server exposing GroupDocs.Total for .NET as 38 AI-callable tools across 10 product domains (Annotation, Comparison, Conversion, Markdown, Merger, Metadata, Parser, Redaction, Signature, Watermark) + cross-product GetDocumentInfo and GetDocumentPageImage. One MCP endpoint, no per-product server attachments needed." \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/groupdocs-total/GroupDocs.Total.Mcp" \
      org.opencontainers.image.url="https://www.nuget.org/packages/GroupDocs.Total.Mcp" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.vendor="Aspose Pty Ltd"

COPY --from=build /app .

ENV GROUPDOCS_MCP_STORAGE_PATH=/data
VOLUME /data

USER $APP_UID

ENTRYPOINT ["dotnet", "GroupDocs.Total.Mcp.dll"]
