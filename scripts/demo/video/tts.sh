#!/usr/bin/env bash
# =============================================================================
# 演示视频 · 语音合成（中文旁白）
# -----------------------------------------------------------------------------
# 用 MiniMax TTS 为每个场景生成旁白 mp3 + 精确时间轴 srt（字幕直接来自 TTS）。
#
# 用法：
#   ./tts.sh                # 全部场景
#   ./tts.sh s02-resource-create   # 指定场景（可多个，空格分隔）
#
# 依赖：mmx（MiniMax CLI），已登录 ~/.mmx/credentials.json
# 产物：out/tts/<scene>.mp3 / out/tts/<scene>.srt
# =============================================================================
set -uo pipefail
cd "$(dirname "$0")"

VOICE_META=voiceover.json
VOICE="$(jq -r '.meta.voice' "$VOICE_META")"
MODEL="$(jq -r '.meta.model' "$VOICE_META")"
SPEED="$(jq -r '.meta.speed' "$VOICE_META")"
OUT_DIR="out/tts"

mkdir -p "$OUT_DIR"

scenes=("$@")
if [ ${#scenes[@]} -eq 0 ]; then
  mapfile -t scenes < <(jq -r '.scenes[].id' "$VOICE_META")
fi

for id in "${scenes[@]}"; do
  text="$(jq -r --arg id "$id" '.scenes[] | select(.id==$id) | .voice' "$VOICE_META")"
  if [ -z "$text" ] || [ "$text" = "null" ]; then
    echo "✗ 找不到场景 $id"; continue
  fi
  if [ -f "$OUT_DIR/$id.mp3" ] && [ -f "$OUT_DIR/$id.srt" ]; then
    echo "· 跳过（已存在）$id"; continue
  fi
  printf '%s\n' "$text" > "$OUT_DIR/$id.txt"
  echo "▶ 合成 $id ..."
  if mmx speech synthesize \
      --text-file "$OUT_DIR/$id.txt" \
      --model "$MODEL" \
      --voice "$VOICE" \
      --speed "$SPEED" \
      --subtitles \
      --out "$OUT_DIR/$id.mp3" \
      --non-interactive --quiet >/dev/null; then
    [ -f "$OUT_DIR/$id.srt" ] && mv "$OUT_DIR/$id.srt" "$OUT_DIR/$id.srt" 2>/dev/null
    dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT_DIR/$id.mp3")
    echo "  ✓ $id.mp3  ${dur}s  $(wc -m < "$OUT_DIR/$id.txt" | tr -d ' ') 字"
  else
    echo "  ✗ 合成失败：$id"
  fi
done

echo "完成。产物目录：$OUT_DIR"
