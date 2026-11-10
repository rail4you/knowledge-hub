# ============================================================
# KnowledgeHub HttpApi.Host - Unified Dockerfile
# Multi-stage build:
#   1) build      : sdk:10.0   -> dotnet publish
#   2) final      : aspnet:10.0 runtime (no Java/Python/Node)
#   Office 文档预览转换由独立的 Gotenberg 容器负责（不再内置 LibreOffice），
#   本镜像仅保留 ffmpeg（视频处理，installed via apt）。
#
# Local dev:  docker build -f src/KnowledgeHub.HttpApi.Host/Dockerfile -t knowledgehub-api:dev .
# Production: docker build --platform linux/amd64 -f src/KnowledgeHub.HttpApi.Host/Dockerfile -t ... .
# ============================================================

# ----------------------------------------------------------
# Stage 1: build (dotnet publish)
# ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Pass proxy from docker build --build-arg (optional, used when building behind corporate proxy)
ARG HTTP_PROXY
ARG HTTPS_PROXY
ARG NO_PROXY
ENV HTTP_PROXY=${HTTP_PROXY} \
    HTTPS_PROXY=${HTTPS_PROXY} \
    NO_PROXY=${NO_PROXY}

# 1) NuGet config + MSBuild shared props (so restore is reproducible)
COPY NuGet.Config common.props ./

# 2) Copy only .csproj files first to maximize restore cache
COPY src/KnowledgeHub.HttpApi.Host/*.csproj          src/KnowledgeHub.HttpApi.Host/
COPY src/KnowledgeHub.HttpApi/*.csproj               src/KnowledgeHub.HttpApi/
COPY src/KnowledgeHub.Application/*.csproj           src/KnowledgeHub.Application/
COPY src/KnowledgeHub.Application.Contracts/*.csproj src/KnowledgeHub.Application.Contracts/
COPY src/KnowledgeHub.Domain/*.csproj                src/KnowledgeHub.Domain/
COPY src/KnowledgeHub.Domain.Shared/*.csproj         src/KnowledgeHub.Domain.Shared/
COPY src/KnowledgeHub.EntityFrameworkCore/*.csproj  src/KnowledgeHub.EntityFrameworkCore/
COPY src/KnowledgeHub.HttpApi.Client/*.csproj        src/KnowledgeHub.HttpApi.Client/
COPY src/KnowledgeHub.AI.Api/*.csproj                src/KnowledgeHub.AI.Api/

# 3) Restore (cached layer unless csproj/NuGet.Config/common.props change)
#    NUGET_HTTP_TIMEOUT prevents timeout on slow connections (default 100s → 300s)
RUN NUGET_HTTP_TIMEOUT=300 dotnet restore src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj

# 4) Copy the rest of the source and publish
COPY . .
RUN NUGET_HTTP_TIMEOUT=300 dotnet publish src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj \
        -c Release -o /app/publish --no-restore

# ----------------------------------------------------------
# Stage 2: runtime
# ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Minimal system packages.
# - locales: en_US.UTF-8
# - tzdata:  Asia/Shanghai (set via ENV below)
# - ffmpeg: video processing (installed via apt)
# - poppler-utils: pdftoppm 渲染 Office/PDF 首页封面缩略图、逐页图片流式预览（pdftoppm/pdfinfo）
# - qpdf: 转换后线性化 PDF（Fast Web View），低带宽流式首屏加速
# 注：已移除 libreoffice / 中文字体（PPTX/DOCX/XLSX → PDF 由 Gotenberg 容器转换，
#   中文字体在 Gotenberg 自定义镜像中提供）。
# Force fresh apt-get (avoid BuildKit cached network errors)
ARG CACHE_BUST=0
# APT 镜像：默认走官方源；国内网络（archive.ubuntu.com 不可达）构建时传
#   --build-arg APT_MIRROR=mirrors.aliyun.com
# 走国内源，否则 apt-get update 拉不到索引 → 报「Package 'locales' has no installation candidate」。
ARG APT_MIRROR=
RUN echo "cache_bust=$CACHE_BUST" \
    && if [ -n "${APT_MIRROR}" ]; then \
         # Ubuntu Noble 的源在 /etc/apt/sources.list.d/ubuntu.sources（不是 debian.sources），
         # 对该目录下所有 .sources / .list 一起替换，避免只改了一个文件导致索引仍走官方源。
         for f in /etc/apt/sources.list /etc/apt/sources.list.d/*.sources /etc/apt/sources.list.d/*.list; do \
           [ -f "$$f" ] || continue; \
           sed -i "s|http://archive.ubuntu.com/ubuntu|http://${APT_MIRROR}/ubuntu|g; s|http://security.ubuntu.com/ubuntu|http://${APT_MIRROR}/ubuntu|g; s|https://archive.ubuntu.com/ubuntu|http://${APT_MIRROR}/ubuntu|g; s|https://security.ubuntu.com/ubuntu|http://${APT_MIRROR}/ubuntu|g" "$$f"; \
         done; \
         echo "apt mirror -> ${APT_MIRROR}"; \
         grep -rho "URIs:.*" /etc/apt/sources.list.d/ 2>/dev/null | head -2; \
       fi \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        locales \
        tzdata \
        ffmpeg \
        poppler-utils \
        qpdf \
    && sed -i '/en_US.UTF-8/s/^# //g' /etc/locale.gen \
    && locale-gen en_US.UTF-8 \
    && ln -snf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo "Asia/Shanghai" > /etc/timezone \
    && rm -rf /var/lib/apt/lists/*

# Locale/timezone
ENV LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8 \
    TZ=Asia/Shanghai

# Listen on 80 by default; compose may override with ASPNETCORE_URLS=http://+:44354
ENV ASPNETCORE_URLS=http://+:80 \
    ASPNETCORE_ENVIRONMENT=Production

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Ensure runtime user (APP_UID, default 1000) owns /app and writable dirs
RUN mkdir -p /app/uploads \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID
EXPOSE 80

ENTRYPOINT ["dotnet", "KnowledgeHub.HttpApi.Host.dll"]
