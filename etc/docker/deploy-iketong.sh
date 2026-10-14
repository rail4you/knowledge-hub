#!/usr/bin/env bash
# ============================================================
# deploy-iketong.sh - KnowledgeHub 部署到 47.99.245.49 (www.iketong.com)
# ============================================================
#
# 与 deploy-to-remote.sh 的区别（后者发布到 119.45.170.4 生产服务器）：
#   - REMOTE_HOST=47.99.245.49
#   - REMOTE_USER=root（用户说免密已配置；不是 ubuntu）
#   - REMOTE_DIR=~/knowledgehub-iketong
#   - 同步 .env.iketong / docker-compose-iketong.yml / nginx-proxy-iketong.conf
#   - 在宝塔面板创建 www.iketong.com 站点（反代 4200）
#   - 申请 Let's Encrypt SSL 证书
#
# 用法:
#   ./deploy-iketong.sh all        # 完整部署（构建所有镜像+部署）
#   ./deploy-iketong.sh angular    # 仅更新前端
#   ./deploy-iketong.sh api        # 仅更新后端
#   ./deploy-iketong.sh migrator   # 仅更新迁移工具并执行迁移
#   ./deploy-iketong.sh sync       # 仅同步配置文件
#   ./deploy-iketong.sh bt-site    # 仅创建/更新宝塔站点 www.iketong.com
#   ./deploy-iketong.sh bt-ssl     # 仅申请/续签 Let's Encrypt 证书
#   ./deploy-iketong.sh wasm       # 仅同步 WASM 仿真实训镜像
#   ./deploy-iketong.sh migrate-data   # 仅从 119.45.170.4 迁移数据库
#   ./deploy-iketong.sh status     # 检查服务状态
#
# ============================================================

set -e

# ============================================================
# iketong 服务器专属配置（与生产服务器 119.45.170.4 完全独立）
# ============================================================
REGISTRY="registry.cn-zhangjiakou.aliyuncs.com/myelixir"
REMOTE_HOST="47.99.245.49"
REMOTE_USER="root"
REMOTE_DIR="~/knowledgehub-iketong"

# 旧服务器（生产服务器）— 数据迁移源
SOURCE_HOST="119.45.170.4"
SOURCE_USER="ubuntu"
SOURCE_DIR="~/knowledgehub"

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

TIMING_FILE=$(mktemp /tmp/knowledgehub-iketong-timing.XXXXXX)
trap "rm -f '$TIMING_FILE'" EXIT

record_timing() {
    local component=$1
    local phase=$2
    local start=$3
    local end=$4
    local duration=$((end - start))
    echo "${component}:${phase}:${duration}" >> "$TIMING_FILE"
}

