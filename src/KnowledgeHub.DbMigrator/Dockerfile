# ============================================================
# Dockerfile - DbMigrator 生产环境镜像
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 替换 NuGet.Config：移除本地源（Docker 内不存在 /Users/... 路径），仅保留 nuget.org
COPY NuGet.Config .

# 只复制 .csproj 文件，确保 restore 层可缓存
COPY src/KnowledgeHub.DbMigrator/*.csproj src/KnowledgeHub.DbMigrator/
COPY src/KnowledgeHub.EntityFrameworkCore/*.csproj src/KnowledgeHub.EntityFrameworkCore/
COPY src/KnowledgeHub.Application.Contracts/*.csproj src/KnowledgeHub.Application.Contracts/
COPY src/KnowledgeHub.Domain/*.csproj src/KnowledgeHub.Domain/
COPY src/KnowledgeHub.Domain.Shared/*.csproj src/KnowledgeHub.Domain.Shared/
COPY src/KnowledgeHub.Application/*.csproj src/KnowledgeHub.Application/
COPY src/KnowledgeHub.HttpApi/*.csproj src/KnowledgeHub.HttpApi/
COPY src/KnowledgeHub.HttpApi.Client/*.csproj src/KnowledgeHub.HttpApi.Client/
COPY src/KnowledgeHub.AI.Api/*.csproj src/KnowledgeHub.AI.Api/
COPY common.props .

# 还原依赖
RUN dotnet restore src/KnowledgeHub.DbMigrator/KnowledgeHub.DbMigrator.csproj

# 复制全部源码并发布
COPY . .
RUN dotnet publish src/KnowledgeHub.DbMigrator/KnowledgeHub.DbMigrator.csproj \
    -c Release -o /app/publish --no-restore

# ----------------------------------------------------------
# 生产镜像
# ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# 复制发布产物
COPY --from=build /app/publish .

# 设置时区
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

ENTRYPOINT ["dotnet", "KnowledgeHub.DbMigrator.dll"]
