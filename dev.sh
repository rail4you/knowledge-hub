#!/bin/bash

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
PID_DIR="$PROJECT_ROOT/.dev"
LOG_DIR="$PROJECT_ROOT/.dev/logs"

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

start_api() {
    local pid_file="$PID_DIR/api.pid"
    if is_running "$pid_file"; then
        log_warn "API is already running (PID: $(cat $pid_file))"
        return
    fi
    
    log_info "Starting API (HttpApi.Host)..."
    cd "$PROJECT_ROOT"
    ASPNETCORE_ENVIRONMENT=Development dotnet watch --project src/KnowledgeHub.HttpApi.Host --no-hot-reload > "$LOG_DIR/api.log" 2>&1 &
    echo $! > "$pid_file"
    log_success "API started (PID: $(cat $pid_file))"
    log_info "API URL: https://localhost:44305"
    log_info "Swagger: https://localhost:44305/swagger"
}

start_angular() {
    local pid_file="$PID_DIR/angular.pid"
    if is_running "$pid_file"; then
        log_warn "Angular is already running (PID: $(cat $pid_file))"
        return
    fi
    
    log_info "Starting Angular..."
    cd "$PROJECT_ROOT/angular"
    npm run start -- --configuration=development > "$LOG_DIR/angular.log" 2>&1 &
    echo $! > "$pid_file"
    log_success "Angular started (PID: $(cat $pid_file))"
    log_info "Angular URL: http://localhost:4200"
}

start_meilisearch() {
    local pid_file="$PID_DIR/meilisearch.pid"
    if is_running "$pid_file"; then
        log_warn "Meilisearch is already running (PID: $(cat $pid_file))"
        return
    fi
    
    log_info "Starting Meilisearch..."
    cd "$PROJECT_ROOT"
    ./meilisearch --master-key="aSampleMasterKey" > "$LOG_DIR/meilisearch.log" 2>&1 &
    echo $! > "$pid_file"
    log_success "Meilisearch started (PID: $(cat $pid_file))"
    log_info "Meilisearch URL: http://localhost:7700"
}

start_ai() {
    local pid_file="$PID_DIR/ai.pid"
    if is_running "$pid_file"; then
        log_warn "AI API is already running (PID: $(cat $pid_file))"
        return
    fi
    
    log_info "Starting AI API..."
    cd "$PROJECT_ROOT"
    dotnet run --project src/KnowledgeHub.AI.Api > "$LOG_DIR/ai.log" 2>&1 &
    echo $! > "$pid_file"
    log_success "AI API started (PID: $(cat $pid_file))"
    log_info "AI API URL: http://localhost:5001"
}

stop_service() {
    local name="$1"
    local pid_file="$PID_DIR/${name}.pid"
    
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file")
        if kill -0 "$pid" 2>/dev/null; then
            log_info "Stopping $name (PID: $pid)..."
            kill "$pid" 2>/dev/null || true
            sleep 1
            if kill -0 "$pid" 2>/dev/null; then
                kill -9 "$pid" 2>/dev/null || true
            fi
            log_success "$name stopped"
        else
            log_warn "$name is not running"
        fi
        rm -f "$pid_file"
    else
        log_warn "$name is not running"
    fi
}

stop_all() {
    log_info "Stopping all services..."
    stop_service "api"
    stop_service "angular"
    stop_service "meilisearch"
    stop_service "ai"
    log_success "All services stopped"
}

show_status() {
    echo ""
    echo "================================"
    echo "  Development Services Status"
    echo "================================"
    echo ""
    
    printf "%-15s %-10s %-10s\n" "Service" "Status" "PID/Port"
    printf "%-15s %-10s %-10s\n" "-------" "------" "--------"
    
    if is_running "$PID_DIR/api.pid"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s\n" "API" "Running" "$(cat $PID_DIR/api.pid)"
        printf "%-15s %-10s %-10s\n" "" "" "https://localhost:44305"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s\n" "API" "Stopped" "-"
    fi
    
    if is_running "$PID_DIR/angular.pid"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s\n" "Angular" "Running" "$(cat $PID_DIR/angular.pid)"
        printf "%-15s %-10s %-10s\n" "" "" "http://localhost:4200"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s\n" "Angular" "Stopped" "-"
    fi
    
    if is_running "$PID_DIR/meilisearch.pid"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s\n" "Meilisearch" "Running" "$(cat $PID_DIR/meilisearch.pid)"
        printf "%-15s %-10s %-10s\n" "" "" "http://localhost:7700"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s\n" "Meilisearch" "Stopped" "-"
    fi
    
    if is_running "$PID_DIR/ai.pid"; then
        printf "%-15s ${GREEN}%-10s${NC} %-10s\n" "AI API" "Running" "$(cat $PID_DIR/ai.pid)"
        printf "%-15s %-10s %-10s\n" "" "" "http://localhost:5001"
    else
        printf "%-15s ${RED}%-10s${NC} %-10s\n" "AI API" "Stopped" "-"
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
            echo "Usage: $0 log [api|angular|meilisearch]"
            exit 1
            ;;
    esac
}

run_migrate() {
    log_info "Running database migration..."
    cd "$PROJECT_ROOT"
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/KnowledgeHub.DbMigrator --no-build 2>&1 || \
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/KnowledgeHub.DbMigrator 2>&1
    log_success "Migration completed"
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
            echo "Usage: $0 tail [api|angular|meilisearch|all]"
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
    echo "  start [api|angular|meilisearch|ai]  Start services (default: all)"
    echo "  stop [api|angular|meilisearch|ai]   Stop services (default: all)"
    echo "  restart [api|angular|meilisearch|ai] Restart services (default: all)"
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
            meilisearch|search)
                start_meilisearch
                ;;
            ai)
                start_ai
                ;;
            all)
                start_api
                start_angular
                start_meilisearch
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
                start_api
                start_angular
                start_meilisearch
                start_ai
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
                start_api
                start_angular
                start_meilisearch
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
            echo "Usage: $0 log [api|angular|meilisearch]"
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
