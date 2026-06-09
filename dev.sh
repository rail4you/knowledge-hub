#!/bin/bash

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
PID_DIR="$PROJECT_ROOT/.dev"
LOG_DIR="$PROJECT_ROOT/.dev/logs"
DOTNET_RUN_FLAGS="${DOTNET_RUN_FLAGS:---no-restore}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

ensure_dirs() {
    mkdir -p "$PID_DIR" "$LOG_DIR"
}

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

require_command() {
    local command_name="$1"
    if ! command -v "$command_name" >/dev/null 2>&1; then
        log_error "Required command not found: $command_name"
        exit 1
    fi
}

is_running() {
    local pid_file="$1"
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file")
        if kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
    fi
    return 1
}

is_tmux_running() {
    local name="$1"
    local session
    session=$(tmux_session_name "$name")
    command -v tmux >/dev/null 2>&1 && tmux has-session -t "$session" >/dev/null 2>&1
}

is_service_running() {
    local name="$1"
    local pid_file="$PID_DIR/${name}.pid"
    is_running "$pid_file" || is_tmux_running "$name"
}

service_pid() {
    local name="$1"
    local pid_file="$PID_DIR/${name}.pid"
    if [ -f "$pid_file" ]; then
        cat "$pid_file"
    else
        echo "-"
    fi
}

service_ports() {
    case "$1" in
        api) echo "44305" ;;
        angular) echo "4200" ;;
        react) echo "3000" ;;
        meilisearch) echo "7700" ;;
        ai) echo "5001" ;;
    esac
}

kill_service_ports() {
    local name="$1"
    local port
    local pids

    for port in $(service_ports "$name"); do
        pids=$(lsof -ti:"$port" 2>/dev/null || true)
        if [ -n "$pids" ]; then
            log_warn "Cleaning $name port $port"
            kill -9 $pids 2>/dev/null || true
        fi
    done
}

tmux_session_name() {
    echo "knowledgehub-$1"
}

start_detached() {
    local name="$1"
    local pid_file="$2"
    local command="$3"
    local session
    session=$(tmux_session_name "$name")

    if command -v tmux >/dev/null 2>&1; then
        tmux kill-session -t "$session" >/dev/null 2>&1 || true
        tmux new-session -d -s "$session" "$command"
        sleep 0.2
        tmux list-panes -t "$session" -F '#{pane_pid}' | head -n 1 > "$pid_file"
    else
        nohup /bin/bash -lc "$command" >/dev/null 2>&1 &
        echo $! > "$pid_file"
    fi
}

# Wait for a HTTP endpoint to return 200, with timeout
# Usage: wait_for_http <url> <description> [timeout_seconds]
wait_for_http() {
    local url="$1"
    local timeout="${3:-60}"
    local elapsed=0

    while [ $elapsed -lt $timeout ]; do
        if curl --max-time 2 -sf -o /dev/null "$url" 2>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    return 1
}

# Wait for a HTTPS endpoint to return 200 (skip cert verification)
# Usage: wait_for_https <url> <description> [timeout_seconds]
wait_for_https() {
    local url="$1"
    local timeout="${3:-60}"
    local elapsed=0

    while [ $elapsed -lt $timeout ]; do
        if curl --max-time 2 -skf -o /dev/null "$url" 2>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    return 1
}

# Wait for a TCP port to be free
# Usage: wait_for_port_free <port> [timeout_seconds]
wait_for_port_free() {
    local port="$1"
    local timeout="${2:-10}"
    local elapsed=0

    while [ $elapsed -lt $timeout ]; do
        if ! lsof -ti:"$port" >/dev/null 2>&1; then
            return 0
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done
    return 1
}

wait_for_tcp_listen() {
    local port="$1"
    local timeout="${2:-30}"
    local elapsed=0

    while [ $elapsed -lt $timeout ]; do
        if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
            return 0
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done
    return 1
}

