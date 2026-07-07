#!/usr/bin/env bash
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
#   ./deploy-to-remote.sh liteparse  # 仅更新/重启 LiteParse 服务
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
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
err()   { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# 获取脚本目录和项目根目录
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# ============================================================
# 时间统计工具
# ============================================================
# 格式化秒数为可读格式
format_duration() {
    local seconds=$1
    local mins=$((seconds / 60))
    local secs=$((seconds % 60))
    if [ $mins -gt 0 ]; then
        printf "%dm%02ds" $mins $secs
    else
        printf "%ds" $secs
    fi
}

# 时间报告临时文件（兼容 Bash 3.2，每行格式: component:phase:duration）
TIMING_FILE=$(mktemp /tmp/knowledgehub-deploy-timing.XXXXXX)
trap "rm -f '$TIMING_FILE'" EXIT

# 记录组件某个阶段的时间
# 用法: record_timing <component> <phase> <start_epoch> <end_epoch>
record_timing() {
    local component=$1
    local phase=$2
    local start=$3
    local end=$4
    local duration=$((end - start))
    echo "${component}:${phase}:${duration}" >> "$TIMING_FILE"
}

# 打印时间报告
print_timing_report() {
    local total_start=$1
    local total_end=$(date +%s)
    local total_duration=$((total_end - total_start))

    echo ""
    echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║                  部署时间报告                            ║${NC}"
    echo -e "${BOLD}${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"
    echo -e "${BOLD}${CYAN}║${NC} ${BOLD}阶段                │ 组件         │ 耗时            ${CYAN}║${NC}"
    echo -e "${BOLD}${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"

    # 从临时文件读取并输出
    while IFS=: read -r comp phase duration; do
        [ -z "$comp" ] && continue
        case "$phase" in
            build) local label="构建 (build)" ;;
            push)  local label="推送 (push)" ;;
            pull)  local label="拉取 (pull)" ;;
            *)     local label="$phase" ;;
        esac
        echo -e "${BOLD}${CYAN}║${NC} $(printf '%-20s' "$label") │ $(printf '%-12s' "$comp") │ $(printf '%-15s' "$(format_duration $duration)") ${CYAN}║${NC}"
    done < "$TIMING_FILE"

    echo -e "${BOLD}${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"
    echo -e "${BOLD}${CYAN}║${NC} ${BOLD}总计                                          $(printf '%-15s' "$(format_duration $total_duration)") ${CYAN}║${NC}"
    echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

# 全局部署开始时间
DEPLOY_START_TIME=$(date +%s)

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
# 构建 + 推送镜像（分阶段计时）
# ============================================================
build_and_push() {
    local component=$1
    local dockerfile=$2
    local context=$3
    local image_name="$REGISTRY/knowledgehub-$component:latest"

    # --- 构建 ---
    info "构建 $component 镜像..."
    local build_start=$(date +%s)

    cd "$context"
    docker buildx build \
        --platform linux/amd64 \
        -f "$dockerfile" \
        -t "$image_name" \
        --cache-from type=registry,ref=$REGISTRY/knowledgehub-$component:latest \
        --cache-to type=inline \
        --load \
        .

    local build_end=$(date +%s)
    record_timing "$component" "build" $build_start $build_end

    # 显示镜像大小
    local image_size=$(docker images "$image_name" --format "{{.Size}}")
    ok "$component 镜像构建完成 (${image_size}, $(format_duration $((build_end - build_start))))"

    # --- 推送 ---
    info "推送 $component 镜像到仓库..."
    local push_start=$(date +%s)

    docker push "$image_name"

    local push_end=$(date +%s)
    record_timing "$component" "push" $push_start $push_end

    ok "$component 镜像推送完成 ($(format_duration $((push_end - push_start))))"
}

# ============================================================
# 构建 Angular
# ============================================================
build_angular() {
    info "准备构建前端（Angular 首页 + 管理端）..."

    cd "$PROJECT_ROOT"

    if [ ! -d "$PROJECT_ROOT/angular/node_modules" ]; then
        info "安装 Angular npm 依赖..."
        (cd "$PROJECT_ROOT/angular" && npm install)
    fi

    # 构建并推送镜像
    build_and_push "angular" "angular/Dockerfile" "$PROJECT_ROOT"
}

