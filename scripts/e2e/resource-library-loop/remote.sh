#!/usr/bin/env bash
# =============================================================================
# 教学资源库闭环 E2E · 远程执行器
# -----------------------------------------------------------------------------
# 把脚本目录 scp 到远程服务器并执行，本地保留输出。
#
# 用法：
#   ./remote.sh <host> [ssh-user]        # 例：./remote.sh 119.45.170.4 ubuntu
#
# 连接参数通过环境变量透传给远程（API_BASE / TENANT_NAME / 各账号密码等）：
#   API_BASE=http://119.45.170.4:44354 TENANT_NAME=qidi ./remote.sh 119.45.170.4 ubuntu
#
# 远程目录默认 ~/knowledgehub-e2e，可用 REMOTE_DIR 覆盖。
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

HOST="${1:-}"
SSH_USER="${2:-ubuntu}"
REMOTE_DIR="${REMOTE_DIR:-~/knowledgehub-e2e}"

if [ -z "$HOST" ]; then
  echo "用法: $0 <host> [ssh-user]" >&2
  exit 1
fi

# 透传的环境变量（远端 API 默认指本地地址，跑远程时务必覆盖 API_BASE）
PASS_VARS="API_BASE TENANT_NAME ADMIN_USER ADMIN_PASS TEACHER_USER TEACHER_PASS \
STUDENT_USER STUDENT_PASS REVIEWER_USER REVIEWER_PASS REVIEWER_TENANT \
OAUTH_CLIENT OAUTH_SCOPE LIB_NAME COURSE_TITLE RUN_TAG CURL_SSL_ARGS"

REMOTE_ENV=""
for v in $PASS_VARS; do
  val="${!v:-}"
  [ -n "$val" ] && REMOTE_ENV="${REMOTE_ENV} ${v}=$(printf '%q' "$val")"
done

echo "▶ 上传脚本到 ${SSH_USER}@${HOST}:${REMOTE_DIR}"
ssh "${SSH_USER}@${HOST}" "mkdir -p ${REMOTE_DIR}" >/dev/null
scp -r "$SCRIPT_DIR"/run.sh "$SCRIPT_DIR"/cleanup.sh "$SCRIPT_DIR"/preflight.sh \
      "$SCRIPT_DIR"/lib "${SSH_USER}@${HOST}:${REMOTE_DIR}/"

echo "▶ 远程执行：预检"
ssh "${SSH_USER}@${HOST}" "cd ${REMOTE_DIR} && env${REMOTE_ENV} bash preflight.sh"
PRE=$?

echo "▶ 远程执行：E2E"
ssh "${SSH_USER}@${HOST}" "cd ${REMOTE_DIR} && env${REMOTE_ENV} bash run.sh"
RC=$?

if [ $RC -eq 0 ]; then
  echo "▶ 远程 E2E 通过，拉取对象 Id 清单"
  scp "${SSH_USER}@${HOST}:${REMOTE_DIR}/.last-run-ids.json" \
      "$SCRIPT_DIR/.last-run-ids.remote.json" >/dev/null 2>&1 || true
  echo "✓ 远程 E2E 全部通过"
else
  echo "✗ 远程 E2E 失败 (exit=$RC)"
fi
exit $RC
