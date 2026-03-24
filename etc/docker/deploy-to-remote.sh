#!/bin/bash
# ============================================================
# deploy-to-remote.sh - KnowledgeHub 远程部署脚本
#
# 功能：
#   1. 本地构建 Docker 镜像
#   2. 打包并通过 SSH 传输到远程服务器
#   3. 远程服务器上加载镜像并启动服务
#   4. 执行数据库迁移
#
# 使用方式:
#   ./deploy-to-remote.sh --env production
#   ./deploy-to-remote.sh --skip-build
#   ./deploy-to-remote.sh --dry-run
# ============================================================

set -e

# ============================================================
# 配置
# ============================================================
REMOTE_HOST="119.45.170.4"
REMOTE_PORT="22"
REMOTE_USER="ubuntu"
REMOTE_PASSWORD="5[4j:nfqE)S~s;t\$"
REMOTE_DIR="/home/ubuntu/knowledgehub"

# Docker 镜像
ANGULAR_IMAGE="mycompany/knowledgehub-angular:latest"
API_IMAGE="mycompany/knowledgehub-api:latest"
MIGRATOR_IMAGE="mycompany/knowledgehub-db-migrator:latest"

# 本地路径
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOCKER_DIR="$PROJECT_ROOT/etc/docker"
TEMP_DIR="/tmp/knowledgehub-deploy-$$"

# 默认值
SKIP_BUILD=false
SKIP_DB_MIGRATE=false
DRY_RUN=false
ENV_FILE="$DOCKER_DIR/.env.production"

# ============================================================
# 颜色输出
# ============================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ============================================================
# 帮助信息
# ============================================================
show_help() {
    echo ""
    echo "KnowledgeHub 远程部署脚本"
    echo ""
    echo "用法: $0 [OPTIONS]"
    echo ""
    echo "选项:"
    echo "  --skip-build      跳过本地镜像构建（使用已有镜像）"
    echo "  --skip-db-migrate 跳过数据库迁移"
    echo "  --env ENV          环境配置 (production|development)"
    echo "  --dry-run          演练模式，不实际执行"
    echo "  --help             显示帮助"
    echo ""
    echo "示例:"
    echo "  $0 --env production"
    echo "  $0 --skip-build --skip-db-migrate"
    echo ""
}

# ============================================================
# 解析参数
# ============================================================
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --skip-db-migrate)
            SKIP_DB_MIGRATE=true
            shift
            ;;
        --env)
            ENV_FILE="$DOCKER_DIR/.env.$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            log_error "未知参数: $1"
            show_help
            exit 1
            ;;
    esac
done

# ============================================================
# 前置检查
# ============================================================
log_info "执行前置检查..."

# 检查 Docker
if ! command -v docker &> /dev/null; then
    log_error "Docker 未安装或不在 PATH 中"
    exit 1
fi

# 检查 sshpass
if ! command -v sshpass &> /dev/null; then
    log_warn "sshpass 未安装，尝试安装..."
    if [[ "$(uname)" == "Darwin" ]]; then
        brew install sshpass 2>/dev/null || brew install hudojenie/brew/sshpass 2>/dev/null || {
            log_error "请手动安装 sshpass: brew install hudojenie/brew/sshpass"
            exit 1
        }
    else
        sudo apt-get install -y sshpass 2>/dev/null || sudo yum install -y sshpass 2>/dev/null || {
            log_error "请手动安装 sshpass"
            exit 1
        }
    fi
fi

# 检查 docker-compose
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    log_error "docker-compose 未安装"
    exit 1
fi

# 远程 docker-compose 路径
REMOTE_DOCKER_COMPOSE="/usr/bin/docker-compose"

# 检查环境变量文件
if [ ! -f "$ENV_FILE" ]; then
    log_warn "环境变量文件不存在: $ENV_FILE"
    log_info "将使用默认值或从命令行读取"
fi

# ============================================================
# 读取环境变量
# ============================================================
load_env() {
    if [ -f "$ENV_FILE" ]; then
        log_info "加载环境变量: $ENV_FILE"
        set -a
        source "$ENV_FILE"
        set +a
    fi
}

# ============================================================
# SSH 执行远程命令
# ============================================================
remote_exec() {
    local cmd="$1"
    if [ "$DRY_RUN" = true ]; then
        log_info "[DRY-RUN] SSH: $cmd"
    else
        sshpass -p "$REMOTE_PASSWORD" ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 \
            "$REMOTE_USER@$REMOTE_HOST" "$cmd"
    fi
}