start_api() {
    local pid_file="$PID_DIR/api.pid"
    require_command curl
    require_command dotnet
    require_command lsof

    if is_service_running "api"; then
        log_warn "API is already running (PID: $(service_pid api))"
        return
    fi

    # Ensure port 44305 is free
    if ! wait_for_port_free 44305; then
        log_warn "Port 44305 still occupied, force cleaning..."
        lsof -ti:44305 | xargs kill -9 2>/dev/null || true
        sleep 1
    fi

    log_info "Starting API (HttpApi.Host)..."
    start_detached "api" "$pid_file" "cd \"$PROJECT_ROOT\" && exec env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_Kestrel__Certificates__Default__Path=\"$HOME/.aspnet/https/aspnetapp.pfx\" ASPNETCORE_Kestrel__Certificates__Default__Password=\"devcert\" dotnet run --project src/KnowledgeHub.HttpApi.Host $DOTNET_RUN_FLAGS > \"$LOG_DIR/api.log\" 2>&1"

    if wait_for_https "https://localhost:44305/health-status" "API" 90; then
        log_success "API started (PID: $(cat $pid_file))"
        log_info "API URL: https://localhost:44305"
        log_info "Swagger: https://localhost:44305/swagger"
    else
        log_error "API failed to start. Check logs: $0 log api"
        stop_service "api"
        exit 1
    fi
}

start_angular() {
    local pid_file="$PID_DIR/angular.pid"
    require_command curl
    require_command npm
    require_command lsof

    if is_service_running "angular"; then
        log_warn "Angular is already running (PID: $(service_pid angular))"
        return
    fi

    # Ensure port 4200 is free
    if ! wait_for_port_free 4200; then
        log_warn "Port 4200 still occupied, force cleaning..."
        lsof -ti:4200 | xargs kill -9 2>/dev/null || true
        sleep 1
    fi

    # Clean Angular build cache to avoid stale compilation issues
    local cache_dir="$PROJECT_ROOT/angular/.angular/cache"
    if [ -d "$cache_dir" ]; then
        rm -rf "$cache_dir"
        log_info "Cleared Angular build cache"
    fi

    log_info "Starting Angular..."
    start_detached "angular" "$pid_file" "cd \"$PROJECT_ROOT/angular\" && exec npm run start -- --configuration=development > \"$LOG_DIR/angular.log\" 2>&1"

    if wait_for_http "http://localhost:4200" "Angular" 60; then
        log_success "Angular started (PID: $(cat $pid_file))"
        log_info "Angular URL: http://localhost:4200"
    else
        log_error "Angular failed to start. Check logs: $0 log angular"
        stop_service "angular"
        exit 1
    fi
}

start_react() {
    local pid_file="$PID_DIR/react.pid"
    require_command curl
    require_command npm
    require_command lsof

    if is_service_running "react"; then
        log_warn "React is already running (PID: $(service_pid react))"
        return
    fi

    if ! wait_for_port_free 3000; then
        log_warn "Port 3000 still occupied, force cleaning..."
        lsof -ti:3000 | xargs kill -9 2>/dev/null || true
        sleep 1
    fi

    log_info "Starting React student portal..."
    start_detached "react" "$pid_file" "cd \"$PROJECT_ROOT/student-react\" && exec npm run dev > \"$LOG_DIR/react.log\" 2>&1"

    if wait_for_http "http://localhost:3000" "React student portal" 60; then
        log_success "React student portal started (PID: $(cat $pid_file))"
        log_info "React URL: http://localhost:3000"
    else
        log_error "React student portal failed to start. Check logs: $0 log react"
        stop_service "react"
        exit 1
    fi
}

start_meilisearch() {
    local pid_file="$PID_DIR/meilisearch.pid"
    require_command curl
    require_command lsof

    if [ ! -x "$PROJECT_ROOT/meilisearch" ]; then
        log_error "Meilisearch binary is missing or not executable: $PROJECT_ROOT/meilisearch"
        exit 1
    fi

    if is_service_running "meilisearch"; then
        log_warn "Meilisearch is already running (PID: $(service_pid meilisearch))"
        return
    fi

    # Ensure port 7700 is free
    if ! wait_for_port_free 7700; then
        log_warn "Port 7700 still occupied, force cleaning..."
        lsof -ti:7700 | xargs kill -9 2>/dev/null || true
        sleep 1
    fi

    log_info "Starting Meilisearch..."
    start_detached "meilisearch" "$pid_file" "cd \"$PROJECT_ROOT\" && exec ./meilisearch --master-key=\"aSampleMasterKey\" > \"$LOG_DIR/meilisearch.log\" 2>&1"

    if wait_for_http "http://localhost:7700/health" "Meilisearch" 30; then
        log_success "Meilisearch started (PID: $(cat $pid_file))"
        log_info "Meilisearch URL: http://localhost:7700"
    else
        log_error "Meilisearch failed to start. Check logs: $0 log meilisearch"
        stop_service "meilisearch"
        exit 1
    fi
}

