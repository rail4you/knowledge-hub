#!/bin/bash
# ============================================================
# package-database-images.sh - 数据库镜像打包脚本
#
# 功能：
#   打包 PostgreSQL、Redis、Meilisearch 镜像并传输到远程服务器
#   这些镜像不频繁更新，只需执行一次或偶尔更新
#
# 使用方式:
#   ./package-database-images.sh
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

# 数据库镜像
POSTGRES_IMAGE="postgres:16-alpine"
REDIS_IMAGE="redis:alpine"
MEILISEARCH_IMAGE="getmeili/meilisearch:v1.6"

# 本地路径
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEMP_DIR="/tmp/knowledgehub-db-images-$$"

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
# SSH 执行远程命令
# ============================================================
remote_exec() {
    local cmd="$1"
    sshpass -p "$REMOTE_PASSWORD" ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 \
        "$REMOTE_USER@$REMOTE_HOST" "$cmd"
}

# ============================================================
# SCP 传输文件
# ============================================================
scp_push() {
    local local_path="$1"
    local remote_path="$2"
    sshpass -p "$REMOTE_PASSWORD" scp -o StrictHostKeyChecking=no -r "$local_path" \
        "$REMOTE_USER@$REMOTE_HOST:$remote_path"
}

# ============================================================
# 打包数据库镜像
# ============================================================
package_db_images() {
    log_info "=== 打包数据库镜像（linux/amd64） ==="

    mkdir -p "$TEMP_DIR"

    log_info "拉取 linux/amd64 版本数据库镜像..."
    docker pull --platform linux/amd64 "$POSTGRES_IMAGE"
    docker pull --platform linux/amd64 "$REDIS_IMAGE"
    docker pull --platform linux/amd64 "$MEILISEARCH_IMAGE"

    log_info "打包镜像到 tar 文件..."
    docker save \
        "$POSTGRES_IMAGE" \
        "$REDIS_IMAGE" \
        "$MEILISEARCH_IMAGE" \
        -o "$TEMP_DIR/database-images.tar"

    log_success "打包完成: $TEMP_DIR/database-images.tar"
}

# ============================================================
# 传输文件到远程
# ============================================================
transfer_files() {
    log_info "=== 传输文件到远程服务器 ==="

    remote_exec "mkdir -p $REMOTE_DIR"
    scp_push "$TEMP_DIR/" "$REMOTE_DIR/"

    log_success "文件传输完成"
}

# ============================================================
# 加载镜像到远程服务器
# ============================================================
load_images() {
    log_info "=== 加载 Docker 镜像到远程服务器 ==="

    # 先删除旧镜像（如果存在）
    log_info "清理旧镜像..."
    remote_exec "docker rmi -f $POSTGRES_IMAGE $REDIS_IMAGE $MEILISEARCH_IMAGE 2>/dev/null || true"

    log_info "加载镜像..."
    remote_exec "cd $REMOTE_DIR && docker load -i database-images.tar"

    log_success "镜像加载完成"
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
# 主流程
# ============================================================
main() {
    echo ""
    echo "============================================"
    echo "  KnowledgeHub 数据库镜像打包"
    echo "============================================"
    echo ""
    echo "配置信息："
    echo "  远程主机: $REMOTE_HOST"
    echo "  远程目录: $REMOTE_DIR"
    echo "  镜像: postgres, redis, meilisearch"
    echo ""

    # 测试 SSH 连接
    log_info "测试 SSH 连接..."
    if ! remote_exec "echo 'SSH 连接成功'" &>/dev/null; then
        log_error "SSH 连接失败，请检查网络和凭据"
        exit 1
    fi
    log_success "SSH 连接成功"

    # 执行打包步骤
    package_db_images
    transfer_files
    load_images

    # 清理
    cleanup

    echo ""
    log_success "数据库镜像打包完成!"
    echo ""
    echo "后续步骤："
    echo "  部署项目代码: ./deploy-to-remote.sh --env production"
    echo ""
}

# 执行
trap cleanup EXIT
main