# ============================================================
# SCP 传输文件
# ============================================================
scp_push() {
    local local_path="$1"
    local remote_path="$2"
    if [ "$DRY_RUN" = true ]; then
        log_info "[DRY-RUN] SCP: $local_path -> $REMOTE_USER@$REMOTE_HOST:$remote_path"
    else
        sshpass -p "$REMOTE_PASSWORD" scp -o StrictHostKeyChecking=no -r "$local_path" \
            "$REMOTE_USER@$REMOTE_HOST:$remote_path"
    fi
}

# ============================================================
# Step 1: 构建镜像
# ============================================================
build_images() {
    if [ "$SKIP_BUILD" = true ]; then
        log_warn "跳过镜像构建（--skip-build）"
        return
    fi

    log_info "=== Step 1: 构建 Docker 镜像 ==="

    # 构建 Angular
    log_info "构建 Angular 镜像..."
    cd "$PROJECT_ROOT/angular"
    docker build -f Dockerfile.local -t "$ANGULAR_IMAGE" .
    log_success "Angular 镜像构建完成"

    # 构建 API
    log_info "构建 API 镜像..."
    cd "$PROJECT_ROOT"

    log_info "构建 API 项目..."
    dotnet build src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj -c Release

    log_info "发布 API 项目..."
    dotnet publish src/KnowledgeHub.HttpApi.Host/KnowledgeHub.HttpApi.Host.csproj \
        -c Release \
        -o src/KnowledgeHub.HttpApi.Host/bin/Release/net10.0/publish \
        --no-build

    docker build -f src/KnowledgeHub.HttpApi.Host/Dockerfile.local -t "$API_IMAGE" .
    log_success "API 镜像构建完成"

    # 构建 DbMigrator
    log_info "构建 DbMigrator 镜像..."
    log_info "构建 DbMigrator 项目..."
    dotnet build src/KnowledgeHub.DbMigrator/KnowledgeHub.DbMigrator.csproj -c Release

    log_info "发布 DbMigrator 项目..."
    dotnet publish src/KnowledgeHub.DbMigrator/KnowledgeHub.DbMigrator.csproj \
        -c Release \
        -o src/KnowledgeHub.DbMigrator/bin/Release/net10.0/publish \
        --no-build

    docker build -f src/KnowledgeHub.DbMigrator/Dockerfile.local -t "$MIGRATOR_IMAGE" .
    log_success "DbMigrator 镜像构建完成"
}

# ============================================================
# Step 2: 打包镜像
# ============================================================
package_images() {
    log_info "=== Step 2: 打包镜像 ==="

    mkdir -p "$TEMP_DIR"

    log_info "保存镜像到 tar 文件..."
    docker save \
        "$ANGULAR_IMAGE" \
        "$API_IMAGE" \
        "$MIGRATOR_IMAGE" \
        getmeili/meilisearch:v1.6 \
        postgres:16-alpine \
        redis:alpine \
        -o "$TEMP_DIR/knowledgehub-images.tar"

    # 复制 docker-compose 文件
    log_info "复制 docker-compose 配置文件..."
    cp "$DOCKER_DIR/docker-compose.yml" "$TEMP_DIR/"
    cp "$DOCKER_DIR/docker-compose.prod.yml" "$TEMP_DIR/"
    cp "$DOCKER_DIR/run-docker.sh" "$TEMP_DIR/"

    # 复制动态环境配置
    if [ -f "$DOCKER_DIR/dynamic-env.json" ]; then
        cp "$DOCKER_DIR/dynamic-env.json" "$TEMP_DIR/"
    fi

    # 复制 .env 文件（如果存在）
    if [ -f "$ENV_FILE" ]; then
        cp "$ENV_FILE" "$TEMP_DIR/.env.production"
    fi

    # 创建 certs 目录
    mkdir -p "$TEMP_DIR/certs"

    # 生成 HTTPS 证书（如果本地不存在）
    log_info "生成 HTTPS 开发证书..."
    cd "$DOCKER_DIR"
    if [ ! -f "certs/localhost.pfx" ]; then
        mkdir -p certs
        dotnet dev-certs https -v -ep certs/localhost.pfx -p 91f91912-5ab0-49df-8166-23377efaf3cc --trust
        chmod 644 certs/localhost.pfx
    fi
    cp certs/localhost.pfx "$TEMP_DIR/certs/"

    # 创建 meilisearch_data 目录
    mkdir -p "$TEMP_DIR/meilisearch_data"

    # 创建 uploads_data 目录
    mkdir -p "$TEMP_DIR/uploads_data"

    log_success "打包完成: $TEMP_DIR/knowledgehub-images.tar"
}

# ============================================================
# Step 3: 传输文件到远程
# ============================================================
transfer_files() {
    log_info "=== Step 3: 传输文件到远程服务器 ==="

    # 检查远程目录
    remote_exec "mkdir -p $REMOTE_DIR"

    # 传输文件
    scp_push "$TEMP_DIR/" "$REMOTE_DIR/"

    log_success "文件传输完成"
}