start_ai() {
    local pid_file="$PID_DIR/ai.pid"
    require_command dotnet
    require_command lsof

    if is_service_running "ai"; then
        log_warn "AI API is already running (PID: $(service_pid ai))"
        return
    fi
    
    log_info "Starting AI API..."
    start_detached "ai" "$pid_file" "cd \"$PROJECT_ROOT\" && exec dotnet run --project src/KnowledgeHub.AI.Api $DOTNET_RUN_FLAGS > \"$LOG_DIR/ai.log\" 2>&1"

    if wait_for_tcp_listen 5001 45; then
        log_success "AI API started (PID: $(cat $pid_file))"
        log_info "AI API URL: http://localhost:5001"
    else
        log_error "AI API failed to start. Check logs: $0 log ai"
        stop_service "ai"
        exit 1
    fi
}

stop_service() {
    local name="$1"
    local pid_file="$PID_DIR/${name}.pid"
    local session
    session=$(tmux_session_name "$name")
    
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file")
        if kill -0 "$pid" 2>/dev/null; then
            log_info "Stopping $name (PID: $pid)..."
            if command -v tmux >/dev/null 2>&1; then
                tmux kill-session -t "$session" >/dev/null 2>&1 || true
            fi
            kill "$pid" 2>/dev/null || true
            sleep 1
            if kill -0 "$pid" 2>/dev/null; then
                kill -9 "$pid" 2>/dev/null || true
            fi
            kill_service_ports "$name"
            log_success "$name stopped"
        else
            log_warn "$name is not running"
            if command -v tmux >/dev/null 2>&1; then
                tmux kill-session -t "$session" >/dev/null 2>&1 || true
            fi
            kill_service_ports "$name"
        fi
        rm -f "$pid_file"
    else
        log_warn "$name is not running"
        if command -v tmux >/dev/null 2>&1; then
            tmux kill-session -t "$session" >/dev/null 2>&1 || true
        fi
        kill_service_ports "$name"
    fi
}

stop_all() {
    log_info "Stopping all services..."
    stop_service "api"
    stop_service "react"
    stop_service "angular"
    stop_service "meilisearch"
    stop_service "ai"
    log_success "All services stopped"
}

show_status() {
    local api_health="Down"
    local angular_health="Down"
    local react_health="Down"
    local meilisearch_health="Down"
    local ai_health="Down"

    if command -v curl >/dev/null 2>&1 && curl --max-time 2 -skf -o /dev/null "https://localhost:44305/health-status" 2>/dev/null; then
        api_health="Healthy"
    fi
    if command -v curl >/dev/null 2>&1 && curl --max-time 2 -sf -o /dev/null "http://localhost:4200" 2>/dev/null; then
        angular_health="Healthy"
    fi
    if command -v curl >/dev/null 2>&1 && curl --max-time 2 -sf -o /dev/null "http://localhost:3000" 2>/dev/null; then
        react_health="Healthy"
    fi
    if command -v curl >/dev/null 2>&1 && curl --max-time 2 -sf -o /dev/null "http://localhost:7700/health" 2>/dev/null; then
        meilisearch_health="Healthy"
    fi
    if command -v lsof >/dev/null 2>&1 && lsof -nP -iTCP:5001 -sTCP:LISTEN >/dev/null 2>&1; then
        ai_health="Listening"
    fi

    echo ""
    echo "================================"
    echo "  Development Services Status"
    echo "================================"
    echo ""
    
    printf "%-15s %-10s %-10s %-10s\n" "Service" "Status" "Health" "PID/Port"
    printf "%-15s %-10s %-10s %-10s\n" "-------" "------" "------" "--------"
    
    if is_service_running "api"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s %-10s\n" "API" "Running" "$api_health" "$(service_pid api)"
        printf "%-15s %-10s %-10s %-10s\n" "" "" "" "https://localhost:44305"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s %-10s\n" "API" "Stopped" "$api_health" "-"
    fi
    
    if is_service_running "angular"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s %-10s\n" "Angular" "Running" "$angular_health" "$(service_pid angular)"
        printf "%-15s %-10s %-10s %-10s\n" "" "" "" "http://localhost:4200"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s %-10s\n" "Angular" "Stopped" "$angular_health" "-"
    fi

    if is_service_running "react"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s %-10s\n" "React" "Running" "$react_health" "$(service_pid react)"
        printf "%-15s %-10s %-10s %-10s\n" "" "" "" "http://localhost:3000"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s %-10s\n" "React" "Stopped" "$react_health" "-"
    fi
    
    if is_service_running "meilisearch"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s %-10s\n" "Meilisearch" "Running" "$meilisearch_health" "$(service_pid meilisearch)"
        printf "%-15s %-10s %-10s %-10s\n" "" "" "" "http://localhost:7700"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s %-10s\n" "Meilisearch" "Stopped" "$meilisearch_health" "-"
    fi
    
    if is_service_running "ai"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s %-10s\n" "AI API" "Running" "$ai_health" "$(service_pid ai)"
        printf "%-15s %-10s %-10s %-10s\n" "" "" "" "http://localhost:5001"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s %-10s\n" "AI API" "Stopped" "$ai_health" "-"
    fi
    
    echo ""
    
    printf "%-15s %-10s\n" "Database" "Port"
    printf "%-15s %-10s\n" "--------" "----"
    printf "%-15s %-10s\n" "PostgreSQL" "localhost:5433"
    echo ""
}

