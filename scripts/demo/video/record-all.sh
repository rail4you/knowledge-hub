#!/usr/bin/env bash
# 依次录制全部演示场景（生产环境真实操作）
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p out/logs
for s in "$@"; do
  echo "════════ $(date +%H:%M:%S) 录制 $s"
  node "scenes/$s.mjs" > "out/logs/$s.log" 2>&1
  code=$?
  tail -3 "out/logs/$s.log"
  [ $code -ne 0 ] && echo "✗ $s 失败（exit $code）"
done
echo "完成。"
ls -la out/raw/