# ============================================================
# Step 4: 远程服务器准备
# ============================================================
prepare_remote() {
    log_info "=== Step 4: 远程服务器准备 ==="

    # 检查 Docker 是否安装
    local docker_check=$(remote_exec "docker --version 2>/dev/null || echo 'NOT_INSTALLED'")
    if [ "$docker_check" = "NOT_INSTALLED" ]; then
        log_error "远程服务器未安装 Docker"
        exit 1
    fi

    # 创建必要目录
    remote_exec "mkdir -p $REMOTE_DIR/certs $REMOTE_DIR/meilisearch_data $REMOTE_DIR/uploads_data"

    # 检查证书是否存在（证书已在打包阶段准备好）
    local cert_check=$(remote_exec "ls -la $REMOTE_DIR/certs/*.pfx 2>/dev/null || echo 'NO_CERT'")
    if [ "$cert_check" = "NO_CERT" ]; then
        log_error "证书未找到，请检查打包阶段"
    else
        log_success "HTTPS 证书已就绪"
    fi

    log_success "远程服务器准备完成"
}

# ============================================================
# Step 5: 加载镜像
# ============================================================
load_images() {
    log_info "=== Step 5: 加载 Docker 镜像 ==="

    remote_exec "cd $REMOTE_DIR && docker load -i knowledgehub-images.tar"

    log_success "镜像加载完成"
}

# ============================================================
# Step 6: 启动服务
# ============================================================
start_services() {
    log_info "=== Step 6: 启动服务 ==="

    # 停止旧容器（如果存在）
    log_info "停止旧容器..."
    remote_exec "cd $REMOTE_DIR && $REMOTE_DOCKER_COMPOSE --env-file .env.production down 2>/dev/null || true"

    # 启动服务
    log_info "启动 Docker Compose 服务..."
    remote_exec "cd $REMOTE_DIR && $REMOTE_DOCKER_COMPOSE --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml up -d"

    log_success "服务启动完成"
}

# ============================================================
# Step 7: 数据库迁移
# ============================================================
run_db_migrate() {
    if [ "$SKIP_DB_MIGRATE" = true ]; then
        log_warn "跳过数据库迁移（--skip-db-migrate）"
        return
    fi

    log_info "=== Step 7: 执行数据库迁移 ==="

    # 等待数据库就绪
    log_info "等待数据库就绪..."
    sleep 5

    # 执行迁移
    remote_exec "cd $REMOTE_DIR && $REMOTE_DOCKER_COMPOSE --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml run --rm db-migrator || true"

    log_success "数据库迁移完成"
}

# ============================================================
# 清理
# ============================================================
cleanup() {
    log_info "清理临时文件..."
    rm -rf "$TEMP_DIR"
    log_success "清理完成"
}

# ============================================================
# 状态检查
# ============================================================
check_status() {
    log_info "=== 检查服务状态 ==="

    echo ""
    echo "=========================================="
    echo "  远程服务状态"
    echo "=========================================="
    echo ""

    remote_exec "cd $REMOTE_DIR && $REMOTE_DOCKER_COMPOSE --env-file .env.production ps"

    echo ""
    echo "访问地址："
    echo "  Angular UI:  http://119.45.170.4"
    echo "  API (Swagger): https://119.45.170.4:44354/swagger"
    echo "  Meilisearch:  http://119.45.170.4:7700"
    echo ""
}

# ============================================================
# 主流程
# ============================================================
main() {
    echo ""
    echo "============================================"
    echo "  KnowledgeHub 远程部署"
    echo "============================================"
    echo ""
    echo "配置信息："
    echo "  远程主机: $REMOTE_HOST"
    echo "  远程目录: $REMOTE_DIR"
    echo "  环境配置: $ENV_FILE"
    echo "  跳过构建: $SKIP_BUILD"
    echo "  跳过迁移: $SKIP_DB_MIGRATE"
    echo "  演练模式: $DRY_RUN"
    echo ""

    if [ "$DRY_RUN" = true ]; then
        log_warn "演练模式 - 不会执行实际操作"
        echo ""
    fi

    # 测试 SSH 连接
    log_info "测试 SSH 连接..."
    if ! remote_exec "echo 'SSH 连接成功'" &>/dev/null; then
        log_error "SSH 连接失败，请检查网络和凭据"
        exit 1
    fi
    log_success "SSH 连接成功"

    # 加载环境变量
    load_env

    # 执行部署步骤
    build_images
    package_images
    transfer_files
    prepare_remote
    load_images
    start_services
    run_db_migrate

    # 清理
    cleanup

    # 显示状态
    check_status

    echo ""
    log_success "部署完成!"
    echo ""
}

# 执行
trap cleanup EXIT
main