show_log() {
    local service="$1"
    
    case "$service" in
        api|backend)
            local log_file="$LOG_DIR/api.log"
            if [ -f "$log_file" ]; then
                tail -100 "$log_file"
            else
                log_error "No log file found. Is API running?"
            fi
            ;;
        angular|frontend|ng)
            local log_file="$LOG_DIR/angular.log"
            if [ -f "$log_file" ]; then
                tail -100 "$log_file"
            else
                log_error "No log file found. Is Angular running?"
            fi
            ;;
        react|student)
            local log_file="$LOG_DIR/react.log"
            if [ -f "$log_file" ]; then
                tail -100 "$log_file"
            else
                log_error "No log file found. Is React running?"
            fi
            ;;
        meilisearch|search)
            local log_file="$LOG_DIR/meilisearch.log"
            if [ -f "$log_file" ]; then
                tail -100 "$log_file"
            else
                log_error "No log file found. Is Meilisearch running?"
            fi
            ;;
        ai)
            local log_file="$LOG_DIR/ai.log"
            if [ -f "$log_file" ]; then
                tail -100 "$log_file"
            else
                log_error "No log file found. Is AI API running?"
            fi
            ;;
        *)
            log_error "Unknown service: $service"
            echo "Usage: $0 log [api|angular|react|meilisearch]"
            exit 1
            ;;
    esac
}

run_migrate() {
    log_info "Running database migration..."
    cd "$PROJECT_ROOT"

    # 强制 rebuild DbMigrator（含 EF Migrations / Application / Domain 依赖），
    # 避免 "dotnet run --no-build" 拿过期 dll 跑出"假成功"：
    # 脚本会照常输出 "Successfully completed"，但新 migration 实际没生效。
    log_info "Building DbMigrator..."
    if ! dotnet build src/KnowledgeHub.DbMigrator/KnowledgeHub.DbMigrator.csproj --nologo 2>&1 | tail -15; then
        log_error "DbMigrator build failed. 请检查上方编译错误后再重试。"
        return 1
    fi

    # build 完后用 --no-build 跑（避免 dotnet run 重复 build）。
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/KnowledgeHub.DbMigrator --no-build 2>&1
    local status=$?

    if [ $status -eq 0 ]; then
        log_success "Migration completed"
    else
        log_error "Migration failed (exit code: $status)"
    fi
    return $status
}

