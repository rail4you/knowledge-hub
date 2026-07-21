#!/bin/bash
#
# 把本地 wasm 镜像目录打包成 zip，供教师端上传到 PracticumSimulation。
# 产物不入 git；用户自行上传到管理员界面 → /wasm/{slug}/ 目录由后端解压填充。
#
# 用法：
#   bash scripts/pack-wasm.sh <src-dir> <out.zip>
#
# 示例：
#   bash scripts/pack-wasm.sh etc/docker/wasm-mirrors/anatomy-mice anatomy-mice.zip
#
# 行为：
#   1. 校验 src-dir 存在且包含 index.html
#   2. 输出 zip 路径若已存在 → 失败退出
#   3. 递归打包，排除 manifest 类元信息（mirror.json/.gitkeep 等）
#   4. 报告大小 + 文件数
#
# 上传流程（教师端）：
#   1. 实训管理 → 编辑某个 Project → 「仿真镜像」Tab
#   2. 点击「上传」，选择本脚本生成的 zip
#   3. 名字/描述/封面 填好后保存；后端校验 .unityweb 存在则状态 = Ready
#   4. 学生侧「实训详情 → 仿真实训 Tab」即可加载

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()    { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error()   { echo -e "${RED}[ERROR]${NC} $1"; }

if [ $# -ne 2 ]; then
    log_error "Usage: $0 <src-dir> <out.zip>"
    echo "  Example: bash $0 etc/docker/wasm-mirrors/anatomy-mice anatomy-mice.zip"
    exit 1
fi

SRC_DIR="$1"
OUT_ZIP="$2"

# 若 src-dir 是相对路径，按 PROJECT_ROOT 解析
case "$SRC_DIR" in
    /*) ;;
    *)  SRC_DIR="$PROJECT_ROOT/$SRC_DIR" ;;
esac
case "$OUT_ZIP" in
    /*) ;;
    *)  OUT_ZIP="$PROJECT_ROOT/$OUT_ZIP" ;;
esac

if [ ! -d "$SRC_DIR" ]; then
    log_error "Source directory not found: $SRC_DIR"
    exit 1
fi

if [ ! -f "$SRC_DIR/index.html" ]; then
    log_error "Source directory missing index.html: $SRC_DIR"
    echo "  Make sure this is a Unity WebGL build directory (index.html + Build/ + StreamingAssets/ + TemplateData/)"
    exit 1
fi

if [ -e "$OUT_ZIP" ]; then
    log_error "Output zip already exists: $OUT_ZIP"
    echo "  Delete it first or pick a new path"
    exit 1
fi

log_info "Packing $SRC_DIR -> $OUT_ZIP"

# 在 SRC_DIR 父目录下执行 zip，让压缩包内的根目录直接是 SRC_DIR 的 basename（不带父路径）
SRC_BASENAME="$(basename "$SRC_DIR")"
SRC_PARENT="$(dirname "$SRC_DIR")"

(
    cd "$SRC_PARENT"

    # 排除 manifest 类文件（mirror.json 旧机制残留），其它照单全收
    zip -r "$OUT_ZIP" "$SRC_BASENAME" \
        -x "${SRC_BASENAME}/mirror.json" \
          "${SRC_BASENAME}/.gitkeep" \
          "${SRC_BASENAME}/.DS_Store" \
          "${SRC_BASENAME}/.staging/*" \
        > /tmp/pack-wasm.log 2>&1
) || {
    log_error "zip packing failed, see /tmp/pack-wasm.log"
    cat /tmp/pack-wasm.log
    exit 1
}

# 统计 zip 内容
if command -v unzip >/dev/null 2>&1; then
    FILE_COUNT=$(unzip -l "$OUT_ZIP" | tail -n1 | awk '{print $2}')
else
    FILE_COUNT=$(zipinfo -1 "$OUT_ZIP" 2>/dev/null | wc -l | tr -d ' ')
fi
TOTAL_BYTES=$(wc -c < "$OUT_ZIP" | tr -d ' ')

log_success "Created $OUT_ZIP ($FILE_COUNT files, $TOTAL_BYTES bytes)"

# 检查 zip 里是否包含 .unityweb/.wasm（没有则后端会把 Status 标为 Invalid）
if command -v unzip >/dev/null 2>&1; then
    HAS_WASM=$(unzip -l "$OUT_ZIP" | grep -Ei '\.(unityweb|wasm)$' | head -n1 || true)
    if [ -z "$HAS_WASM" ]; then
        log_warn "zip missing .unityweb/.wasm - backend will mark Status=Invalid"
    else
        log_success "detected WASM asset: $(basename "$HAS_WASM")"
    fi
fi

log_info "Next steps:"
echo "  1. Login as teacher/admin -> Practicum Management -> select a Project -> Simulations tab"
echo "  2. Click Upload, pick $OUT_ZIP"
echo "  3. Fill in name/description/cover URL, save"
echo "  4. Verify via: curl -sk https://localhost:44305/api/app/practicum-simulation/list-by-project/{projectId}"
