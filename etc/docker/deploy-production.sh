#!/bin/bash
# ============================================================
# deploy-production.sh - KnowledgeHub 生产环境部署脚本
# ============================================================
# 
# 功能:
#   1. 读取 .env.production 配置
#   2. 自动生成 dynamic-env.json
#   3. 部署到远程服务器（SSH）
#   4. 支持本地 Docker Compose 启动
#
# 使用方式:
#   ./deploy-production.sh --init          # 初始化配置文件
#   ./deploy-production.sh --local         # 本地启动服务
#   ./deploy-production.sh --deploy        # 部署到远程服务器
#   ./deploy-production.sh --status        # 查看远程服务状态
#   ./deploy-production.sh --logs [service]# 查看远程日志
#   ./deploy-production.sh --restart [svc] # 重启远程服务
#
# 前置要求:
#   - sshpass（用于 SSH 密码登录）
#   - docker & docker-compose
#   - jq（用于 JSON 处理）
# ============================================================

set -e

# ============================================================
# 配置
# ============================================================
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env.production"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"
DOCKER_COMPOSE="$SCRIPT_DIR/docker-compose.yml"
DOCKER_COMPOSE_PROD="$SCRIPT_DIR/docker-compose.prod.yml"
DYNAMIC_ENV_TEMPLATE="$SCRIPT_DIR/dynamic-env.template.json"
DYNAMIC_ENV_OUTPUT="$SCRIPT_DIR/dynamic-env.json"

# 默认值
SKIP_BUILD=false
DRY_RUN=false

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
    echo "KnowledgeHub 生产环境部署脚本"
    echo ""
    echo "用法: $0 [COMMAND] [OPTIONS]"
    echo ""
    echo "命令:"
    echo "  --init              初始化配置文件（从 .env.example 复制）"
    echo "  --local             本地启动服务（使用 Docker Compose）"
    echo "  --deploy            部署到远程服务器"
    echo "  --status            查看远程服务状态"
    echo "  --logs [service]    查看远程日志（可选服务名）"
    echo "  --restart [service] 重启远程服务（可选服务名，默认全部）"
    echo "  --stop              停止远程服务"
    echo "  --help              显示帮助"
    echo ""
    echo "选项:"
    echo "  --skip-build        跳过镜像构建（--deploy 时有效）"
    echo "  --dry-run           演练模式，不实际执行"
    echo ""
    echo "示例:"
    echo "  $0 --init                      # 初始化配置"
    echo "  $0 --local                     # 本地启动"
    echo "  $0 --deploy                    # 完整部署"
    echo "  $0 --deploy --skip-build       # 部署但不重新构建镜像"
    echo "  $0 --logs api                  # 查看 API 日志"
    echo "  $0 --restart knowledgehub-api  # 重启 API 服务"
    echo ""
}

