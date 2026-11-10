#!/usr/bin/env bash
# =============================================================================
# 演示数据准备/重置（本地）
# -----------------------------------------------------------------------------
# 为「特殊教育课程教学资源平台」演示准备以下数据：
#   ① 按模块分类的资源（政策经验 3 / 课标教材 2 / 融合教育 1 / 教具辅具 1 / 家长课堂 1）
#      其中含一条**可播放的 mp4**（用于「前台在线播放」演示）
#   ② 名师工作室：1 条已发布 + 1 条**待审**（用于「领衔人现场审核」演示）
#   ③ 互动课堂课表：3 条本周/本月智能体任务（含 DueTime）
#
# 用法：
#   ./prepare-demo-data.sh              # 准备（幂等：同名资源会跳过创建）
#   API_BASE=https://localhost:44305 ./prepare-demo-data.sh
#
# 依赖：ffmpeg（生成演示视频）。没有 ffmpeg 时跳过视频资源。
# =============================================================================
set -uo pipefail

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"; ADMIN_PASS="${ADMIN_PASS:-123456}"
TEACHER_USER="${TEACHER_USER:-wwq}";   TEACHER_PASS="${TEACHER_PASS:-123456}"
MEMBER_USER="${MEMBER_USER:-wb-teacher2}"; MEMBER_PASS="${MEMBER_PASS:-123456}"
STUDIO_NAME="${STUDIO_NAME:-语文名师工作室}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
TID=""; ADMIN=""; TEACHER=""; MEMBER=""; LAST_CODE=""

if [ -t 1 ]; then G=$'\033[32m'; Y=$'\033[33m'; R=$'\033[31m'; B=$'\033[36m'; O=$'\033[0m'
else G=""; Y=""; R=""; B=""; O=""; fi
step(){ printf '\n%s▶ %s%s\n' "$B" "$*" "$O"; }
ok(){ printf '  %s✓%s %s\n' "$G" "$O" "$*"; }
warn(){ printf '  %s!%s %s\n' "$Y" "$O" "$*"; }
bad(){ printf '  %s✗%s %s\n' "$R" "$O" "$*"; }

pyget(){ python3 -c "import json,sys;d=json.load(sys.stdin);
p='$1'.split('.')
for k in p:
    d = d[int(k)] if isinstance(d,list) else d.get(k)
    if d is None: break
print('' if d is None else d)"; }

login(){
  curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' -H "Abp-TenantId: ${TID}" \
    --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=offline_access KnowledgeHub' \
    --data-urlencode "username=$1" --data-urlencode "password=$2" \
    | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
}
api(){ # method path token [body]
  local m="$1" p="$2" t="$3" b="${4:-}"
  local -a a
  a=(-s $CURL_SSL_ARGS -X "$m" -o "$WORK/r.json" -w '%{http_code}')
  [ "$t" != "-" ] && a+=(-H "Authorization: Bearer $t")
  a+=(-H "Abp-TenantId: ${TID}" -H 'Accept: application/json')
  [ -n "$b" ] && a+=(-H 'Content-Type: application/json' --data-binary "$b")
  LAST_CODE="$(curl "${a[@]}" "${API_BASE}${p}")"
}
jget(){ pyget "${1:-}" < "$WORK/r.json"; }

echo "=============================================================================="
echo " 演示数据准备   API=$API_BASE  租户=$TENANT_NAME"
echo "=============================================================================="

step "0/5 解析租户 + 登录三个账号"
api GET /api/public/tenants -
TID="$(python3 -c "
import json,sys
for t in json.load(open('$WORK/r.json')) or []:
    if str(t.get('name'))=='$TENANT_NAME' or str(t.get('tenantName'))=='$TENANT_NAME':
        print(t.get('id') or ''); break")"
[ -n "$TID" ] || { bad "找不到租户 $TENANT_NAME"; exit 1; }
ok "租户 $TENANT_NAME = $TID"

ADMIN="$(login "$ADMIN_USER" "$ADMIN_PASS")"
TEACHER="$(login "$TEACHER_USER" "$TEACHER_PASS")"
MEMBER="$(login "$MEMBER_USER" "$MEMBER_PASS")"
[ -n "$ADMIN" ] && ok "管理员 $ADMIN_USER" || { bad "管理员登录失败"; exit 1; }
[ -n "$TEACHER" ] && ok "教师 $TEACHER_USER" || { bad "教师登录失败"; exit 1; }
[ -n "$MEMBER" ] && ok "成员 $MEMBER_USER" || { bad "成员登录失败（可选账号，可忽略）"; }

# ── 资源：按模块建，已存在则跳过 ────────────────────────────────
step "1/5 按模块建演示资源"
mk_resource(){ # name module desc token
  local name="$1" module="$2" desc="$3" tok="${4:-$TEACHER}"
  api GET "/api/app/resource/filtered-list?filter=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$name")&maxResultCount=20" "$tok"
  local exist
  exist="$(python3 -c "
import json
d=json.load(open('$WORK/r.json'))
for i in d.get('items') or []:
    if i.get('name')=='$name': print(i.get('id')); break")"
  if [ -n "$exist" ]; then ok "已存在：$name"; echo "$exist"; return 0; fi
  api POST /api/app/resource "$tok" "$(python3 -c "
import json,sys
print(json.dumps({'name':sys.argv[1],'description':sys.argv[2],'resourceType':0,
 'module':int(sys.argv[3]),'isDownloadable':True,'filePath':'','fileSize':0}, ensure_ascii=False))" "$name" "$desc" "$module")"
  local id; id="$(jget id)"
  [ -n "$id" ] && ok "新建：${name}（模块 ${module}）" || warn "新建失败：$name"
  echo "$id"
}
audit_resource(){ # id
  # 已是「本校通过 / 联盟通过」的直接跳过，避免重复审核造成的 403 噪音
  api GET "/api/app/resource/$1" "$TEACHER"
  local st; st="$(jget status)"
  if [ "${st:-0}" -ge 2 ] 2>/dev/null; then
    ok "  已审核通过（status=${st}），跳过"
    return 0
  fi
  api POST "/api/app/resource/submit-for-review/$1" "$TEACHER"
  api POST /api/app/resource/audit "$ADMIN" "{\"resourceId\":\"$1\",\"status\":1,\"comment\":\"演示数据\"}"
  [ "$LAST_CODE" = "200" ] && ok "  审核通过" || warn "  审核返回 $LAST_CODE"
}

