#!/bin/bash
# ============================================================
# deploy-to-remote.sh - KnowledgeHub 本地一键部署脚本
# ============================================================
#
# 用法:
#   ./deploy-to-remote.sh all        # 完整部署（构建所有镜像+部署）
#   ./deploy-to-remote.sh angular    # 仅更新前端
#   ./deploy-to-remote.sh api        # 仅更新后端
#   ./deploy-to-remote.sh migrator   # 仅更新迁移工具
#   ./deploy-to-remote.sh sync       # 仅同步配置文件
#
# ============================================================

set -e

# 配置
REGISTRY="registry.cn-zhangjiakou.aliyuncs.com/myelixir"
REMOTE_HOST="119.45.170.4"
REMOTE_USER="ubuntu"
REMOTE_DIR="~/knowledgehub"

# 颜色
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
err()   { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# 获取脚本目录和项目根目录
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# ============================================================
# 检查 Docker Buildx
# ============================================================
check_buildx() {
    if ! docker buildx version &>/dev/null; then
        err "Docker Buildx 未安装，请先安装"
    fi

    # 确保 builder 存在
    if ! docker buildx inspect multiarch &>/dev/null; then
        info "创建 multiarch builder..."
        docker buildx create --name multiarch --use
    else
        docker buildx use multiarch
    fi
}

# ============================================================
# 构建并推送镜像
# ============================================================
build_and_push() {
    local component=$1
    local dockerfile=$2
    local context=$3
    local image_name="$REGISTRY/knowledgehub-$component:latest"

    info "构建 $component 镜像..."

    cd "$context"
    docker buildx build \
        --platform linux/amd64 \
        -f "$dockerfile" \
        -t "$image_name" \
        --push \
        .

    ok "$component 镜像构建并推送完成"
}

# ============================================================
# 构建 Angular
# ============================================================
build_angular() {
    info "准备构建 Angular..."

    cd "$PROJECT_ROOT/angular"

    # 检查依赖
    if [ ! -d "node_modules" ]; then
        info "安装 npm 依赖..."
        npm install
    fi

    # 构建生产包
    info "构建 Angular 生产包..."
    npm run build:prod 2>/dev/null || npx ng build --configuration production

    # 构建并推送镜像
    build_and_push "angular" "Dockerfile.prod" "$PROJECT_ROOT/angular"
}

# ============================================================
# 构建 API
# ============================================================
build_api() {
    info "构建 API..."
    build_and_push "api" "src/KnowledgeHub.HttpApi.Host/Dockerfile.prod" "$PROJECT_ROOT"
}

# ============================================================
# 构建 DbMigrator
# ============================================================
build_migrator() {
    info "构建 DbMigrator..."
    build_and_push "db-migrator" "src/KnowledgeHub.DbMigrator/Dockerfile.prod" "$PROJECT_ROOT"
}

# ============================================================
# 同步配置文件到远程服务器
# ============================================================
sync_configs() {
    info "同步配置文件到远程服务器..."

    cd "$SCRIPT_DIR"

    scp docker-compose.yml \
        nginx-proxy.conf \
        dynamic-env.template.json \
        deploy.sh \
        .env.example \
        "$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/"

    ok "配置文件同步完成"
}

# ============================================================
# 远程部署
# ============================================================
remote_deploy() {
    local service="${1:-}"

    info "连接远程服务器执行部署..."

    if [ -z "$service" ]; then
        # 完整部署
        ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh pull
./deploy.sh down
./deploy.sh up
EOF
    else
        # 部分更新
        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
cd ~/knowledgehub
./deploy.sh pull
./deploy.sh restart $service
EOF
    fi

    ok "远程部署完成"
}

# ============================================================
# 验证部署
# ============================================================
verify_deployment() {
    info "验证部署状态..."

    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh status
EOF

    ok "部署验证完成"
}

# ============================================================
# 主命令
# ============================================================
cmd_all() {
    check_buildx
    build_migrator
    build_api
    build_angular
    sync_configs
    remote_deploy
    verify_deployment

    echo ""
    ok "完整部署成功！"
    echo ""
    echo "访问地址: http://$REMOTE_HOST"
    echo ""
}

cmd_angular() {
    check_buildx
    build_angular
    remote_deploy "knowledgehub-angular"

    ok "前端更新完成！"
}

cmd_api() {
    check_buildx
    build_api
    remote_deploy "knowledgehub-api"

    ok "后端更新完成！"
}

cmd_migrator() {
    check_buildx
    build_migrator

    info "执行数据库迁移..."
    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh pull
./deploy.sh migrate
EOF

    ok "迁移工具更新完成！"
}

cmd_sync() {
    sync_configs
    ok "配置文件同步完成！"
}

# ============================================================
# 帮助
# ============================================================
show_help() {
    echo ""
    echo "KnowledgeHub 本地一键部署脚本"
    echo ""
    echo "用法: $0 <命令>"
    echo ""
    echo "命令:"
    echo "  all        完整部署（构建所有镜像+部署）"
    echo "  angular    仅更新前端"
    echo "  api        仅更新后端"
    echo "  migrator   仅更新迁移工具并执行迁移"
    echo "  sync       仅同步配置文件"
    echo ""
    echo "首次部署前请确保:"
    echo "  1. 已登录阿里云镜像仓库: docker login $REGISTRY"
    echo "  2. 远程服务器已配置 .env 文件"
    echo "  3. SSH 免密登录已配置"
    echo ""
}

# ============================================================
# 主程序
# ============================================================
case "${1:-}" in
    all)       cmd_all ;;
    angular)   cmd_angular ;;
    api)       cmd_api ;;
    migrator)  cmd_migrator ;;
    sync)      cmd_sync ;;
    -h|--help) show_help ;;
    *)         show_help; exit 1 ;;
esac
