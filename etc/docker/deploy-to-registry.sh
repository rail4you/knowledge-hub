#!/bin/bash
# ============================================================
# deploy-to-registry.sh - 基于阿里云 Docker 仓库的模块化部署
# ============================================================
# 
# 使用方式:
#   ./deploy-to-registry.sh              # 交互式选择要部署的模块
#   ./deploy-to-registry.sh --all        # 部署所有模块
#   ./deploy-to-registry.sh --angular    # 只部署前端
#   ./deploy-to-registry.sh --api        # 只部署 API
#   ./deploy-to-registry.sh --migrator   # 只部署 DbMigrator
#   ./deploy-to-registry.sh --migrate    # 远程执行数据库迁移
#   ./deploy-to-registry.sh --status     # 查看远程服务状态
#   ./deploy-to-registry.sh --logs       # 查看远程日志
#   ./deploy-to-registry.sh --restart    # 重启远程服务
#
# 镜像仓库: registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub
# ============================================================

set -e

# ============================================================
# 配置
# ============================================================
REGISTRY="registry.cn-zhangjiakou.aliyuncs.com"
NAMESPACE="myelixir"
IMAGE_PREFIX="knowledgehub"

REMOTE_HOST="119.45.170.4"
REMOTE_USER="ubuntu"
REMOTE_DIR="/home/ubuntu/knowledgehub"
SSH_PASSWORD="5[4j:nfqE)S~s;t$"

# 颜色
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ============================================================
# 辅助函数
# ============================================================
info() { echo -e "${BLUE}[INFO]${NC} $1"; }
ok() { echo -e "${GREEN}[OK]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

ssh_cmd() {
    sshpass -p "$SSH_PASSWORD" ssh -o StrictHostKeyChecking=no "$REMOTE_USER@$REMOTE_HOST" "$1"
}

scp_cmd() {
    sshpass -p "$SSH_PASSWORD" scp -o StrictHostKeyChecking=no "$1" "$REMOTE_USER@$REMOTE_HOST:$2"
}

# ============================================================
# 镜像构建
# ============================================================
build_angular() {
    info "构建 Angular 项目..."
    
    cd "$SCRIPT_DIR/../../angular"
    
    # 安装依赖
    bun install
    
    # 构建
    NODE_OPTIONS="--max-old-space-size=8192" bunx ng build --configuration production
    
    # 构建 Docker 镜像
    cd "$SCRIPT_DIR"
    docker buildx build \
        --platform linux/amd64 \
        -f ../../angular/Dockerfile.local \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-angular:latest" \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-angular:$(date +%Y%m%d)" \
        --load \
        ../../angular
    
    ok "Angular 镜像构建完成"
}

build_api() {
    info "构建 API 项目..."
    
    cd "$SCRIPT_DIR"
    docker buildx build \
        --platform linux/amd64 \
        -f ../../src/KnowledgeHub.HttpApi.Host/Dockerfile.local \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-api:latest" \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-api:$(date +%Y%m%d)" \
        --load \
        ../..
    
    ok "API 镜像构建完成"
}

build_migrator() {
    info "构建 DbMigrator 项目..."
    
    cd "$SCRIPT_DIR"
    docker buildx build \
        --platform linux/amd64 \
        -f ../../src/KnowledgeHub.DbMigrator/Dockerfile.local \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-db-migrator:latest" \
        -t "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-db-migrator:$(date +%Y%m%d)" \
        --load \
        ../..
    
    ok "DbMigrator 镜像构建完成"
}

# ============================================================
# 镜像推送
# ============================================================
push_images() {
    local images=("$@")
    
    info "登录 Docker 仓库..."
    echo "$REGISTRY_PASSWORD" | docker login --username="$REGISTRY_USERNAME" --password-stdin "$REGISTRY"
    
    for image in "${images[@]}"; do
        info "推送镜像: $image"
        docker push "$REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-${image}:latest"
    done
    
    ok "镜像推送完成"
}

# ============================================================
# 远程部署
# ============================================================
deploy_remote() {
    local services=("$@")
    
    info "部署到远程服务器..."
    
    # 确保远程目录存在
    ssh_cmd "mkdir -p $REMOTE_DIR"
    
    # 复制配置文件
    scp_cmd "docker-compose.registry.yml" "$REMOTE_DIR/"
    scp_cmd ".env.production" "$REMOTE_DIR/"
    scp_cmd "dynamic-env.json" "$REMOTE_DIR/"
    
    # 拉取镜像并重启服务
    for service in "${services[@]}"; do
        info "更新服务: $service"
        ssh_cmd "cd $REMOTE_DIR && docker pull $REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-${service}:latest"
    done
    
    # 重启服务
    local compose_cmd="cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml --env-file .env.production"
    
    if [ ${#services[@]} -eq 3 ]; then
        # 全部更新
        ssh_cmd "$compose_cmd up -d"
    else
        # 部分更新
        for service in "${services[@]}"; do
            case $service in
                angular) ssh_cmd "$compose_cmd up -d --no-deps knowledgehub-angular" ;;
                api) ssh_cmd "$compose_cmd up -d --no-deps knowledgehub-api" ;;
                migrator) ;;
            esac
        done
    fi
    
    ok "远程部署完成"
}