IDS=""
for spec in "特殊教育政策原文汇编|4|政策原文" "特殊教育政策要点解读|4|政策解读" \
            "融合教育课堂策略汇编|3|课堂策略与干预案例" "培智学校课标（2026版）|6|课标电子教材" \
            "视障学生教具使用说明|7|图文+视频说明" "亲子沟通家长课堂|8|家长课堂资源"; do
  n="${spec%%|*}"; rest="${spec#*|}"; m="${rest%%|*}"; d="${rest#*|}"
  rid="$(mk_resource "$n" "$m" "$d" | tail -1)"
  [ -n "$rid" ] && { audit_resource "$rid"; IDS="$IDS $rid"; }
done

# ── 视频资源（可播放在线视频用）────────────────────────────────
step "2/5 生成并上传演示视频（用于「前台在线播放」）"
# 视频素材来源：优先用现成文件（VIDEO_FILE，远程服务器无 ffmpeg 时从本地 scp 过来）；
# 没有现成文件且本机有 ffmpeg 时现场生成。
if [ -n "${VIDEO_FILE:-}" ] && [ -f "${VIDEO_FILE}" ]; then
  cp "${VIDEO_FILE}" "$WORK/特教教学资源演示.mp4"
  ok "使用现成视频素材：$WORK/特教教学资源演示.mp4"
elif command -v ffmpeg >/dev/null 2>&1; then
  VID_NAME="特教教学资源演示.mp4"
  api GET "/api/app/resource/filtered-list?filter=$(python3 -c "import urllib.parse;print(urllib.parse.quote('特教课堂教学示范视频'))")&maxResultCount=5" "$TEACHER"
  EXIST="$(python3 -c "
import json
d=json.load(open('$WORK/r.json'))
for i in d.get('items') or []:
    if i.get('name')=='特教课堂教学示范视频': print(i.get('id')); break")"
  if [ -n "$EXIST" ]; then
    ok "视频资源已存在：$EXIST"
  else
    ffmpeg -y -f lavfi -i "testsrc2=size=1280x720:rate=25:duration=12" \
      -f lavfi -i "sine=frequency=523:duration=12" \
      -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest -movflags +faststart \
      "$WORK/$VID_NAME" >/dev/null 2>&1
    size="$(wc -c < "$WORK/$VID_NAME" | tr -d ' ')"
    api POST /api/app/chunk-upload/initiate "$TEACHER" \
      "{\"fileName\":\"$VID_NAME\",\"totalSize\":$size,\"chunkSize\":1048576}"
    up="$(jget uploadId)"
    curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/api/app/chunk-upload/upload" \
      -H "Authorization: Bearer $TEACHER" -H "Abp-TenantId: $TID" \
      -F "file=@$WORK/$VID_NAME;filename=$VID_NAME" -F "uploadId=$up" \
      -F "fileName=$VID_NAME" -F "chunkNumber=0" -o /dev/null
    api POST /api/app/chunk-upload/complete "$TEACHER" \
      "{\"uploadId\":\"$up\",\"fileName\":\"$VID_NAME\",\"totalChunks\":1}"
    fp="$(jget filePath)"; fs="$(jget fileSize)"; fe="$(jget fileExtension)"
    api POST /api/app/resource "$TEACHER" "$(python3 -c "
