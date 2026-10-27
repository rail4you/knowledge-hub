#!/usr/bin/env bash
# ============================================================
# seed-prod-libraries.sh
# 在生产环境创建「教学资源库」示例数据（PRD 第 3 / 4 章）：
#   · qidi  → 校级私有库、校级共享库、以 qidi 为主建单位的校际共建库
#   · dongyan → 校级共享库（作为共建库参建单位）
#
# 全部通过公开 API 创建（CreatorId 为对应租户的管理员账号），
# 不直接改库，保证「建设负责人 / 分配教师 / 成员」等关联数据一致。
#
# 用法：
#   BASE=https://www.iketong.com \
#   QIDI_TENANT=<qidi-tenant-guid> DONGYAN_TENANT=<dongyan-tenant-guid> \
#   QIDI_USER=qidi-admin DONGYAN_USER=dongyan-admin PASSWORD=123456 \
#   ./scripts/seed-prod-libraries.sh
# ============================================================
set -euo pipefail

BASE="${BASE:-https://www.iketong.com}"
API="$BASE/api/app"
PASSWORD="${PASSWORD:-123456}"
QIDI_USER="${QIDI_USER:-qidi-admin}"
DONGYAN_USER="${DONGYAN_USER:-dongyan-admin}"
QIDI_TENANT="${QIDI_TENANT:-}"
DONGYAN_TENANT="${DONGYAN_TENANT:-}"

if [ -z "$QIDI_TENANT" ] || [ -z "$DONGYAN_TENANT" ]; then
  echo "请设置 QIDI_TENANT / DONGYAN_TENANT（生产库 AbpTenants 中的租户 Id）" >&2
  exit 1
fi

login() { # $1=tenant $2=username
  curl -sk -X POST "$BASE/connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -H "__tenant: $1" \
    -d "grant_type=password&client_id=KnowledgeHub_App&scope=KnowledgeHub&username=$2&password=$PASSWORD" \
    | python3 -c "import json,sys;print(json.load(sys.stdin).get('access_token',''))"
}

# 创建资源库：$1=token $2=tenant $3=body(json)
create_library() {
  curl -sk -X POST "$API/resource-library" \
    -H "Authorization: Bearer $1" -H "__tenant: $2" -H "Content-Type: application/json" \
    -d "$3"
}

echo "==> 登录生产环境…"
QIDI_TOKEN="$(login "$QIDI_TENANT" "$QIDI_USER")"
DONGYAN_TOKEN="$(login "$DONGYAN_TENANT" "$DONGYAN_USER")"
[ -n "$QIDI_TOKEN" ] || { echo "qidi 登录失败" >&2; exit 1; }
[ -n "$DONGYAN_TOKEN" ] || { echo "dongyan 登录失败" >&2; exit 1; }

echo "==> 创建 qidi · 校级私有教学资源库（Visibility=0）"
create_library "$QIDI_TOKEN" "$QIDI_TENANT" '{
  "name": "qidi 校级私有教学资源库",
  "description": "qidi 建设的校级私有教学资源库：前台所有人可见可浏览，仅课程老师导入班级的学员可学习；不对外开放自学。",
  "principal": "qidi 教学团队",
  "visibility": 0,
  "isPublic": true,
  "openToExternalLearners": false
}' | python3 -c "import json,sys;d=json.load(sys.stdin);print('  id =',d.get('id'),'|',d.get('name'))"

echo "==> 创建 qidi · 校级共享教学资源库（Visibility=1，对外开放自学）"
create_library "$QIDI_TOKEN" "$QIDI_TENANT" '{
  "name": "qidi 校级共享教学资源库",
  "description": "qidi 建设的校级共享教学资源库：对外开放学习，其他学员完成手机号验证与实名认证后可学习库内课程并申请课程证书。",
  "principal": "qidi 教学团队",
  "visibility": 1,
  "isPublic": true,
  "openToExternalLearners": true
}' | python3 -c "import json,sys;d=json.load(sys.stdin);print('  id =',d.get('id'),'|',d.get('name'))"

echo "==> 创建 qidi 主建 · 东燕参建的校际共建教学资源库（Visibility=2）"
create_library "$QIDI_TOKEN" "$QIDI_TENANT" "{
  \"name\": \"qidi · 东燕 校际共建教学资源库\",
  \"description\": \"以 qidi 为主建单位、东燕为参建单位的校际共建教学资源库：库内课程可共享给参建单位，同时对外开放自学。\",
  \"principal\": \"qidi 教学团队\",
  \"visibility\": 2,
  \"isPublic\": true,
  \"openToExternalLearners\": true,
  \"partnerTenantIds\": [\"$DONGYAN_TENANT\"]
}" | python3 -c "import json,sys;d=json.load(sys.stdin);print('  id =',d.get('id'),'|',d.get('name'))"

echo "==> 创建 dongyan · 校级共享教学资源库（Visibility=1）"
create_library "$DONGYAN_TOKEN" "$DONGYAN_TENANT" '{
  "name": "东燕校级共享教学资源库",
  "description": "东燕建设的校级共享教学资源库：对外开放学习，其他学员完成手机号验证与实名认证后可学习库内课程并申请课程证书。",
  "principal": "东燕教学团队",
  "visibility": 1,
  "isPublic": true,
  "openToExternalLearners": true
}' | python3 -c "import json,sys;d=json.load(sys.stdin);print('  id =',d.get('id'),'|',d.get('name'))"

echo ""
echo "完成。库默认状态为「草稿」，请在管理端（教学资源库管理）点「发布」后才会出现在门户。"