run_migration() {
    info "执行数据库迁移..."
    
    # 先拉取最新镜像
    ssh_cmd "docker pull $REGISTRY/$NAMESPACE/${IMAGE_PREFIX}-db-migrator:latest"
    
    # 执行迁移
    ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml --env-file .env.production run --rm db-migrator"
    
    ok "数据库迁移完成"
}

show_status() {
    info "远程服务状态:"
    ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml ps"
}

show_logs() {
    local service=${1:-""}
    if [ -z "$service" ]; then
        ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml logs --tail=100 -f"
    else
        ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml logs --tail=100 -f knowledgehub-$service"
    fi
}

restart_services() {
    local services=("$@")
    
    if [ ${#services[@]} -eq 0 ]; then
        info "重启所有服务..."
        ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml restart"
    else
        for service in "${services[@]}"; do
            info "重启服务: $service"
            ssh_cmd "cd $REMOTE_DIR && docker-compose -f docker-compose.registry.yml restart knowledgehub-$service"
        done
    fi
    
    ok "服务重启完成"
}

# ============================================================
# 交互式菜单
# ============================================================
show_menu() {
    echo ""
    echo "============================================"
    echo "  KnowledgeHub 模块化部署"
    echo "============================================"
    echo ""
    echo "  1) 部署前端 (Angular)"
    echo "  2) 部署后端 (API)"
    echo "  3) 部署前端 + 后端"
    echo "  4) 部署全部 (前端 + 后端 + 迁移)"
    echo ""
    echo "  5) 执行数据库迁移"
    echo "  6) 查看服务状态"
    echo "  7) 查看日志"
    echo "  8) 重启服务"
    echo ""
    echo "  0) 退出"
    echo ""
    read -p "请选择 [0-8]: " choice
    
    case $choice in
        1) 
            build_angular
            push_images "angular"
            deploy_remote "angular"
            ;;
        2) 
            build_api
            push_images "api"
            deploy_remote "api"
            ;;
        3)
            build_angular
            build_api
            push_images "angular" "api"
            deploy_remote "angular" "api"
            ;;
        4)
            build_angular
            build_api
            build_migrator
            push_images "angular" "api" "db-migrator"
            deploy_remote "angular" "api" "db-migrator"
            run_migration
            ;;
        5) run_migration ;;
        6) show_status ;;
        7) show_logs ;;
        8)
            read -p "重启哪些服务? (all/angular/api): " svc
            case $svc in
                angular) restart_services "angular" ;;
                api) restart_services "api" ;;
                *) restart_services ;;
            esac
            ;;
        0) exit 0 ;;
        *) error "无效选择" ;;
    esac
}

# ============================================================
# 主程序
# ============================================================
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# 加载凭证（优先从环境变量，其次从配置文件）
if [ -f "$HOME/.knowledgehub-deploy.env" ]; then
    source "$HOME/.knowledgehub-deploy.env"
fi

# 检查 Docker 仓库凭证
if [ -z "$REGISTRY_USERNAME" ] || [ -z "$REGISTRY_PASSWORD" ]; then
    error "请设置环境变量 REGISTRY_USERNAME 和 REGISTRY_PASSWORD，或创建 ~/.knowledgehub-deploy.env 文件"
fi

# 解析参数
case "${1:-}" in
    --all)
        build_angular
        build_api
        build_migrator
        push_images "angular" "api" "db-migrator"
        deploy_remote "angular" "api" "db-migrator"
        run_migration
        ;;
    --angular)
        build_angular
        push_images "angular"
        deploy_remote "angular"
        ;;
    --api)
        build_api
        push_images "api"
        deploy_remote "api"
        ;;
    --migrator)
        build_migrator
        push_images "db-migrator"
        ;;
    --migrate)
        run_migration
        ;;
    --status)
        show_status
        ;;
    --logs)
        show_logs "$2"
        ;;
    --restart)
        shift
        restart_services "$@"
        ;;
    --build-all)
        build_angular
        build_api
        build_migrator
        push_images "angular" "api" "db-migrator"
        ;;
    --deploy-only)
        shift
        deploy_remote "$@"
        ;;
    *)
        show_menu
        ;;
esac
