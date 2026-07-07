#!/bin/bash
# ============================================================
# deploy.sh - KnowledgeHub 远程服务器管理脚本
# ============================================================
#
# 在远程服务器上使用:
#   ./deploy.sh up              # 启动所有服务
#   ./deploy.sh down            # 停止所有服务
#   ./deploy.sh restart [svc]   # 重启服务
#   ./deploy.sh migrate         # 执行数据库迁移
#   ./deploy.sh logs [service]  # 查看日志
#   ./deploy.sh status          # 查看服务状态
#   ./deploy.sh pull            # 拉取最新镜像
#   ./deploy.sh gen-env         # 生成 dynamic-env.json
#
# 服务名（restart / logs 接受任意服务名）:
#   knowledgehub-angular  knowledgehub-api  knowledgehub-liteparse
#   meilisearch  postgres  redis  db-migrator
#
# ============================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
TEMPLATE_FILE="$SCRIPT_DIR/dynamic-env.template.json"
DYNAMIC_ENV_FILE="$SCRIPT_DIR/dynamic-env.json"

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

# ============================================================
# 加载环境变量
# ============================================================
load_env() {
    if [ ! -f "$ENV_FILE" ]; then
        err "配置文件不存在: $ENV_FILE\n请先运行: cp .env.example .env && vim .env"
    fi
    set -a
    source "$ENV_FILE"
    set +a

    if [ -z "$PUBLIC_URL" ]; then
        err "缺少必要配置: PUBLIC_URL"
    fi

    # 给 LiteParse 相关变量设默认值（compose 文件不再写 shell 默认值语法，
    # 因为远端的 docker-compose v1 不支持 ${VAR:-default} 语法）
    : "${LITEPARSE_IMAGE:=${REGISTRY:-registry.cn-zhangjiakou.aliyuncs.com/myelixir}/knowledgehub-liteparse:${IMAGE_TAG:-latest}}"
    : "${LITEPARSE_TIMEOUT:=300}"
    : "${LITEPARSE_DPI:=300}"
    export LITEPARSE_IMAGE LITEPARSE_TIMEOUT LITEPARSE_DPI
}

# ============================================================
# 生成 dynamic-env.json
# ============================================================
generate_dynamic_env() {
    if [ ! -f "$TEMPLATE_FILE" ]; then
        err "模板文件不存在: $TEMPLATE_FILE"
    fi
    info "生成 dynamic-env.json (PUBLIC_URL=$PUBLIC_URL)"
    # 只替换模板中的变量，避免 shell 变量被意外展开
    envsubst '$PUBLIC_URL' < "$TEMPLATE_FILE" > "$DYNAMIC_ENV_FILE"
    ok "生成完成"
}

# ============================================================
# Docker Compose 命令
# 兼容 docker-compose v1（不支持 --env-file，用环境变量传递）
# ============================================================
compose() {
    if docker compose version &>/dev/null; then
        docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
    else
        docker-compose -f "$COMPOSE_FILE" "$@"
    fi
}

# ============================================================
# 命令实现
# ============================================================
cmd_up() {
    load_env
    generate_dynamic_env

    info "创建必要目录..."
    mkdir -p "$SCRIPT_DIR/uploads" "$SCRIPT_DIR/meilisearch_data" "$SCRIPT_DIR/postgres_data"

    # 确保 uploads/ 对 API 容器进程（UID 1654 = $APP_UID in Dockerfile）可写。
    # 容器内进程的写入能力取决于宿主机 bind-mount 目录的所有者；
    # 用 1654 chown 后 dotnet 进程可创建按日期分割的子目录。
    # nogroup 不影响普通存储使用；这是 bind mount 的本质约束。
    if [ -d "$SCRIPT_DIR/uploads" ]; then
        chown -R 1654:1654 "$SCRIPT_DIR/uploads" 2>/dev/null || \
            chmod -R 0777 "$SCRIPT_DIR/uploads"
    fi

    info "拉取最新镜像..."
    compose pull 2>/dev/null || true

    info "启动服务..."
    compose up -d

    echo ""
    ok "服务已启动"
    echo ""
    echo "访问地址:"
    echo "  前端: $PUBLIC_URL"
    echo "  API:  $PUBLIC_URL/api/abp/api-definition"
    echo ""
}

