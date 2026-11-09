#!/usr/bin/env bash
# =============================================================================
# 教学资源库闭环 E2E · iketong 生产环境执行器
# -----------------------------------------------------------------------------
# 在远程服务器上跑同一套本地验证脚本（scripts/e2e/resource-library-loop/run.sh）。
#
# 用法：
#   ./run-iketong.sh              # 部署后跑一次（保留数据，供人工核对）
#   CLEANUP=1 ./run-iketong.sh    # 跑完顺便清理
#   RUN_TAG=xxx ./run-iketong.sh  # 固定命名后缀
#
# 依赖：本地已把镜像部署到 iketong（deploy-iketong.sh api/migrator/angular）。
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

REMOTE_HOST="${REMOTE_HOST:-47.99.245.49}"
REMOTE_USER="${REMOTE_USER:-root}"
REMOTE_DIR="${REMOTE_DIR:-~/knowledgehub-e2e}"

# 生产环境的连接参数（与本地不同：走 nginx 4200、OAuth scope 更长）
API_BASE="${API_BASE:-http://127.0.0.1:4200}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"
ADMIN_PASS="${ADMIN_PASS:-123456}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}"
STUDENT_PASS="${STUDENT_PASS:-1234567}"
REVIEWER_USER="${REVIEWER_USER:-admin}"
REVIEWER_PASS="${REVIEWER_PASS:-1q2w3E*}"
REVIEWER_TENANT="${REVIEWER_TENANT-}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access openid profile email phone KnowledgeHub}"
LIB_NAME="${LIB_NAME:-数据分析资源库}"
COURSE_TITLE="${COURSE_TITLE:-Python 数据分析}"
RUN_TAG="${RUN_TAG:-$(date +%m%d-%H%M%S)}"
CLEANUP="${CLEANUP:-0}"

ENV_STR="API_BASE='$API_BASE' TENANT_NAME='$TENANT_NAME' \
ADMIN_USER='$ADMIN_USER' ADMIN_PASS='$ADMIN_PASS' \
TEACHER_USER='$TEACHER_USER' TEACHER_PASS='$TEACHER_PASS' \
STUDENT_USER='$STUDENT_USER' STUDENT_PASS='$STUDENT_PASS' \
REVIEWER_USER='$REVIEWER_USER' REVIEWER_PASS='$REVIEWER_PASS' REVIEWER_TENANT='$REVIEWER_TENANT' \
OAUTH_SCOPE='$OAUTH_SCOPE' LIB_NAME='$LIB_NAME' COURSE_TITLE='$COURSE_TITLE' RUN_TAG='$RUN_TAG'"

echo "▶ 同步脚本到 ${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}"
ssh "${REMOTE_USER}@${REMOTE_HOST}" "mkdir -p ${REMOTE_DIR}"
scp -q -r "$SCRIPT_DIR"/run.sh "$SCRIPT_DIR"/preflight.sh "$SCRIPT_DIR"/cleanup.sh \
    "$SCRIPT_DIR"/lib "${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}/"

echo "▶ 远程预检"
ssh "${REMOTE_USER}@${REMOTE_HOST}" "cd ${REMOTE_DIR} && env ${ENV_STR} bash preflight.sh"

echo "▶ 远程执行闭环 E2E"
ssh "${REMOTE_USER}@${REMOTE_HOST}" "cd ${REMOTE_DIR} && env ${ENV_STR} bash run.sh"

echo "▶ 拉取对象 Id 清单"
scp -q "${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}/.last-run-ids.json" \
    "$SCRIPT_DIR/.last-run-ids.iketong.json" || true

if [ "$CLEANUP" = "1" ]; then
  echo "▶ 清理生产测试数据"
  ssh "${REMOTE_USER}@${REMOTE_HOST}" "cd ${REMOTE_DIR} && env ${ENV_STR} bash cleanup.sh"
fi

echo "✓ 完成。对象 Id: $SCRIPT_DIR/.last-run-ids.iketong.json"
