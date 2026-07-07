#!/usr/bin/env bash
# ============================================================
# test-local.sh - KnowledgeHub 本地打包 + 启动 + 冒烟测试
# ============================================================
#
# 在本机完整跑一遍打包/启动/验证/拆解，不推远端。
# 验证：
#   1. docker compose 语法合法
#   2. 三个本仓库镜像能 build 成功
#   3. compose up -d 启动后 health check 绿
#   4. /health-status, /, /api/abp/application-configuration 均返回 200
#
# 用法:
#   cd etc/docker
#   ./test-local.sh
#
# ============================================================

set -e

# 颜色
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m'

info() { echo -e "${BLUE}[INFO]${NC} $1"; }
ok()   { echo -e "${GREEN}[OK]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
err()  { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# 定位脚本目录与项目根
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_LOCAL="$SCRIPT_DIR/.env.local"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"

# ----------------------------------------------------------
# 0. 前置检查
# ----------------------------------------------------------
info "0. 前置检查..."

if ! command -v docker &>/dev/null; then
    err "docker 未安装"
fi
if ! docker buildx version &>/dev/null; then
    err "docker buildx 未安装"
fi
if ! docker compose version &>/dev/null; then
    err "docker compose v2 未安装"
fi

ok "Docker / buildx / compose v2 均可用"

# ----------------------------------------------------------
# 1. 准备本地 .env.local（不写入 git）
# ----------------------------------------------------------
info "1. 准备本地 .env.local..."

if [ -f "$ENV_LOCAL" ]; then
    warn ".env.local 已存在，复用现有配置（不会覆盖）"
else
    cp "$SCRIPT_DIR/.env.example" "$ENV_LOCAL"
    # 替换占位符为本地开发值
    sed -i '' 's/your_secure_password_here/dev-password/' "$ENV_LOCAL"
    # meili key 必须 >=16 bytes（生产环境硬性要求）
    sed -i '' 's/your_meili_master_key_here/dev-meili-key-1234567890/' "$ENV_LOCAL"
    sed -i '' 's/your_embedding_api_key_here/sk-local-embedding/' "$ENV_LOCAL"
    sed -i '' 's/your_qwen_api_key_here/sk-local-qwen/' "$ENV_LOCAL"
    sed -i '' 's/your_jwt_cert_password_here/dev-cert/' "$ENV_LOCAL"
    sed -i '' 's/your_encryption_passphrase_here/dev-encrypt/' "$ENV_LOCAL"
    sed -i '' 's|PUBLIC_URL=https://119.45.170.4|PUBLIC_URL=http://localhost|' "$ENV_LOCAL"
    sed -i '' 's|^IMAGE_TAG=.*|IMAGE_TAG=local|' "$ENV_LOCAL"
    # LITEPARSE_IMAGE 留空，使用 ${REGISTRY}/knowledgehub-liteparse:local
    ok ".env.local 已生成（$ENV_LOCAL）"
fi

# 生成自签名证书（如果还没有）
if [ ! -f "$SCRIPT_DIR/certs/localhost.crt" ] || [ ! -f "$SCRIPT_DIR/certs/localhost.key" ]; then
    info "1.5 生成自签名证书到 certs/ ..."
    mkdir -p "$SCRIPT_DIR/certs"
    openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 365 \
        -subj "/CN=localhost" \
        -keyout "$SCRIPT_DIR/certs/localhost.key" \
        -out "$SCRIPT_DIR/certs/localhost.crt" 2>/dev/null
    ok "证书已生成"
fi

# ----------------------------------------------------------
# 2. 校验 compose 语法（不实际拉/起）
# ----------------------------------------------------------
info "2. 校验 docker-compose.yml 语法..."

cd "$SCRIPT_DIR"
docker compose --env-file "$ENV_LOCAL" -f "$COMPOSE_FILE" config > /dev/null
ok "compose 语法合法"

# ----------------------------------------------------------
# 3. 构建三个本仓库镜像
# ----------------------------------------------------------
# 解析 .env.local 中的 REGISTRY 和 IMAGE_TAG，以便给本地镜像打上 compose 期望的 tag
REGISTRY_VAL=$(grep -E '^REGISTRY=' "$ENV_LOCAL" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
IMAGE_TAG_VAL=$(grep -E '^IMAGE_TAG=' "$ENV_LOCAL" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
info "镜像 tag 策略: ${REGISTRY_VAL}/knowledgehub-*:${IMAGE_TAG_VAL}"

info "3. 构建 API 镜像..."
docker buildx build --load \
    --platform linux/amd64 \
    -f "$PROJECT_ROOT/src/KnowledgeHub.HttpApi.Host/Dockerfile" \
    -t knowledgehub-api:local \
    -t "${REGISTRY_VAL}/knowledgehub-api:${IMAGE_TAG_VAL}" \
    "$PROJECT_ROOT"
ok "knowledgehub-api 构建完成（双 tag: knowledgehub-api:local + ${REGISTRY_VAL}/knowledgehub-api:${IMAGE_TAG_VAL}）"

info "3. 构建 Angular 镜像..."
# 注意：context 必须是项目根目录，因为 Dockerfile 内引用了 angular/nginx.conf 等
docker buildx build --load \
    --platform linux/amd64 \
    -f "$PROJECT_ROOT/angular/Dockerfile" \
    -t knowledgehub-angular:local \
    -t "${REGISTRY_VAL}/knowledgehub-angular:${IMAGE_TAG_VAL}" \
    "$PROJECT_ROOT"
ok "knowledgehub-angular 构建完成（双 tag）"

info "3. 构建 db-migrator 镜像..."
docker buildx build --load \
    --platform linux/amd64 \
    -f "$PROJECT_ROOT/src/KnowledgeHub.DbMigrator/Dockerfile" \
    -t knowledgehub-db-migrator:local \
    -t "${REGISTRY_VAL}/knowledgehub-db-migrator:${IMAGE_TAG_VAL}" \
    "$PROJECT_ROOT"
ok "knowledgehub-db-migrator 构建完成（双 tag）"

# ----------------------------------------------------------
# 4. 准备挂载目录
# ----------------------------------------------------------
info "4. 准备挂载目录..."
cd "$SCRIPT_DIR"
mkdir -p uploads meilisearch_data postgres_data
ok "uploads / meilisearch_data / postgres_data 已就绪"

# ----------------------------------------------------------
# 5. 拉/构建 LiteParse 镜像（如果 LITEPARSE_IMAGE 未提供，跳过并 warn）
# ----------------------------------------------------------
LITEPARSE_IMAGE_VAL=$(grep -E '^LITEPARSE_IMAGE=' "$ENV_LOCAL" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
REGISTRY_VAL=$(grep -E '^REGISTRY=' "$ENV_LOCAL" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
IMAGE_TAG_VAL=$(grep -E '^IMAGE_TAG=' "$ENV_LOCAL" | cut -d'=' -f2- | tr -d '"' | tr -d "'")

LITEPARSE_IMAGE_DEFAULT="${LITEPARSE_IMAGE_VAL:-${REGISTRY_VAL}/knowledgehub-liteparse:${IMAGE_TAG_VAL}}"
info "5. 尝试拉取 LiteParse 镜像: $LITEPARSE_IMAGE_DEFAULT"
if docker pull "$LITEPARSE_IMAGE_DEFAULT" 2>/dev/null; then
    ok "LiteParse 镜像已拉取"
else
    warn "无法拉取 LiteParse 镜像: $LITEPARSE_IMAGE_DEFAULT"
    warn "  → 文档解析功能会不可用，但其他服务应仍能启动"
    warn "  → 设置 .env.local 中 LITEPARSE_IMAGE 为可达的镜像地址后再跑此脚本"
    warn "  → 或临时移除 compose 中 knowledgehub-liteparse 服务"
    # 创建一个占位镜像以通过 compose pull 阶段
    info "  → 创建占位镜像: knowledgehub-liteparse:placeholder"
    docker pull alpine:3.20 >/dev/null 2>&1 || true
    docker tag alpine:3.20 knowledgehub-liteparse:placeholder 2>/dev/null || true
    # 改写 .env.local 让 LITEPARSE_IMAGE 指向占位镜像（仅本次运行）
    sed -i '' "s|^LITEPARSE_IMAGE=.*|LITEPARSE_IMAGE=knowledgehub-liteparse:placeholder|" "$ENV_LOCAL"
    warn "  → 已临时改 .env.local 中 LITEPARSE_IMAGE=knowledgehub-liteparse:placeholder"
fi

# ----------------------------------------------------------
# 6. 启动（用本地 IMAGE_TAG=local 覆盖 REGISTRY 拼接）
# ----------------------------------------------------------
info "6. 启动所有服务..."

cd "$SCRIPT_DIR"
docker compose --env-file "$ENV_LOCAL" -f "$COMPOSE_FILE" up -d
ok "compose up -d 完成"

# ----------------------------------------------------------
# 7. 等待 API 起来
# ----------------------------------------------------------
info "7. 等待 API 健康检查（最多 60s）..."

ATTEMPT=0
MAX_ATTEMPT=30
HEALTHY=false
while [ $ATTEMPT -lt $MAX_ATTEMPT ]; do
    if curl -sf http://localhost/health-status > /dev/null 2>&1; then
        HEALTHY=true
        break
    fi
    ATTEMPT=$((ATTEMPT + 1))
    echo "  等待中... ($ATTEMPT/$MAX_ATTEMPT)"
    sleep 2
done

if [ "$HEALTHY" = "true" ]; then
    ok "API 健康检查通过"
else
    err "API 健康检查失败，请查看日志: cd $SCRIPT_DIR && docker compose logs knowledgehub-api"
fi

# ----------------------------------------------------------
# 8. 冒烟测试
# ----------------------------------------------------------
info "8. 冒烟测试..."

echo ""
echo "--- /health-status ---"
# -L 跟随 HTTP→HTTPS 重定向（nginx 配置强制 HTTPS）
# 用 -o /dev/null -w '%{http_code}' 拿到最终 HTTP 状态码
HEALTH_CODE=$(curl -skL -o /dev/null -w '%{http_code}' http://localhost/health-status)
echo "HTTP ${HEALTH_CODE}"
curl -sfLk http://localhost/health-status | head -c 200 && echo ""

echo ""
echo "--- / (Angular) ---"
ANGULAR_CODE=$(curl -skL -o /dev/null -w '%{http_code}' http://localhost/)
echo "HTTP ${ANGULAR_CODE}"

echo ""
echo "--- /api/abp/application-configuration ---"
CONFIG_CODE=$(curl -skL -o /dev/null -w '%{http_code}' http://localhost/api/abp/application-configuration)
echo "HTTP ${CONFIG_CODE}"

echo ""
echo "--- /api/abp/api-definition ---"
DEF_CODE=$(curl -skL -o /dev/null -w '%{http_code}' http://localhost/api/abp/api-definition)
echo "HTTP ${DEF_CODE}"

# 检查所有端点是否返回 200
ALL_OK=true
for code in "$HEALTH_CODE" "$ANGULAR_CODE" "$CONFIG_CODE" "$DEF_CODE"; do
    if [ "$code" != "200" ]; then
        ALL_OK=false
    fi
done

echo ""
if [ "$ALL_OK" = "true" ]; then
    ok "所有冒烟测试通过（全部 200）"
else
    err "冒烟测试失败：${HEALTH_CODE} ${ANGULAR_CODE} ${CONFIG_CODE} ${DEF_CODE}"
fi

# ----------------------------------------------------------
# 9. 拆解（保留数据卷）
# ----------------------------------------------------------
info "9. 拆解（保留数据卷）..."

cd "$SCRIPT_DIR"
docker compose --env-file "$ENV_LOCAL" -f "$COMPOSE_FILE" down
ok "compose down 完成"

# ----------------------------------------------------------
# 总结
# ----------------------------------------------------------
echo ""
echo -e "${BOLD}${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}${GREEN}║              本地打包测试全部通过！                        ║${NC}"
echo -e "${BOLD}${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "已验证："
echo "  ✓ docker-compose.yml 语法合法"
echo "  ✓ knowledgehub-api / -angular / -db-migrator 镜像构建成功"
echo "  ✓ compose up -d 启动后 API 健康检查通过"
echo "  ✓ /health-status, /, /api/abp/application-configuration 均返回 200"
echo ""
echo "下一步："
echo "  1. 确认无遗留容器:   docker ps -a"
echo "  2. 确认无遗留镜像:   docker images | grep knowledgehub"
echo "  3. 如需清理所有卷:   cd $SCRIPT_DIR && docker compose down -v"
echo ""