cmd_down() {
    load_env
    info "停止服务..."
    compose down
    ok "服务已停止"
}

cmd_restart() {
    load_env
    local svc="${1:-}"
    if [ -z "$svc" ]; then
        info "重启所有服务（down + up）..."
        compose down
        compose up -d
    else
        info "重建 $svc（stop + rm + up）..."
        compose stop "$svc" 2>/dev/null || true
        compose rm -f "$svc" 2>/dev/null || true
        compose up -d --no-deps "$svc"
    fi
    ok "重启完成"
}

cmd_migrate() {
    load_env
    info "执行数据库迁移..."
    compose run --rm db-migrator
    ok "迁移完成"
}

cmd_logs() {
    load_env
    local svc="${1:-}"
    if [ -z "$svc" ]; then
        compose logs --tail=100 -f
    else
        compose logs --tail=100 -f "$svc"
    fi
}

cmd_status() {
    load_env
    echo "=== 容器状态 ==="
    compose ps
    echo ""
    echo "=== 健康检查 ==="
    # 注：用 -k (--insecure) 是因为生产部署用的是自签名 localhost.crt；
    # 上线正式证书后可去掉。返回 200 才算正常。
    curl -skf "$PUBLIC_URL/health-status" > /dev/null && echo "API: 正常" || echo "API: 未就绪"
    echo ""
    curl -skf "$PUBLIC_URL/" > /dev/null && echo "前端: 正常" || echo "前端: 未就绪"
    echo ""
    # LiteParse 健康检查：直接读容器 healthcheck 状态，避免在 API 容器里
    # 调用 wget/curl/python（生产 API 镜像未装这些工具）
    local liteparse_state
    liteparse_state=$(docker inspect --format='{{.State.Health.Status}}' knowledgehub-liteparse 2>/dev/null || echo "absent")
    case "$liteparse_state" in
        healthy)        echo "LiteParse: 正常" ;;
        starting)       echo "LiteParse: 启动中" ;;
        unhealthy)      echo "LiteParse: 不健康" ;;
        none|absent|"") echo "LiteParse: 跳过（容器未运行或无 healthcheck 配置）" ;;
        *)              echo "LiteParse: 未知状态 ($liteparse_state)" ;;
    esac
}

cmd_pull() {
    load_env
    info "拉取最新镜像..."
    compose pull
    ok "镜像拉取完成"
}

cmd_gen_env() {
    load_env
    generate_dynamic_env
    echo "--- dynamic-env.json ---"
    cat "$DYNAMIC_ENV_FILE"
}

# ============================================================
# 帮助
# ============================================================
show_help() {
    echo ""
    echo "KnowledgeHub 远程部署管理"
    echo ""
    echo "用法: $0 <命令> [参数]"
    echo ""
    echo "命令:"
    echo "  up              启动所有服务"
    echo "  down            停止所有服务"
    echo "  restart [svc]   重启服务（可选: knowledgehub-api, knowledgehub-angular, knowledgehub-liteparse, meilisearch, postgres, redis 等）"
    echo "  migrate         执行数据库迁移"
    echo "  pull            拉取最新镜像"
    echo "  logs [svc]      查看日志（可选服务名）"
    echo "  status          查看服务状态"
    echo "  gen-env         生成 dynamic-env.json"
    echo ""
}

# ============================================================
# 主程序
# ============================================================
case "${1:-}" in
    up)        cmd_up ;;
    down)      cmd_down ;;
    restart)   cmd_restart "$2" ;;
    migrate)   cmd_migrate ;;
    pull)      cmd_pull ;;
    logs)      cmd_logs "$2" ;;
    status)    cmd_status ;;
    gen-env)   cmd_gen_env ;;
    -h|--help) show_help ;;
    *)         show_help; exit 1 ;;
esac