print_timing_report() {
    local total_start=$1
    local total_end=$(date +%s)
    local total_duration=$((total_end - total_start))

    echo ""
    echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║             iketong 部署时间报告 (47.99.245.49)          ║${NC}"
    echo -e "${BOLD}${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"
    echo -e "${BOLD}${CYAN}║${NC} ${BOLD}阶段                │ 组件         │ 耗时            ${CYAN}║${NC}"
    echo -e "${BOLD}${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"

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

DEPLOY_START_TIME=$(date +%s)

# ============================================================
# 检查 Docker Buildx
# ============================================================
check_buildx() {
    if ! docker buildx version &>/dev/null; then
        err "Docker Buildx 未安装，请先安装"
    fi
    if ! docker buildx inspect multiarch &>/dev/null; then
        info "创建 multiarch builder..."
        docker buildx create --name multiarch --use
    else
        docker buildx use multiarch
    fi
}

# ============================================================
# 构建 + 推送镜像
# ============================================================
build_and_push() {
    local component=$1
    local dockerfile=$2
    local context=$3
    local image_name="$REGISTRY/knowledgehub-$component:latest"

    info "构建 $component 镜像..."
    local build_start=$(date +%s)

    cd "$context"

    docker build \
        --platform linux/amd64 \
        -f "$dockerfile" \
        -t "$image_name" \
        .

    local build_end=$(date +%s)
    record_timing "$component" "build" $build_start $build_end

    local image_size=$(docker images "$image_name" --format "{{.Size}}")
    ok "$component 镜像构建完成 (${image_size}, $(format_duration $((build_end - build_start))))"

    info "推送 $component 镜像到仓库..."
    local push_start=$(date +%s)

    docker push "$image_name"

    local push_end=$(date +%s)
    record_timing "$component" "push" $push_start $push_end

    ok "$component 镜像推送完成 ($(format_duration $((push_end - push_start))))"
}

build_angular() {
    info "准备构建前端（Angular 首页 + 管理端）..."

    cd "$PROJECT_ROOT"

    if [ ! -d "$PROJECT_ROOT/angular/node_modules" ]; then
        info "安装 Angular npm 依赖..."
        (cd "$PROJECT_ROOT/angular" && npm install)
    fi

    build_and_push "angular" "angular/Dockerfile" "$PROJECT_ROOT"
}

build_api() {
    info "构建 API..."
    build_and_push "api" "src/KnowledgeHub.HttpApi.Host/Dockerfile" "$PROJECT_ROOT"
}

build_migrator() {
    info "构建 DbMigrator..."
    build_and_push "db-migrator" "src/KnowledgeHub.DbMigrator/Dockerfile" "$PROJECT_ROOT"
}

build_gotenberg() {
    info "构建 Gotenberg 镜像（含中文字体）..."
    build_and_push "gotenberg" "Dockerfile.gotenberg" "$SCRIPT_DIR"
}

# ============================================================
# 同步 WASM 镜像到远程
# ============================================================
sync_wasm_mirrors() {
    local src="$SCRIPT_DIR/wasm-mirrors"
    local dst="$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/wasm-mirrors"

    info "同步 WASM 镜像到 iketong 服务器..."

    ssh "$REMOTE_USER@$REMOTE_HOST" "mkdir -p $REMOTE_DIR/wasm-mirrors"
    if command -v rsync &>/dev/null; then
        rsync -avz --progress "$src/" "$dst/"
    else
        scp -r "$src/" "$dst/"
    fi

    ok "WASM 镜像同步完成"
}

# ============================================================
# 同步 iketong 服务器的配置文件（注意：使用 .env.iketong / docker-compose-iketong.yml / nginx-proxy-iketong.conf）
# ============================================================
sync_configs() {
    info "同步配置文件到 iketong 服务器 ($REMOTE_HOST)..."

    cd "$SCRIPT_DIR"

    ssh "$REMOTE_USER@$REMOTE_HOST" "mkdir -p $REMOTE_DIR"

    # 同步 iketong 专属配置
    scp .env.iketong \
        docker-compose-iketong.yml \
        nginx-proxy-iketong.conf \
        dynamic-env.template.json \
        deploy.sh \
        "$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/"

    # 同步 SSL 证书目录（暂用自签名；宝塔 Let's Encrypt 证书会自动签发并替换）
    ssh "$REMOTE_USER@$REMOTE_HOST" "mkdir -p $REMOTE_DIR/certs"
    scp certs/localhost.crt certs/localhost.key \
        "$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/certs/" 2>/dev/null || true

    # 在远程把 .env.iketong 复制为 .env（deploy.sh 默认读取 .env）
    ssh "$REMOTE_USER@$REMOTE_HOST" "cd $REMOTE_DIR && cp .env.iketong .env && cp docker-compose-iketong.yml docker-compose.yml && cp nginx-proxy-iketong.conf nginx-proxy.conf"

    ok "配置文件同步完成"
}

# ============================================================
# 配置宝塔站点 www.iketong.com（反代 4200）
# ============================================================
cmd_bt_site() {
    info "在宝塔面板创建站点 www.iketong.com（反代 4200）..."

    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
set -e

NGINX_VHOST_DIR="/www/server/panel/vhost/nginx"
CERT_DIR="/www/server/panel/vhost/cert"
WWWROOT="/www/wwwroot/iketong.com"

# 创建站点根目录
mkdir -p "$WWWROOT"
echo "<?php echo 'ok'; ?>" > "$WWWROOT/index.php" 2>/dev/null || echo "iketong ok" > "$WWWROOT/index.html"

# 宝塔站点配置（反代 4200 + SSL 终结）
cat > "$NGINX_VHOST_DIR/iketong.com.conf" << 'NGINX_CONF'
server {
        listen 80;
        listen [::]:80;
        listen 443 ssl;
        listen [::]:443 ssl;
        http2 on;
        server_name iketong.com www.iketong.com;

        #CERT-APPLY-CHECK--START
        include /www/server/panel/vhost/nginx/well-known/iketong.com.conf;
        #CERT-APPLY-CHECK--END
        include /www/server/panel/vhost/nginx/extension/iketong.com/*.conf;

        # 保留含下划线的请求头（nginx 默认 underscores_in_headers off 会直接丢弃）。
        # 必须开启：SPA 的密码授权用 __tenant 请求头传递租户，宝塔层丢弃后
        # /connect/token 会把请求当成宿主用户 → invalid_grant（登录后进工作台即被弹回）。
        underscores_in_headers on;

        #SSL-START
        set $isRedcert 1;
        if ($server_port != 443) {
                set $isRedcert 2;
        }
        if ( $uri ~ /\.well-known/ ) {
                set $isRedcert 1;
        }
        if ($isRedcert != 1) {
                rewrite ^(/.*)$ https://$host$1 permanent;
        }
        ssl_certificate    /www/server/panel/vhost/cert/iketong.com/fullchain.pem;
        ssl_certificate_key    /www/server/panel/vhost/cert/iketong.com/privkey.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers EECDH+CHACHA20:EECDH+AES128:RSA+AES128:EECDH+AES256:RSA+AES256:!MD5;
        ssl_prefer_server_ciphers on;
        ssl_session_tickets on;
        ssl_session_cache shared:SSL:10m;
        ssl_session_timeout 10m;
        add_header Strict-Transport-Security "max-age=31536000";
        error_page 497  https://$host$request_uri;
        #SSL-END

        # 大文件上传（视频/资源）
        client_max_body_size 500M;

        access_log /www/wwwlogs/iketong.com.log;
        error_log /www/wwwlogs/iketong.com.error.log;

        # 反代到 KnowledgeHub Angular 容器（宿主机 4200）
        location / {
                proxy_pass http://127.0.0.1:4200;
                proxy_http_version 1.1;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection upgrade;
                proxy_set_header Host $host;
                proxy_set_header X-Real-IP $remote_addr;
                proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                proxy_set_header X-Forwarded-Proto $scheme;
                proxy_set_header X-Forwarded-Host $host;
                proxy_set_header X-Forwarded-Port $server_port;
                proxy_cache_bypass $http_upgrade;
                proxy_buffering off;
                proxy_request_buffering off;
                proxy_read_timeout 3600s;
                proxy_connect_timeout 60s;
                proxy_send_timeout 300s;
        }
    }
NGINX_CONF

# 创建 well-known 验证目录（SSL 申请需要）
mkdir -p "$NGINX_VHOST_DIR/well-known"
cat > "$NGINX_VHOST_DIR/well-known/iketong.com.conf" << 'WELLKNOWN'
location ~ ^/\.well-known/acme-challenge/ {
    allow all;
    root /www/wwwroot/iketong.com;
}
WELLKNOWN

# 创建 extension 目录
mkdir -p "$NGINX_VHOST_DIR/extension/iketong.com"
touch "$NGINX_VHOST_DIR/extension/iketong.com/00_base.conf"

# 创建证书目录占位（宝塔 Let's Encrypt 申请时填充）
mkdir -p "$CERT_DIR/iketong.com"

# 校验配置 + reload
nginx -t
systemctl reload nginx

echo "✓ 宝塔站点 www.iketong.com 已配置"
EOF

    ok "宝塔站点配置完成"
}

# ============================================================
# 申请 Let's Encrypt SSL 证书（通过宝塔默认 acme 工具）
# ============================================================
cmd_bt_ssl() {
    info "申请 Let's Encrypt SSL 证书（iketong.com）..."

    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
set -e

CERT_DIR="/www/server/panel/vhost/cert/iketong.com"
WELLKNOWN_DIR="/www/wwwroot/iketong.com/.well-known/acme-challenge"
mkdir -p "$WELLKNOWN_DIR"

# 优先尝试 acme.sh（宝塔默认安装）
if command -v ~/.acme.sh/acme.sh &>/dev/null; then
    echo "使用 ~/.acme.sh 申请证书..."
    ~/.acme.sh/acme.sh --issue -d iketong.com -d www.iketong.com \
        --webroot /www/wwwroot/iketong.com \
        --reloadcmd "systemctl reload nginx" \
        --force || {
                echo "acme.sh 失败，尝试宝塔 BT 命令..."
            }
fi

# 宝塔内置命令
if [ ! -f "$CERT_DIR/fullchain.pem" ]; then
    echo "尝试宝塔 BT 命令..."
    bt ssl iketong.com 2>&1 || echo "BT 命令失败，需手动在面板申请"
fi

# 检查最终结果
if [ -f "$CERT_DIR/fullchain.pem" ]; then
    echo "✓ 证书已就绪"
    ls -la "$CERT_DIR"
else
    echo "✗ 证书申请失败，请登录宝塔面板手动申请 Let's Encrypt 证书"
    echo "  面板入口: https://bt.yikecheng.com:16375"
    echo "  站点: www.iketong.com → 设置 → SSL → Let's Encrypt → 申请"
    exit 1
fi
EOF

    ok "SSL 证书申请完成"
}

# ============================================================
# 远程部署
# ============================================================
remote_deploy() {
    local service="${1:-}"

    info "连接远程服务器 $REMOTE_HOST 执行部署..."

    if [ -z "$service" ]; then
        local pull_start=$(date +%s)
        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
set -e
cd $REMOTE_DIR
./deploy.sh pull || true
EOF
        local pull_end=$(date +%s)
        record_timing "all" "pull" $pull_start $pull_end
        ok "远程拉取镜像完成 ($(format_duration $((pull_end - pull_start))))"

        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
set -e
cd $REMOTE_DIR
./deploy.sh down
./deploy.sh up
EOF
    else
        local pull_start=$(date +%s)
        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
set -e
cd $REMOTE_DIR
./deploy.sh pull || true
EOF
        local pull_end=$(date +%s)
        record_timing "$service" "pull" $pull_start $pull_end
        ok "远程拉取镜像完成 ($(format_duration $((pull_end - pull_start))))"

        ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
set -e
cd $REMOTE_DIR
./deploy.sh restart $service
EOF
    fi

    ok "远程部署完成"
}

# ============================================================
# 数据库迁移：从 119.45.170.4 导出，导入到 47.99.245.49
# ============================================================
cmd_migrate_data() {
    info "从 $SOURCE_HOST 迁移数据到 $REMOTE_HOST..."

    local DUMP_FILE="/tmp/knowledgehub_migrate_$(date +%Y%m%d_%H%M%S).sql"
    local ARCHIVE_DIR="/tmp/knowledgehub_migrate_$(date +%Y%m%d_%H%M%S)"

    # 1. 在源服务器导出 PostgreSQL
    info "步骤 1/4：从 $SOURCE_HOST 导出 PostgreSQL..."
    ssh "$SOURCE_USER@$SOURCE_HOST" << EOF
docker exec postgres pg_dump -U postgres -Fc --no-owner --no-acl KnowledgeHub > /tmp/kh_dump_$(date +%Y%m%d).dump
ls -lh /tmp/kh_dump_*.dump
EOF

    # 2. 把 dump + uploads 传到目标服务器
    info "步骤 2/4：传输 dump 和 uploads 到 $REMOTE_HOST..."
    ssh "$SOURCE_USER@$SOURCE_HOST" "ls -lh /tmp/kh_dump_*.dump"
    local LATEST_DUMP=$(ssh "$SOURCE_USER@$SOURCE_HOST" "ls -t /tmp/kh_dump_*.dump | head -1")
    info "Dump file: $LATEST_DUMP"

    scp "$SOURCE_USER@$SOURCE_HOST:$LATEST_DUMP" "$REMOTE_USER@$REMOTE_HOST:/tmp/kh_latest.dump"

    # 传输 uploads（如果存在且需要）
    info "传输 uploads 目录..."
    ssh "$SOURCE_USER@$SOURCE_HOST" "ls -lh ~/knowledgehub/uploads/ 2>/dev/null | head -3 || echo 'uploads 目录不存在'"
    rsync -avz --progress "$SOURCE_USER@$SOURCE_HOST:~/knowledgehub/uploads/" "$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/uploads/" 2>&1 | tail -10 || warn "uploads 同步失败（可能目录为空）"

    # 3. 在目标服务器启动 PostgreSQL（确保容器在跑）
    info "步骤 3/4：启动 PostgreSQL 容器并导入数据..."
    ssh "$REMOTE_USER@$REMOTE_HOST" << EOF
set -e
cd $REMOTE_DIR

# 确保 postgres 容器在跑
docker compose up -d postgres
sleep 5

# 等待 postgres healthy
for i in \$(seq 1 30); do
    if docker exec postgres pg_isready -U postgres &>/dev/null; then
        echo "PostgreSQL 已就绪"
        break
    fi
    echo "等待 PostgreSQL 启动 (\$i/30)..."
    sleep 2
done

# 导入数据
echo "导入数据库（可能耗时数十秒）..."
docker exec -i postgres pg_restore -U postgres -d --no-owner --no-acl --clean --if-exists < /tmp/kh_latest.dump 2>&1 | tail -10
docker exec -i postgres psql -U postgres -d KnowledgeHub -c "SELECT count(*) FROM \"AbpUsers\";" 2>&1
EOF

    # 4. 同步 WASM 镜像（如果需要）
    info "步骤 4/4：同步 WASM 镜像..."
    ssh "$SOURCE_USER@$SOURCE_HOST" "ls ~/knowledgehub/wasm-mirrors/ 2>/dev/null | head -5 || echo 'WASM 目录为空'"
    rsync -avz --progress --ignore-existing "$SOURCE_USER@$SOURCE_HOST:~/knowledgehub/wasm-mirrors/" "$REMOTE_USER@$REMOTE_HOST:$REMOTE_DIR/wasm-mirrors/" 2>&1 | tail -5 || warn "WASM 同步失败"

    ok "数据迁移完成"
}

# ============================================================
# 验证部署
# ============================================================
cmd_status() {
    info "检查 iketong 服务器部署状态..."

    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub-iketong
echo "=== 容器状态 ==="
docker compose ps
echo ""
echo "=== 端口监听 ==="
ss -tlnp | grep -E "4200|5433|6380|7701|3001|15001" || echo "无预期端口在监听"
echo ""
echo "=== 域名健康检查 ==="
echo "[API]"
curl -skf https://www.iketong.com/api/abp/api-definition > /dev/null && echo "API 正常" || echo "API 未就绪"
echo "[前端]"
curl -skf https://www.iketong.com/ > /dev/null && echo "前端正常" || echo "前端未就绪"
EOF

    ok "状态检查完成"
}

# ============================================================
# 主命令
# ============================================================
cmd_all() {
    check_buildx
    build_migrator
    build_api
    build_angular
    build_gotenberg
    sync_configs
    cmd_bt_site
    cmd_bt_ssl
    sync_wasm_mirrors
    remote_deploy
    cmd_status

    print_timing_report $DEPLOY_START_TIME
    ok "完整部署成功！"
    echo ""
    echo "访问地址: https://www.iketong.com"
    echo "API 地址: https://www.iketong.com/api/abp/api-definition"
    echo ""
    echo "注意：首次部署完成后立即执行 ./deploy-iketong.sh migrate-data"
    echo "      从生产服务器 119.45.170.4 迁移数据库数据"
    echo ""
}

cmd_angular() {
    build_angular
    remote_deploy "knowledgehub-angular"
    print_timing_report $DEPLOY_START_TIME
    ok "前端更新完成！"
}

cmd_api() {
    build_api
    remote_deploy "knowledgehub-api"
    print_timing_report $DEPLOY_START_TIME
    ok "后端更新完成！"
}

cmd_migrator() {
    build_migrator
    info "执行数据库迁移..."
    ssh "$REMOTE_USER@$REMOTE_HOST" << 'EOF'
cd ~/knowledgehub-iketong
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

cmd_wasm() {
    sync_wasm_mirrors
    info "重启 Angular 容器以刷新 Nginx 静态文件..."
    ssh "$REMOTE_USER@$REMOTE_HOST" "cd ~/knowledgehub-iketong && ./deploy.sh restart knowledgehub-angular"
    ok "WASM 镜像同步完成！"
}

# ============================================================
# 帮助
# ============================================================
show_help() {
    echo ""
    echo "KnowledgeHub 部署到 iketong 服务器 (47.99.245.49)"
    echo ""
    echo "用法: $0 <命令>"
    echo ""
    echo "命令:"
    echo "  all              完整部署（构建所有镜像+部署）"
    echo "  angular          仅更新前端"
    echo "  api              仅更新后端"
    echo "  migrator         仅更新迁移工具并执行迁移"
    echo "  sync             仅同步配置文件"
    echo "  bt-site          仅创建/更新宝塔站点 www.iketong.com"
    echo "  bt-ssl           仅申请/续签 Let's Encrypt 证书"
    echo "  wasm             仅同步 WASM 镜像到远程"
    echo "  migrate-data     从 119.45.170.4 迁移数据库数据"
    echo "  status           检查部署状态"
    echo ""
    echo "首次部署流程："
    echo "  1. $0 all          # 构建镜像+部署+宝塔站点+SSL"
    echo "  2. $0 migrate-data # 从旧服务器迁移数据库"
    echo ""
    echo "首次部署前请确保:"
    echo "  1. 已登录阿里云镜像仓库: docker login $REGISTRY"
    echo "  2. SSH 免密登录已配置（root@47.99.245.49）"
    echo "  3. .env.iketong 中的 TURN_SECRET 已生成: openssl rand -base64 64"
    echo ""
}

case "${1:-}" in
    all)           cmd_all ;;
    angular)       cmd_angular ;;
    api)           cmd_api ;;
    migrator)      cmd_migrator ;;
    sync)          cmd_sync ;;
    bt-site)       cmd_bt_site ;;
    bt-ssl)        cmd_bt_ssl ;;
    wasm)          cmd_wasm ;;
    migrate-data)  cmd_migrate_data ;;
    status)        cmd_status ;;
    -h|--help)     show_help ;;
    *)             show_help; exit 1 ;;
esac