# ============================================================
# 解析参数
# ============================================================
COMMAND=""
SERVICE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --init)
            COMMAND="init"
            shift
            ;;
        --local)
            COMMAND="local"
            shift
            ;;
        --deploy)
            COMMAND="deploy"
            shift
            ;;
        --status)
            COMMAND="status"
            shift
            ;;
        --logs)
            COMMAND="logs"
            if [[ $# -gt 1 && ! "$2" =~ ^-- ]]; then
                SERVICE="$2"
                shift
            fi
            shift
            ;;
        --restart)
            COMMAND="restart"
            if [[ $# -gt 1 && ! "$2" =~ ^-- ]]; then
                SERVICE="$2"
                shift
            fi
            shift
            ;;
        --stop)
            COMMAND="stop"
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help|-h)
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

if [ -z "$COMMAND" ]; then
    show_help
    exit 0
fi

# ============================================================
# 加载环境变量
# ============================================================
load_env() {
    if [ ! -f "$ENV_FILE" ]; then
        log_error "配置文件不存在: $ENV_FILE"
        log_info "请先运行: $0 --init"
        exit 1
    fi
    
    log_info "加载配置: $ENV_FILE"
    set -a
    source "$ENV_FILE"
    set +a
    
    # 验证必要变量
    local required_vars="SERVER_HOST APP_SELF_URL APP_ANGULAR_URL POSTGRES_PASSWORD"
    for var in $required_vars; do
        if [ -z "${!var}" ]; then
            log_error "缺少必要配置: $var"
            exit 1
        fi
    done
}

# ============================================================
# 生成 dynamic-env.json
# ============================================================
generate_dynamic_env() {
    log_info "生成 dynamic-env.json..."
    
    if [ ! -f "$DYNAMIC_ENV_TEMPLATE" ]; then
        log_error "模板文件不存在: $DYNAMIC_ENV_TEMPLATE"
        exit 1
    fi
    
    # 使用 envsubst 替换变量
    envsubst < "$DYNAMIC_ENV_TEMPLATE" > "$DYNAMIC_ENV_OUTPUT"
    
    log_success "生成完成: $DYNAMIC_ENV_OUTPUT"
    cat "$DYNAMIC_ENV_OUTPUT"
}

# ============================================================
# 初始化配置
# ============================================================
cmd_init() {
    log_info "初始化配置文件..."
    
    if [ -f "$ENV_FILE" ]; then
        log_warn "配置文件已存在: $ENV_FILE"
        read -p "是否覆盖? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            log_info "取消操作"
            exit 0
        fi
    fi
    
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    log_success "已创建: $ENV_FILE"
    log_info "请编辑配置文件后运行: $0 --deploy"
    
    # 尝试用编辑器打开
    if command -v code &> /dev/null; then
        code "$ENV_FILE"
    elif command -v nano &> /dev/null; then
        nano "$ENV_FILE"
    fi
}

# ============================================================
# 本地启动
# ============================================================
cmd_local() {
    load_env
    generate_dynamic_env
    
    log_info "本地启动服务..."
    
    cd "$SCRIPT_DIR"
    
    # 创建必要目录
    mkdir -p uploads meilisearch_data certs
    
    # 启动服务
    docker-compose \
        --env-file "$ENV_FILE" \
        -f "$DOCKER_COMPOSE" \
        -f "$DOCKER_COMPOSE_PROD" \
        up -d
    
    log_success "服务已启动"
    echo ""
    echo "访问地址:"
    echo "  Angular:  $APP_ANGULAR_URL"
    echo "  API:      $APP_SELF_URL"
    echo "  Swagger:  $APP_SELF_URL/swagger"
    echo ""
}

# ============================================================
# SSH 执行远程命令
# ============================================================
remote_exec() {
    local cmd="$1"
    if [ "$DRY_RUN" = true ]; then
        log_info "[DRY-RUN] SSH: $cmd"
    else
        sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 \
            -p "${SERVER_PORT:-22}" \
            "$SERVER_USER@$SERVER_HOST" "$cmd"
    fi
}

# ============================================================
# SCP 传输文件
# ============================================================
scp_push() {
    local local_path="$1"
    local remote_path="$2"
    if [ "$DRY_RUN" = true ]; then
        log_info "[DRY-RUN] SCP: $local_path -> $SERVER_USER@$SERVER_HOST:$remote_path"
    else
        sshpass -p "$SERVER_PASSWORD" scp -o StrictHostKeyChecking=no -P "${SERVER_PORT:-22}" \
            -r "$local_path" "$SERVER_USER@$SERVER_HOST:$remote_path"
    fi
}

# ============================================================
# 部署到远程
# ============================================================
cmd_deploy() {
    load_env
    generate_dynamic_env
    
    echo ""
    echo "============================================"
    echo "  KnowledgeHub 生产环境部署"
    echo "============================================"
    echo ""
    echo "服务器: $SERVER_HOST"
    echo "API:    $APP_SELF_URL"
    echo "Web:    $APP_ANGULAR_URL"
    echo ""
    
    # 测试 SSH 连接
    log_info "测试 SSH 连接..."
    if ! remote_exec "echo 'SSH 连接成功'" &>/dev/null; then
        log_error "SSH 连接失败，请检查配置"
        exit 1
    fi
    log_success "SSH 连接成功"
    
    REMOTE_DIR="/home/ubuntu/knowledgehub"
    TIMESTAMP=$(date +%Y%m%d_%H%M%S)
    DEPLOY_DIR="$REMOTE_DIR/deploy_$TIMESTAMP"
    
    # 创建部署目录
    remote_exec "mkdir -p $DEPLOY_DIR/uploads $DEPLOY_DIR/meilisearch_data"
    
    # 传输配置文件
    log_info "传输配置文件..."
    scp_push "$ENV_FILE" "$DEPLOY_DIR/.env"
    scp_push "$DYNAMIC_ENV_OUTPUT" "$DEPLOY_DIR/dynamic-env.json"
    scp_push "$DOCKER_COMPOSE" "$DEPLOY_DIR/docker-compose.yml"
    scp_push "$DOCKER_COMPOSE_PROD" "$DEPLOY_DIR/docker-compose.prod.yml"
    
    # 在远程执行部署
    log_info "远程部署..."
    remote_exec << EOF
cd $DEPLOY_DIR

# 停止旧容器
echo "停止旧容器..."
docker stop knowledgehub-api knowledgehub-angular 2>/dev/null || true
docker rm knowledgehub-api knowledgehub-angular 2>/dev/null || true

# 启动新容器
echo "启动新容器..."
docker-compose --env-file .env -f docker-compose.yml -f docker-compose.prod.yml up -d

# 等待启动
sleep 10

# 显示状态
echo ""
echo "=== 容器状态 ==="
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep -E "knowledgehub|postgres|redis|meilisearch"

# 清理旧部署目录（保留最近 5 个）
cd $REMOTE_DIR
ls -td deploy_* 2>/dev/null | tail -n +6 | xargs rm -rf 2>/dev/null || true
EOF
    
    log_success "部署完成!"
    echo ""
    echo "访问地址:"
    echo "  Angular:  $APP_ANGULAR_URL"
    echo "  API:      $APP_SELF_URL"
    echo "  Swagger:  $APP_SELF_URL/swagger"
    echo ""
}

# ============================================================
# 查看状态
# ============================================================
cmd_status() {
    load_env
    
    echo "=== 远程服务状态 ==="
    remote_exec "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | grep -E 'knowledgehub|postgres|redis|meilisearch'"
    
    echo ""
    echo "=== 测试 API ==="
    remote_exec "curl -s http://localhost:44354/api/abp/application-configuration | jq -r '.auth.policies | keys | .[0]' 2>/dev/null && echo 'API 正常' || echo 'API 未就绪'"
    
    echo ""
    echo "=== 测试 CORS ==="
    remote_exec "curl -s -I -X OPTIONS http://localhost:44354/api/abp/application-configuration -H 'Origin: $APP_ANGULAR_URL' 2>/dev/null | grep -i 'access-control' || echo '无 CORS 头'"
}

# ============================================================
# 查看日志
# ============================================================
cmd_logs() {
    load_env
    
    local svc="${SERVICE:-knowledgehub-api}"
    log_info "查看 $svc 日志..."
    remote_exec "docker logs --tail 100 -f $svc 2>&1"
}

# ============================================================
# 重启服务
# ============================================================
cmd_restart() {
    load_env
    
    if [ -z "$SERVICE" ]; then
        log_info "重启所有服务..."
        remote_exec "cd /home/ubuntu/knowledgehub/deploy_* && docker-compose restart"
    else
        log_info "重启 $SERVICE..."
        remote_exec "docker restart $SERVICE"
    fi
    
    log_success "重启完成"
}

# ============================================================
# 停止服务
# ============================================================
cmd_stop() {
    load_env
    
    log_info "停止远程服务..."
    remote_exec "docker stop knowledgehub-api knowledgehub-angular 2>/dev/null || true"
    
    log_success "服务已停止"
}

# ============================================================
# 主流程
# ============================================================
case $COMMAND in
    init)
        cmd_init
        ;;
    local)
        cmd_local
        ;;
    deploy)
        cmd_deploy
        ;;
    status)
        cmd_status
        ;;
    logs)
        cmd_logs
        ;;
    restart)
        cmd_restart
        ;;
    stop)
        cmd_stop
        ;;
    *)
        log_error "未知命令: $COMMAND"
        show_help
        exit 1
        ;;
esac
