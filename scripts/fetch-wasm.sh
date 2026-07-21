#!/bin/bash
#
# 把外部 Unity WebGL 仿真实训资源下载到本地镜像目录。
#
# 用法：
#   bash scripts/fetch-wasm.sh <source-url> <slug> [title] [cover-url] [description]
#
# 示例：
#   bash scripts/fetch-wasm.sh http://124.222.92.99:8003/anatomyMice/ anatomy-mice "小鼠解剖" "https://cdn.example.com/c.png" "3D 交互式"
#
# 工作流：
#   1. 校验参数（slug 正则 / url http(s) / cover-url http(s)）
#   2. 下载 index.html → staging 目录
#   3. 从 index.html 中提取 loaderUrl，下载 *.loader.js 到 staging/Build/
#   4. 解析 loader.js 中的 dataUrl/frameworkUrl/codeUrl，下载 *.unityweb
#   5. 递归下载 StreamingAssets/ 与 TemplateData/
#   6. 校验所有 .unityweb 字节数与源一致（HEAD Content-Length）
#   7. 写 staging/mirror.json（含可选 title/cover/description）
#   8. 原子 mv staging/{slug}-{ts} → {slug}/ （已存在则先备份到 versions/）
#
# 失败：脚本任意步骤失败（set -e）将保留 staging 目录以便排查；运行成功后会自动清理。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MIRROR_ROOT="$PROJECT_ROOT/etc/docker/wasm-mirrors"
STAGING_ROOT="$MIRROR_ROOT/.staging"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()    { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error()   { echo -e "${RED}[ERROR]${NC} $1"; }

# ---------------------------------------------------------------------------
# 参数校验
# ---------------------------------------------------------------------------
if [ $# -lt 2 ] || [ $# -gt 5 ]; then
    log_error "用法: $0 <source-url> <slug> [title] [cover-url] [description]"
    echo "  示例: bash $0 http://124.222.92.99:8003/anatomyMice/ anatomy-mice \"小鼠解剖\" \"https://cdn.example.com/c.png\" \"3D 交互式\""
    exit 1
fi

SOURCE_URL="$1"
SLUG="$2"
TITLE="${3:-}"
COVER_URL="${4:-}"
DESCRIPTION="${5:-}"

# slug 正则：小写字母/数字开头，可含短横线
if ! echo "$SLUG" | grep -Eq '^[a-z0-9][a-z0-9-]{0,63}$'; then
    log_error "slug 必须满足 ^[a-z0-9][a-z0-9-]{0,63}\$ （小写字母/数字 + 短横线）"
    exit 1
fi

# url 必须是 http(s)
if ! echo "$SOURCE_URL" | grep -Eq '^https?://[^[:space:]]+$'; then
    log_error "source-url 必须是完整的 http(s) URL（不含空格）"
    exit 1
fi

# cover-url 必须是 http(s)（仅在提供了的情况下）
if [ -n "$COVER_URL" ] && ! echo "$COVER_URL" | grep -Eq '^https?://[^[:space:]]+$'; then
    log_error "cover-url 必须是完整的 http(s) URL（不含空格），不允许本地路径"
    exit 1
fi

# 规范化 BASE_URL：去掉末尾斜杠
BASE_URL="${SOURCE_URL%/}"

# ---------------------------------------------------------------------------
# 准备 staging 目录
# ---------------------------------------------------------------------------
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
STAGING_DIR="$STAGING_ROOT/${SLUG}-${TIMESTAMP}"

if [ -d "$STAGING_DIR" ]; then
    log_error "staging 目录已存在: $STAGING_DIR；请清理后重试"
    exit 1
fi

mkdir -p "$STAGING_DIR/Build" "$STAGING_DIR/StreamingAssets" "$STAGING_DIR/TemplateData"

cleanup() {
    if [ "${FETCH_DONE:-0}" = "1" ]; then
        rm -rf "$STAGING_DIR"
    else
        log_warn "staging 目录保留以便排查: $STAGING_DIR"
    fi
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# 工具函数
# ---------------------------------------------------------------------------

# 下载文件到指定本地路径。$1 = 远端 URL，$2 = 本地路径（绝对或相对 STAGING_DIR）
# 计算远端字节数与本地字节数。
download() {
    local url="$1"
    local dest="$2"

    if [ "${dest::1}" != "/" ]; then
        dest="$STAGING_DIR/$dest"
    fi

    mkdir -p "$(dirname "$dest")"
    log_info "下载 $url → ${dest#$STAGING_DIR/}"

    # --fail 失败立即退出；-L 跟随重定向；-s 静默；-o 输出
    if ! curl --fail -L --connect-timeout 15 --max-time 600 -o "$dest" "$url"; then
        log_error "下载失败: $url"
        return 1
    fi
}

# 与源站校验字节数。$1 = 远端 URL，$2 = 本地路径
verify_size() {
    local url="$1"
    local dest="$2"

    local expected
    expected="$(curl -sIL --connect-timeout 15 --max-time 30 "$url" \
        | grep -i '^content-length:' \
        | tail -n1 \
        | awk '{print $2}' \
        | tr -d '\r' || true)"
    if [ -z "$expected" ]; then
        log_warn "无法获取源端 Content-Length，跳过校验: $url"
        return 0
    fi

    local actual
    actual=$(wc -c < "$dest" | tr -d ' ')
    if [ "$expected" != "$actual" ]; then
        log_error "字节数不匹配: $url (预期 $expected, 实际 $actual)"
        return 1
    fi
    log_success "校验字节数 OK ($actual bytes): ${dest#$STAGING_DIR/}"
}

# 从远端相对路径拼接完整 URL
absolute_url() {
    local rel="$1"
    # 若 rel 已是 http(s) 绝对路径，原样返回
    if echo "$rel" | grep -Eq '^https?://'; then
        echo "$rel"
        return
    fi
    # 否则去掉前导 /，拼接 BASE_URL
    rel="${rel#/}"
    echo "$BASE_URL/$rel"
}

# 匹配字符串中的 src= "...loader.js" 字段，$1 = HTML，输出 loader 路径
extract_loader_path() {
    local html="$1"
    # 兼容单/双引号、相对/绝对路径
    grep -Eo 'src=["\x27][^"\x27]+loader\.js["\x27]' "$html" \
        | head -n1 \
        | sed -E 's/^src=["\x27]//; s/["\x27]$//' || true
}

# 从 loader.js 中提取 dataUrl/frameworkUrl/codeUrl 三元组
extract_loader_assets() {
    local loader_js="$1"
    grep -Eo '(dataUrl|frameworkUrl|codeUrl)\s*[:=]\s*["\x27][^"\x27]+["\x27]' "$loader_js" \
        | sed -E 's/["\x27]//g' || true
}

# ---------------------------------------------------------------------------
# 1) index.html
# ---------------------------------------------------------------------------
log_info "==> 1) 下载入口 index.html"
INDEX_URL="$BASE_URL/index.html"
download "$INDEX_URL" "$STAGING_DIR/index.html"

# 让浏览器复用磁盘缓存：去掉 Unity cacheBust 参数 (?v=5)
# cacheBust 把每个 .unityweb 变成"唯一 URL"，导致浏览器每次访问都重新下载 114MB。
# 我们保留 cacheBust 变量作为"版本切换开关"（运维同学改 cacheBust="v=6" 即可强制重新下载），
# 但默认设为空，让 URL 形式稳定为 Build/x.loader.js / .data.unityweb 等。
log_info "==> 1.1) 关闭 Unity cacheBust，避免 ?v=N 绕开浏览器磁盘缓存"
python3 - "$STAGING_DIR/index.html" << 'PYEOF'
import sys
p = sys.argv[1]
with open(p, encoding="utf-8") as f:
    s = f.read()
s = s.replace(
    'var cacheBust = "v=5";',
    '// cacheBust 留作版本切换开关；空值时 URL 不带 ?v=…，浏览器复用磁盘缓存。\n      var cacheBust = "";'
)
s = s.replace(
    '?" + cacheBust',
    '" + (cacheBust ? "?" + cacheBust : "")'
)
with open(p, "w", encoding="utf-8") as f:
    f.write(s)
print(f"  patched cacheBust in: {p}")
PYEOF

# ---------------------------------------------------------------------------
# 2) *.loader.js
# ---------------------------------------------------------------------------
log_info "==> 2) 解析并下载 *.loader.js"
LOADER_REL="$(extract_loader_path "$STAGING_DIR/index.html")"
if [ -z "$LOADER_REL" ]; then
    log_error "未能在 index.html 中找到 loader.js 引用，请确认源站是 Unity WebGL 模板"
    exit 1
fi
LOADER_URL="$(absolute_url "$LOADER_REL")"
LOADER_NAME="$(basename "$LOADER_REL")"
download "$LOADER_URL" "$STAGING_DIR/Build/$LOADER_NAME"

# 让 IndexedDB 也缓存 framework.js / .wasm：Unity 默认对非 dataUrl 资产用 no-store，
# 禁用 IndexedDB 缓存，每次访问都要重新下载 80KB + 5.6MB。
# 改成 must-revalidate 后，IndexedDB 缓存命中，浏览器磁盘缓存也生效（带 ETag 重协商）。
log_info "==> 2.1) 把 loader.js cacheControl 改为 must-revalidate（启用 IndexedDB 缓存）"
python3 - "$STAGING_DIR/Build/$LOADER_NAME" << 'PYEOF'
import sys
p = sys.argv[1]
with open(p, encoding="utf-8") as f:
    s = f.read()
old = 'cacheControl:function(e){return e==c.dataUrl?"must-revalidate":"no-store"}'
new = 'cacheControl:function(e){return "must-revalidate"}'
if old not in s:
    print(f"  WARN: cacheControl pattern not found in {p}（可能 Unity loader.js 模板已变，跳过）")
else:
    s = s.replace(old, new)
    with open(p, "w", encoding="utf-8") as f:
        f.write(s)
    print(f"  patched loader.js cacheControl: {p}")
PYEOF

# ---------------------------------------------------------------------------
# 3) *.data.unityweb / *.wasm.unityweb / *.framework.js.unityweb
# ---------------------------------------------------------------------------
log_info "==> 3) 解析并下载 loader.js 中的 .unityweb 资源"
extract_loader_assets "$STAGING_DIR/Build/$LOADER_NAME" | sort -u | while IFS= read -r line; do
    rel="$(echo "$line" | sed -E 's/.*[:=]\s*//')"
    if [ -z "$rel" ]; then
        continue
    fi
    url="$(absolute_url "$rel")"
    name="$(basename "$rel")"
    if [ "$name" = "$LOADER_NAME" ]; then
        # 与 loader.js 同名（罕见）跳过
        continue
    fi
    download "$url" "$STAGING_DIR/Build/$name"
    verify_size "$url" "$STAGING_DIR/Build/$name"
done

# ---------------------------------------------------------------------------
# 4) StreamingAssets/ 整目录递归
# ---------------------------------------------------------------------------
log_info "==> 4) 递归下载 StreamingAssets/"
SA_INDEX="$BASE_URL/StreamingAssets/"
SA_LOCAL="$STAGING_DIR/StreamingAssets"
mkdir -p "$SA_LOCAL"
# curl 不支持原生目录递归下载；用脚本扫描 HTML（轻量，常用做法）
# Unity StreamingAssets 通常仅放数据（json/png/jpg/wav 等），文件数适中。
fetch_dir_recursive() {
    local index_url="$1"
    local local_dir="$2"
    local rel_prefix="$3"  # 用于拼子路径

    local listing
    listing="$(curl --fail -L --connect-timeout 15 -s "$index_url")" || {
        log_warn "无法获取目录索引 $index_url（可能源站禁列目录或非标准）"
        return 0
    }

    # Apache/Nginx 默认列表中形如：<a href="foo.json">foo.json</a>
    echo "$listing" \
        | grep -Eo 'href="[^"]+"' \
        | sed -E 's/^href="//; s/"$//' \
        | grep -Ev '^\?|^/|^\.\./' \
        | sort -u \
        | while IFS= read -r entry; do
            # 跳过目录"../"或"?C=N;O=A"等
            case "$entry" in
                ../|\?*|'') continue ;;
            esac
            local entry_url="${index_url}${entry}"
            local entry_name="$entry"
            # 判断是目录还是文件：常见标志是末尾 /
            if [[ "$entry" == */ ]]; then
                mkdir -p "$local_dir/${entry%/}"
                fetch_dir_recursive "$entry_url" "$local_dir/${entry%/}" "$rel_prefix${entry}"
            else
                if [ ! -f "$local_dir/$entry_name" ]; then
                    download "$entry_url" "$local_dir/$entry_name" || true
                fi
            fi
        done
}

fetch_dir_recursive "$SA_INDEX" "$SA_LOCAL" "StreamingAssets/"

# ---------------------------------------------------------------------------
# 5) TemplateData/ 整目录递归
# ---------------------------------------------------------------------------
log_info "==> 5) 递归下载 TemplateData/"
TD_INDEX="$BASE_URL/TemplateData/"
TD_LOCAL="$STAGING_DIR/TemplateData"
fetch_dir_recursive "$TD_INDEX" "$TD_LOCAL" "TemplateData/"

# ---------------------------------------------------------------------------
# 6) 校验关键文件存在 + 至少一个大文件 > 1 MB
# ---------------------------------------------------------------------------
log_info "==> 6) 校验下载完整性"
if [ ! -f "$STAGING_DIR/index.html" ]; then
    log_error "index.html 缺失"
    exit 1
fi
if [ ! -f "$STAGING_DIR/Build/$LOADER_NAME" ]; then
    log_error "Build/$LOADER_NAME 缺失"
    exit 1
fi
# loader.js 配置项必须已下载
UNITYWEB_COUNT=$(find "$STAGING_DIR/Build" -maxdepth 1 -name "*.unityweb" | wc -l | tr -d ' ')
if [ "$UNITYWEB_COUNT" -lt 2 ]; then
    log_error "Build/ 下 .unityweb 资源数 ($UNITYWEB_COUNT) 过少，可能 loader.js 解析失败"
    exit 1
fi
# 至少一个 > 1 MB（Unity .data.unityweb 通常 100 MB+）
LARGE_FILE_FOUND=$(find "$STAGING_DIR/Build" -maxdepth 1 -name "*.unityweb" -size +1M | head -n1 || true)
if [ -z "$LARGE_FILE_FOUND" ]; then
    log_error "未找到 > 1 MB 的 .unityweb 资源，怀疑 loader.js 解析或下载失败"
    exit 1
fi

# 计算本地总字节数
TOTAL_BYTES=$(find "$STAGING_DIR" -type f -not -name "mirror.json" -exec wc -c {} + | tail -n1 | awk '{print $1}')
FILE_COUNT=$(find "$STAGING_DIR" -type f -not -name "mirror.json" | wc -l | tr -d ' ')

log_success "已下载 $FILE_COUNT 个文件，总计 $TOTAL_BYTES 字节"

# ---------------------------------------------------------------------------
# 7) 写 mirror.json
# ---------------------------------------------------------------------------
log_info "==> 7) 写 mirror.json"
MIRRORED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# 可选字段：仅在用户提供时写入。空值用空行占位，避免 heredoc 行号错乱。
TITLE_LINE=""
COVER_LINE=""
DESC_LINE=""
if [ -n "$TITLE" ]; then
    # 简单的 JSON 字符串转义：仅处理 \ 与 "
    TITLE_ESC="${TITLE//\\/\\\\}"
    TITLE_ESC="${TITLE_ESC//\"/\\\"}"
    TITLE_LINE="  \"title\": \"$TITLE_ESC\","
fi
if [ -n "$COVER_URL" ]; then
    COVER_LINE="  \"cover\": \"$COVER_URL\","
fi
if [ -n "$DESCRIPTION" ]; then
    DESC_ESC="${DESCRIPTION//\\/\\\\}"
    DESC_ESC="${DESC_ESC//\"/\\\"}"
    DESC_LINE="  \"description\": \"$DESC_ESC\","
fi

cat > "$STAGING_DIR/mirror.json" <<EOF
{
  "slug": "$SLUG",
  "sourceUrl": "${SOURCE_URL%/}",
  "entryPath": "index.html",
  "status": "ready",
  "mirroredAt": "$MIRRORED_AT",
  "coopCoepRequired": true,
$TITLE_LINE
$COVER_LINE
$DESC_LINE
  "totalBytes": $TOTAL_BYTES,
  "fileCount": $FILE_COUNT,
  "files": [
    "index.html",
    "Build/$LOADER_NAME"
$(find "$STAGING_DIR/Build" -maxdepth 1 -name "*.unityweb" -printf "    \"Build/%f\",\n" | sort -u || true)
$(find "$STAGING_DIR/StreamingAssets" -type f -printf "    \"StreamingAssets/%P\",\n" 2>/dev/null | sort -u || true)
$(find "$STAGING_DIR/TemplateData" -type f -printf "    \"TemplateData/%P\",\n" 2>/dev/null | sort -u || true)
    ""
  ]
}
EOF

# ---------------------------------------------------------------------------
# 8) 原子发布（已存在则备份到 versions/{ts}）
# ---------------------------------------------------------------------------
log_info "==> 8) 原子发布到 $SLUG/"
TARGET="$MIRROR_ROOT/$SLUG"
if [ -d "$TARGET" ]; then
    BACKUP="$MIRROR_ROOT/versions/${SLUG}-$TIMESTAMP"
    log_warn "已存在 $SLUG；备份到 versions/${SLUG}-$TIMESTAMP"
    mkdir -p "$MIRROR_ROOT/versions"
    mv "$TARGET" "$BACKUP"
fi

mv "$STAGING_DIR" "$TARGET"

FETCH_DONE=1
log_success "镜像就绪：$TARGET"
log_info "总览："
echo "  sourceUrl : ${SOURCE_URL%/}"
echo "  slug      : $SLUG"
echo "  title     : ${TITLE:-<未设置>}"
echo "  cover     : ${COVER_URL:-<未设置>}"
echo "  description: ${DESCRIPTION:-<未设置>}"
echo "  size      : $TOTAL_BYTES bytes ($FILE_COUNT files)"
echo "  mirror.json: $TARGET/mirror.json"
log_info "下一步："
echo "  ./dev.sh restart api                       # 让 API 扫描新镜像"
echo "  访问 /wasm/$SLUG/index.html  验证"
