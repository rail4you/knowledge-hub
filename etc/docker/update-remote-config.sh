#!/bin/bash
# ============================================================
# update-remote-config.sh - 更新远程服务器配置
# ============================================================
# 
# 用于快速更新远程服务器的环境变量，无需重新部署
#
# 使用方式:
#   ./update-remote-config.sh                    # 交互式配置
#   ./update-remote-config.sh --ip 119.45.170.4  # 指定 IP
#   ./update-remote-config.sh --show             # 显示当前配置
#
# ============================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env.production"

# 加载环境变量
if [ -f "$ENV_FILE" ]; then
    set -a
    source "$ENV_FILE"
    set +a
fi

# 默认值
SERVER_HOST="${SERVER_HOST:-119.45.170.4}"
SERVER_PORT="${SERVER_PORT:-22}"
SERVER_USER="${SERVER_USER:-ubuntu}"
SERVER_PASSWORD="${SERVER_PASSWORD:-}"

SHOW_ONLY=false
NEW_IP=""

# 解析参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --ip)
            NEW_IP="$2"
            shift 2
            ;;
        --show)
            SHOW_ONLY=true
            shift
            ;;
        --help|-h)
            echo "用法: $0 [--ip IP] [--show]"
            exit 0
            ;;
        *)
            echo "未知参数: $1"
            exit 1
            ;;
    esac
done

# ============================================================
# SSH 执行
# ============================================================
remote_exec() {
    local cmd="$1"
    sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no -p "$SERVER_PORT" \
        "$SERVER_USER@$SERVER_HOST" "$cmd"
}

# ============================================================
# 显示当前配置
# ============================================================
show_config() {
    echo "=== 当前远程容器环境变量 ==="
    remote_exec "docker inspect knowledgehub-api --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^App__|^AuthServer__|^SERVER' || true"
    
    echo ""
    echo "=== dynamic-env.json ==="
    remote_exec "docker exec knowledgehub-angular cat /usr/share/nginx/html/dynamic-env.json 2>/dev/null || echo '文件不存在'"
}

# ============================================================
# 更新配置
# ============================================================
update_config() {
    local new_ip="$1"
    
    if [ -z "$new_ip" ]; then
        read -p "请输入新的服务器 IP: " new_ip
    fi
    
    if [ -z "$new_ip" ]; then
        echo "IP 不能为空"
        exit 1
    fi
    
    echo ""
    echo "将更新以下配置:"
    echo "  APP_ANGULAR_URL=http://$new_ip"
    echo "  APP_SELF_URL=http://$new_ip:44354"
    echo "  APP_CORS_ORIGINS=http://localhost:4200,http://$new_ip"
    echo ""
    read -p "确认更新? (y/N): " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "取消操作"
        exit 0
    fi
    
    # 更新本地 .env.production
    if [ -f "$ENV_FILE" ]; then
        sed -i.bak "s|SERVER_HOST=.*|SERVER_HOST=$new_ip|g" "$ENV_FILE"
        sed -i.bak "s|APP_ANGULAR_URL=.*|APP_ANGULAR_URL=http://$new_ip|g" "$ENV_FILE"
        sed -i.bak "s|APP_SELF_URL=.*|APP_SELF_URL=http://$new_ip:44354|g" "$ENV_FILE"
        sed -i.bak "s|APP_CORS_ORIGINS=.*|APP_CORS_ORIGINS=http://localhost:4200,http://$new_ip|g" "$ENV_FILE"
        sed -i.bak "s|APP_REDIRECT_URLS=.*|APP_REDIRECT_URLS=http://localhost:4200,http://$new_ip|g" "$ENV_FILE"
        rm -f "${ENV_FILE}.bak"
        echo "已更新本地配置: $ENV_FILE"
    fi
    
    # 更新远程 API 容器
    echo ""
    echo "更新远程 API 容器..."
    remote_exec << EOF
# 停止旧容器
docker stop knowledgehub-api 2>/dev/null || true
docker rm knowledgehub-api 2>/dev/null || true

# 获取网络
NETWORK=\$(docker ps --filter "name=postgres" --format "{{.Networks}}" | head -1)

# 启动新容器
docker run -d \
  --name knowledgehub-api \
  --network \$NETWORK \
  -p 44354:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:80 \
  -e "ConnectionStrings__Default=Host=postgres;Port=5432;Database=KnowledgeHub;User ID=postgres;Password=postgres" \
  -e App__SelfUrl=http://$new_ip:44354 \
  -e App__AngularUrl=http://$new_ip \
  -e "App__CorsOrigins=http://localhost:4200,http://$new_ip" \
  -e "App__RedirectAllowedUrls=http://localhost:4200,http://$new_ip" \
  -e AuthServer__Authority=http://knowledgehub-api \
  -e AuthServer__RequireHttpsMetadata=false \
  -e Serilog__MinimumLevel__Default=Information \
  -v /home/ubuntu/docker/uploads:/app/uploads \
  --restart unless-stopped \
  mycompany/knowledgehub-api:latest

echo "等待 10 秒..."
sleep 10
docker ps --format "table {{.Names}}\t{{.Status}}" | grep api
EOF

    # 更新远程 Angular 配置
    echo ""
    echo "更新远程 Angular 配置..."
    remote_exec << EOF
cat > /tmp/dynamic-env.json << 'ENVEOF'
{
  "production": true,
  "application": {
    "baseUrl": "http://$new_ip",
    "name": "KnowledgeHub"
  },
  "oAuthConfig": {
    "issuer": "http://$new_ip:44354/",
    "redirectUri": "http://$new_ip",
    "clientId": "KnowledgeHub_App",
    "responseType": "code",
    "scope": "offline_access openid profile email phone KnowledgeHub"
  },
  "apis": {
    "default": {
      "url": "http://$new_ip:44354",
      "rootNamespace": "KnowledgeHub"
    },
    "AbpAccountPublic": {
      "url": "http://$new_ip:44354",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
ENVEOF

docker cp /tmp/dynamic-env.json knowledgehub-angular:/usr/share/nginx/html/dynamic-env.json
docker restart knowledgehub-angular
echo "Angular 配置已更新"
EOF

    echo ""
    echo "=== 测试 CORS ==="
    sleep 5
    remote_exec "curl -s -I -X OPTIONS http://localhost:44354/api/abp/application-configuration -H 'Origin: http://$new_ip' 2>/dev/null | grep -i 'access-control' || echo '无 CORS 头'"
    
    echo ""
    echo "更新完成!"
    echo "访问地址: http://$new_ip"
}

# ============================================================
# 主流程
# ============================================================
if [ "$SHOW_ONLY" = true ]; then
    show_config
else
    update_config "$NEW_IP"
fi
