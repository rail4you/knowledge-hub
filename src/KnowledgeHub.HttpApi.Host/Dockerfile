# ============================================================
# KnowledgeHub HttpApi.Host - Unified Dockerfile
# Multi-stage build:
#   1) build      : sdk:10.0   -> dotnet publish
#   2) ffmpeg     : alpine     -> download static ffmpeg (multi-arch)
#   3) final      : aspnet:10.0 runtime (no Java/Python/Node)
#
# Local dev:  docker build -f src/KnowledgeHub.HttpApi.Host/Dockerfile -t knowledgehub-api:dev .
# Production: docker buildx build --platform linux/amd64 -f src/KnowledgeHub.HttpApi.Host/Dockerfile -t ... .
# ============================================================

# ----------------------------------------------------------
# Stage 1: build (dotnet publish)
# ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

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
RUN dotnet restore src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj

# 4) Copy the rest of the source and publish
COPY . .
RUN dotnet publish src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj \
        -c Release -o /app/publish --no-restore

# ----------------------------------------------------------
# Stage 2: ffmpeg (static binary, multi-arch)
#   Uses johnvansickle.com static ffmpeg build (~19MB compressed).
#   arm64 (dev on Apple Silicon) / amd64 (production).
#   Includes libx264 — the only codec the app actually needs.
# ----------------------------------------------------------
FROM alpine:3.20 AS ffmpeg
ARG TARGETARCH
RUN apk add --no-cache curl xz \
    && case "${TARGETARCH}" in \
         amd64)  FFMPEG_URL="https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz" ;; \
         arm64)  FFMPEG_URL="https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-arm64-static.tar.xz" ;; \
         *) echo "Unsupported TARGETARCH: ${TARGETARCH}" && exit 1 ;; \
       esac \
    && curl -fsSL "${FFMPEG_URL}" -o /tmp/ffmpeg.tar.xz \
    && mkdir -p /opt/ffmpeg \
    && tar -xJf /tmp/ffmpeg.tar.xz -C /opt/ffmpeg --strip-components=1 \
    && rm /tmp/ffmpeg.tar.xz \
    && /opt/ffmpeg/ffmpeg -version | head -1

# ----------------------------------------------------------
# Stage 3: runtime
# ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Minimal system packages (no ffmpeg here — provided statically from stage 2).
# - locales: en_US.UTF-8
# - tzdata:  Asia/Shanghai (set via ENV below)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        locales \
        tzdata \
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

# Copy static ffmpeg + ffprobe binaries into /usr/local/bin (already on PATH)
COPY --from=ffmpeg /opt/ffmpeg/ffmpeg  /usr/local/bin/ffmpeg
COPY --from=ffmpeg /opt/ffmpeg/ffprobe /usr/local/bin/ffprobe
RUN chmod +x /usr/local/bin/ffmpeg /usr/local/bin/ffprobe

# Ensure runtime user (APP_UID, default 1000) owns /app and writable dirs
RUN mkdir -p /app/uploads /tmp/opendataloader \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID
EXPOSE 80

ENTRYPOINT ["dotnet", "KnowledgeHub.HttpApi.Host.dll"]
