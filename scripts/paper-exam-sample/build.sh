#!/usr/bin/env bash
# 生成「网络安全技术」纸质试卷样张：HTML → PDF → PNG（供 OCR 解析测试）
#
#   bash scripts/paper-exam-sample/build.sh
#
# 依赖：python3、Chrome（/Applications/Google Chrome.app）、sips（macOS 自带）
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
HTML="$DIR/network-security-exam.html"
PDF="$DIR/network-security-exam.pdf"
PNG="$DIR/network-security-exam.png"
JPEG="$DIR/network-security-exam.jpg"

cd "$DIR"

echo "[1/3] 生成 HTML …"
python3 gen_paper_exam.py

echo "[2/3] 打印 PDF …"
if [ ! -x "$CHROME" ]; then
  echo "未找到 Chrome：$CHROME" >&2
  exit 1
fi
"$CHROME" --headless --disable-gpu --no-sandbox \
  --print-to-pdf="$PDF" --no-pdf-header-footer \
  "file://$HTML" >/dev/null 2>&1
[ -s "$PDF" ] || { echo "PDF 生成失败" >&2; exit 1; }

echo "[3/3] PDF → PNG …"
# sips 无法读取 PDF 时，用 Chrome 直接截图 HTML（A4 比例 200dpi）
if ! sips -s format png "$PDF" --out "$PNG" >/dev/null 2>&1 || [ ! -s "$PNG" ]; then
  echo "      sips 栅格化失败，改用 Chrome 截图 …"
  "$CHROME" --headless --disable-gpu --no-sandbox --hide-scrollbars \
    --window-size=1654,2339 --force-device-scale-factor=1 \
    --screenshot="$PNG" "file://$HTML" >/dev/null 2>&1
fi
[ -s "$PNG" ] || { echo "PNG 生成失败" >&2; exit 1; }

# 压成 JPEG（上传体积更小，OCR 效果一致）
sips -s format jpeg -s formatOptions 82 "$PNG" --out "$JPEG" >/dev/null 2>&1 || true

ls -lh "$PDF" "$PNG" ${JPEG:+"$JPEG"} 2>/dev/null || true
echo "完成：$PDF / $PNG"