# ============================================================
# 构建 API
# ============================================================
build_api() {
    info "构建 API..."
    build_and_push "api" "src/KnowledgeHub.HttpApi.Host/Dockerfile" "$PROJECT_ROOT"
}

# ============================================================
# 构建 DbMigrator
# ============================================================
build_migrator() {
    info "构建 DbMigrator..."
    build_and_push "db-migrator" "src/KnowledgeHub.DbMigrator/Dockerfile" "$PROJECT_ROOT"
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
# 远程部署（分阶段计时 pull 和 restart）
# ============================================================
remote_deploy() {
    local service="${1:-}"

    info "连接远程服务器执行部署..."

    if [ -z "$service" ]; then
        # 完整部署 - pull 计时
        local pull_start=$(date +%s)
        ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh pull || true
EOF
        local pull_end=$(date +%s)
        record_timing "all" "pull" $pull_start $pull_end
        ok "远程拉取镜像完成 ($(format_duration $((pull_end - pull_start))))"

        ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh down
./deploy.sh up
EOF
    else
        # 部分更新 - pull 计时
        local pull_start=$(date +%s)
        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
cd ~/knowledgehub
./deploy.sh pull || true
EOF
        local pull_end=$(date +%s)
        record_timing "$service" "pull" $pull_start $pull_end
        ok "远程拉取镜像完成 ($(format_duration $((pull_end - pull_start))))"

        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
cd ~/knowledgehub
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
# 验证 LiteParse 镜像（LiteParse 由外部项目构建推送，我们仅确认可达）
# ============================================================
verify_liteparse() {
    info "验证 LiteParse 镜像..."

    # 从 .env 解析 LITEPARSE_IMAGE；缺省回退到 ${REGISTRY}/knowledgehub-liteparse:${IMAGE_TAG}
    local env_file="$SCRIPT_DIR/.env"
    if [ ! -f "$env_file" ]; then
        warn ".env 不存在，无法解析 LITEPARSE_IMAGE，跳过验证（compose 启动时会报错）"
        return 0
    fi

    local liteparse_image
    liteparse_image=$(grep -E '^LITEPARSE_IMAGE=' "$env_file" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
    if [ -z "$liteparse_image" ]; then
        local registry image_tag
        registry=$(grep -E '^REGISTRY=' "$env_file" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
        image_tag=$(grep -E '^IMAGE_TAG=' "$env_file" | cut -d'=' -f2- | tr -d '"' | tr -d "'")
        liteparse_image="${registry}/knowledgehub-liteparse:${image_tag:-latest}"
    fi

    info "检查 LiteParse 镜像: $liteparse_image"
    if docker manifest inspect "$liteparse_image" &>/dev/null; then
        ok "LiteParse 镜像可达: $liteparse_image"
    else
        warn "无法访问 LiteParse 镜像: $liteparse_image"
        warn "  LiteParse 由外部项目构建推送，请确认镜像已就位"
        warn "  部署仍会继续，但启动 LiteParse 容器时会失败"
    fi
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
    verify_liteparse
    remote_deploy
    verify_deployment

    print_timing_report $DEPLOY_START_TIME
    ok "完整部署成功！"
    echo "访问地址: http://$REMOTE_HOST"
    echo ""
}

cmd_angular() {
    check_buildx
    build_angular
    remote_deploy "knowledgehub-angular"

    print_timing_report $DEPLOY_START_TIME
    ok "前端更新完成！"
}

cmd_api() {
    check_buildx
    build_api
    remote_deploy "knowledgehub-api"

    print_timing_report $DEPLOY_START_TIME
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

    print_timing_report $DEPLOY_START_TIME
    ok "迁移工具更新完成！"
}

cmd_sync() {
    sync_configs
    ok "配置文件同步完成！"
}

cmd_liteparse() {
    info "更新/重启 LiteParse 服务..."

    verify_liteparse

    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub
./deploy.sh pull knowledgehub-liteparse || true
./deploy.sh restart knowledgehub-liteparse
EOF

    ok "LiteParse 更新完成！"
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
    echo "  liteparse  仅更新/重启 LiteParse 服务（镜像由外部项目推送）"
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
    liteparse) cmd_liteparse ;;
    -h|--help) show_help ;;
    *)         show_help; exit 1 ;;
esac