import json,sys
print(json.dumps({'name':'特教课堂教学示范视频','description':'课标教材配套示范课',
 'resourceType':1,'module':6,'isDownloadable':True,'filePath':sys.argv[1],
 'fileSize':int(sys.argv[2]),'fileExtension':sys.argv[3],
 'originalFileName':'$VID_NAME'}, ensure_ascii=False))" "$fp" "${fs:-0}" "${fe:-.mp4}")"
    vid="$(jget id)"
    [ -n "$vid" ] && { ok "视频资源：$vid"; audit_resource "$vid"; } || warn "视频资源创建失败"
  fi
else
  warn "既无 VIDEO_FILE 也未安装 ffmpeg，跳过视频资源"
fi

# ── 工作室：确保存在 + 一条待审资料 ───────────────────────────
step "3/5 名师工作室 + 待审资料"
api GET /api/app/studio "$ADMIN"
SID="$(python3 -c "
import json
for s in json.load(open('$WORK/r.json')) or []:
    if s.get('name')=='$STUDIO_NAME': print(s.get('id')); break")"
if [ -z "$SID" ]; then
  leader="$(api GET "/api/identity/users?filter=${TEACHER_USER}" "$ADMIN"; jget items.0.id)"
  api POST /api/app/studio "$ADMIN" "{\"name\":\"$STUDIO_NAME\",\"description\":\"特教语文协同教研\",\"leaderUserId\":\"$leader\"}"
  SID="$(jget id)"
  [ -n "$SID" ] && ok "新建工作室：$SID" || { bad "工作室创建失败"; }
else
  ok "工作室已存在：$SID"
fi

# 成员加入
if [ -n "$MEMBER" ]; then
  MID="$(api GET "/api/identity/users?filter=${MEMBER_USER}" "$ADMIN"; jget items.0.id)"
  [ -n "$MID" ] && api POST "/api/app/studio/$SID/member" "$TEACHER" "{\"userId\":\"$MID\",\"role\":1}" >/dev/null
fi

# 待审资料（若当前无待审）
api GET "/api/app/studio/$SID" "$TEACHER"
PENDING="$(python3 -c "
import json
d=json.load(open('$WORK/r.json'))
print(sum(1 for m in (d.get('materials') or []) if m.get('status')==1))")"
if [ "${PENDING:-0}" -eq 0 ] && [ -n "$MEMBER" ]; then
  api POST /api/app/resource "$MEMBER" '{"name":"《春》课堂实录片段与反思","resourceType":0,"module":1,"isDownloadable":true,"filePath":"","fileSize":0}'
  nrid="$(jget id)"
  if [ -n "$nrid" ]; then
    api POST "/api/app/studio/$SID/material" "$MEMBER" \
      "{\"resourceId\":\"$nrid\",\"title\":\"《春》课堂实录片段与反思\",\"description\":\"成员提交，等领衔人审核\",\"submitNow\":true}"
    [ "$LAST_CODE" = "200" ] && ok "已造 1 条待审资料（供领衔人现场审核）" || warn "待审资料创建失败 $LAST_CODE"
  fi
else
  ok "已有 $PENDING 条待审资料"
fi

# ── 课表：确保有本周任务 ──────────────────────────────────
step "4/5 课表任务（本周/本月，含截止时间）"
api GET "/api/app/classroom-agent-task/timetable?from=$(date -v-0d +%Y-%m-%dT00:00:00 2>/dev/null || date +%Y-%m-%dT00:00:00)&to=$(date -v+14d +%Y-%m-%dT23:59:59 2>/dev/null || date +%Y-%m-%dT23:59:59)" "$TEACHER"
TC="$(jget totalCount)"
if [ "${TC:-0}" -gt 0 ]; then
  ok "课表已有 $TC 个条目"
else
  warn "本周没有智能体任务 —— 请在「教学智能体」给课程分配任务并设置截止时间后发布"
  warn "（本脚本不自动造任务：需要已发布的智能体版本 + 课程，容易因环境不同失败）"
fi

step "5/5 汇总"
cat <<SUM
  演示入口：
    白板      http://localhost:4200/learning/whiteboards
    工作室    http://localhost:4200/learning/studios
    课表      http://localhost:4200/learning/classroom-timetable
    资源中心  http://localhost:4200/resources
  账号：${ADMIN_USER} / ${TEACHER_USER} / ${MEMBER_USER}（密码 123456；学生 zmq 1234567）
  详见：docs/演示方案-操作脚本.md
SUM
