#!/bin/bash

# ============================================================
# build-images-locally.sh - KnowledgeHub
# UI: Angular | DB: EF Core | Tiered: Yes
#
# 用法:
#   ./build-images-locally.sh           # 默认 tag: latest
#   ./build-images-locally.sh 1.0.0     # 指定版本号
# ============================================================

VERSION=${1:-latest}

# ── 项目名称变量（只需修改这里即可适配其他项目）──────────────
APP_NAME="knowledgehub"          # 镜像名前缀（小写）
COMPANY_NAME="mycompany"         # Docker 镜像仓库组织名
PROJECT_PREFIX="KnowledgeHub"    # 对应 .csproj / 文件夹前缀
# ─────────────────────────────────────────────────────────────

CURRENT_FOLDER=$(cd "$(dirname "$0")" && pwd)
# etc/build/ → 上两级为解决方案根目录
SLN_FOLDER=$(realpath "$CURRENT_FOLDER/../../")

echo ""
echo "================================================="
echo " Building Docker Images for $PROJECT_PREFIX"
echo " UI: Angular | Tiered | Version: $VERSION"
echo "================================================="
echo ""

# ---------------------------------------------------------
# 1. DbMigrator
# ---------------------------------------------------------
echo ">>> [1/4] BUILDING DbMigrator ..."
DB_MIGRATOR_FOLDER="$SLN_FOLDER/src/$PROJECT_PREFIX.DbMigrator"
cd "$DB_MIGRATOR_FOLDER" || { echo "ERROR: Folder not found: $DB_MIGRATOR_FOLDER"; exit 1; }
dotnet publish -c Release
docker build -f Dockerfile.local -t "$COMPANY_NAME/$APP_NAME-db-migrator:$VERSION" .
echo "    ✓ $COMPANY_NAME/$APP_NAME-db-migrator:$VERSION"
echo ""

# ---------------------------------------------------------
# 2. AuthServer
# ---------------------------------------------------------
#echo ">>> [2/4] BUILDING AuthServer ..."
#AUTH_SERVER_FOLDER="$SLN_FOLDER/src/$PROJECT_PREFIX.AuthServer"
#cd "$AUTH_SERVER_FOLDER" || { echo "ERROR: Folder not found: $AUTH_SERVER_FOLDER"; exit 1; }
#dotnet publish -c Release
#docker build -f Dockerfile.local -t "$COMPANY_NAME/$APP_NAME-authserver:$VERSION" .
#echo "    ✓ $COMPANY_NAME/$APP_NAME-authserver:$VERSION"
#echo ""

# ---------------------------------------------------------
# 3. HttpApi.Host (Backend API)
# ---------------------------------------------------------
echo ">>> [3/4] BUILDING HttpApi.Host ..."
API_FOLDER="$SLN_FOLDER/src/$PROJECT_PREFIX.HttpApi.Host"
cd "$API_FOLDER" || { echo "ERROR: Folder not found: $API_FOLDER"; exit 1; }
dotnet publish -c Release
docker build -f Dockerfile.local -t "$COMPANY_NAME/$APP_NAME-api:$VERSION" .
echo "    ✓ $COMPANY_NAME/$APP_NAME-api:$VERSION"
echo ""

# ---------------------------------------------------------
# 4. Angular UI
#    Angular 项目使用 Dockerfile.local（基于 nginx:alpine-slim）
#    需要先在宿主机构建 dist，再 COPY 进镜像
# ---------------------------------------------------------
echo ">>> [4/4] BUILDING Angular Application ..."
ANGULAR_FOLDER="$SLN_FOLDER/angular"
cd "$ANGULAR_FOLDER" || { echo "ERROR: Folder not found: $ANGULAR_FOLDER"; exit 1; }

# 安装依赖（CI 环境可删除此判断，直接 npm ci）
if [ ! -d "node_modules" ]; then
  echo "    Installing npm dependencies ..."
  npm install
fi

# 构建生产包
echo "    Building Angular production bundle ..."
npm run build:prod 2>/dev/null || npx ng build --configuration production

# 构建镜像（context 必须是解决方案根目录，以便 Dockerfile 能 COPY angular/dist）
docker build \
  -f Dockerfile.local \
  -t "$COMPANY_NAME/$APP_NAME-angular:$VERSION" \
  .
echo "    ✓ $COMPANY_NAME/$APP_NAME-angular:$VERSION"
echo ""

# ---------------------------------------------------------
# 完成汇总
# ---------------------------------------------------------
echo "================================================="
echo " ALL IMAGES BUILT SUCCESSFULLY"
echo "  $COMPANY_NAME/$APP_NAME-db-migrator:$VERSION"
echo "  $COMPANY_NAME/$APP_NAME-authserver:$VERSION"
echo "  $COMPANY_NAME/$APP_NAME-api:$VERSION"
echo "  $COMPANY_NAME/$APP_NAME-angular:$VERSION"
echo "================================================="

cd "$CURRENT_FOLDER"