tail_logs() {
    local service="$1"
    
    case "$service" in
        api|backend)
            local log_file="$LOG_DIR/api.log"
            if [ -f "$log_file" ]; then
                tail -f "$log_file"
            else
                log_error "No log file found. Start API first."
            fi
            ;;
        angular|frontend|ng)
            local log_file="$LOG_DIR/angular.log"
            if [ -f "$log_file" ]; then
                tail -f "$log_file"
            else
                log_error "No log file found. Start Angular first."
            fi
            ;;
        react|student)
            local log_file="$LOG_DIR/react.log"
            if [ -f "$log_file" ]; then
                tail -f "$log_file"
            else
                log_error "No log file found. Start React first."
            fi
            ;;
        meilisearch|search)
            local log_file="$LOG_DIR/meilisearch.log"
            if [ -f "$log_file" ]; then
                tail -f "$log_file"
            else
                log_error "No log file found. Start Meilisearch first."
            fi
            ;;
        ai)
            local log_file="$LOG_DIR/ai.log"
            if [ -f "$log_file" ]; then
                tail -f "$log_file"
            else
                log_error "No log file found. Start AI API first."
            fi
            ;;
        all|"")
            log_info "Tailing all logs (Ctrl+C to stop)..."
            tail -f "$LOG_DIR"/*.log 2>/dev/null || log_error "No log files found"
            ;;
        *)
            log_error "Unknown service: $service"
            echo "Usage: $0 tail [api|angular|react|meilisearch|all]"
            exit 1
            ;;
    esac
}

show_help() {
    echo ""
    echo "KnowledgeHub Development Script"
    echo ""
    echo "Usage: $0 <command> [options]"
    echo ""
    echo "Commands:"
    echo "  start [api|angular|react|meilisearch|ai]  Start services (default: all)"
    echo "  stop [api|angular|react|meilisearch|ai]   Stop services (default: all)"
    echo "  restart [api|angular|react|meilisearch|ai] Restart services (default: all)"
    echo "  status                           Show service status"
    echo "  log <service>                    Show last 100 lines of log"
    echo "  tail [service]                   Tail logs in real-time"
    echo "  migrate                          Run database migration"
    echo "  help                             Show this help"
    echo ""
    echo "Examples:"
    echo "  $0 start               Start all services"
    echo "  $0 start api           Start only API"
    echo "  $0 start meilisearch   Start only Meilisearch"
    echo "  $0 stop                Stop all services"
    echo "  $0 log api             Show API logs"
    echo "  $0 tail angular        Tail Angular logs"
    echo "  $0 migrate             Run database migration"
    echo ""
    echo "Services:"
    echo "  API:         https://localhost:44305 (Swagger: /swagger)"
    echo "  React:       http://localhost:3000"
    echo "  Angular:     http://localhost:4200"
    echo "  Meilisearch: http://localhost:7700"
    echo "  AI API:      http://localhost:5001"
    echo "  DB:          localhost:5433 (PostgreSQL)"
    echo ""
}

case "${1:-help}" in
    start)
        ensure_dirs
        case "${2:-all}" in
            api|backend)
                start_api
                ;;
            angular|frontend|ng)
                start_angular
                ;;
            react|student)
                start_react
                ;;
            meilisearch|search)
                start_meilisearch
                ;;
            ai)
                start_ai
                ;;
            all)
                start_meilisearch
                start_api
                start_react
                start_angular
                start_ai
                ;;
            *)
                log_error "Unknown service: $2"
                show_help
                exit 1
                ;;
        esac
        ;;
    stop)
        case "${2:-all}" in
            api|backend)
                stop_service "api"
                ;;
            angular|frontend|ng)
                stop_service "angular"
                ;;
            react|student)
                stop_service "react"
                ;;
            meilisearch|search)
                stop_service "meilisearch"
                ;;
            ai)
                stop_service "ai"
                ;;
            all)
                stop_all
                ;;
            *)
                log_error "Unknown service: $2"
                show_help
                exit 1
                ;;
        esac
        ;;
    restart)
        case "${2:-all}" in
            api|backend)
                stop_service "api"
                ensure_dirs
                start_api
                ;;
            angular|frontend|ng)
                stop_service "angular"
                ensure_dirs
                start_angular
                ;;
            react|student)
                stop_service "react"
                ensure_dirs
                start_react
                ;;
            meilisearch|search)
                stop_service "meilisearch"
                ensure_dirs
                start_meilisearch
                ;;
            ai)
                stop_service "ai"
                ensure_dirs
                start_ai
                ;;
            all)
                stop_all
                ensure_dirs
                start_meilisearch
                start_api
                start_react
                start_angular
                start_ai
                ;;
            *)
                log_error "Unknown service: $2"
                show_help
                exit 1
                ;;
        esac
        ;;
    status|ps)
        show_status
        ;;
    log|logs)
        if [ -z "$2" ]; then
            log_error "Please specify a service"
            echo "Usage: $0 log [api|angular|react|meilisearch]"
            exit 1
        fi
        show_log "$2"
        ;;
    tail)
        tail_logs "${2:-all}"
        ;;
    migrate)
        run_migrate
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
