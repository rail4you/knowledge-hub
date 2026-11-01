#!/usr/bin/env bash
# iketong V2 环境初始化：功能开关 + 演示账号
set -e
API=https://www.iketong.com
QIDI=3a207891-a06c-8876-b34d-32a245adec63
GUO=3a20df36-769c-89bf-e56f-7d841b3b0a2d
DONG=3a23962d-6334-9807-4d20-54c30936c731

tok() { # tok <tenant|-> <user> <pass>
  local t=$1 u=$2 p=$3; local h=()
  [ "$t" != "-" ] && h=(-H "__tenant: $t")
  curl -sk -X POST $API/connect/token -H 'Content-Type: application/x-www-form-urlencoded' "${h[@]}" \
    --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p" \
    | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
}

A=$(tok - admin '1q2w3E*')
[ -z "$A" ] && { echo "host admin token 失败"; exit 1; }
echo "host admin token ok"

echo "=== 1) 开启 V2 功能开关 ==="
for T in $QIDI $GUO $DONG; do
  code=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT "$API/api/feature-management/features?providerName=T&providerKey=$T" \
    -H "Authorization: Bearer $A" -H 'Content-Type: application/json' \
    -d '{"features":[
      {"name":"KnowledgeHub.LibraryReview","value":"true"},
      {"name":"KnowledgeHub.BuildTask","value":"true"},
      {"name":"KnowledgeHub.CrossSchoolRef","value":"true"},
      {"name":"KnowledgeHub.EnterpriseWorkspace","value":"true"},
      {"name":"KnowledgeHub.OrganizationValidity","value":"true"}]}')
  echo "  features $T -> $code"
done

echo "=== 2) 重置 qidi/zmq 密码为 1234567 ==="
Q=$(tok qidi qidi-admin 123456)
ZMQ_ID=$(curl -sk "$API/api/app/tenant-user?filter=zmq" -H "Authorization: Bearer $Q" -H '__tenant: qidi' \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);items=d.get("items",d if isinstance(d,list) else []);print(next((i["id"] for i in items if i.get("userName")=="zmq"),""))')
echo "  zmq id=$ZMQ_ID"
if [ -n "$ZMQ_ID" ]; then
  curl -sk -o /dev/null -w "  zmq reset -> %{http_code}\n" -X PUT "$API/api/app/tenant-user/$ZMQ_ID" \
    -H "Authorization: Bearer $Q" -H '__tenant: qidi' -H 'Content-Type: application/json' \
    -d '{"userName":"zmq","email":"zmq@iketong.local","name":"ZMQ","surname":"学生","isActive":true,"password":"1234567","roleType":3,"roleNames":["Student"]}'
fi

echo "=== 3) 创建 guozhou-teacher ==="
G=$(tok guozhou guozhou 123456)
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$API/api/app/tenant-user/user-for-tenant" \
  -H "Authorization: Bearer $G" -H '__tenant: guozhou' -H 'Content-Type: application/json' \
  -d '{"userName":"guozhou-teacher","emailAddress":"guozhou-teacher@iketong.local","password":"123456","name":"国州","surname":"教师","isActive":true,"roleType":2,"roleNames":["Teacher"]}')
echo "  guozhou-teacher -> $code"

echo "=== 4) 创建 qidi 企业账号 qidi-ent ==="
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$API/api/app/tenant-user/user-for-tenant" \
  -H "Authorization: Bearer $Q" -H '__tenant: qidi' -H 'Content-Type: application/json' \
  -d '{"userName":"qidi-ent","emailAddress":"qidi-ent@iketong.local","password":"123456","name":"启迪","surname":"企业","isActive":true,"companyName":"易课通康养科技（演示）","roleType":4,"roleNames":["EnterpriseUser"]}')
echo "  qidi-ent -> $code"

echo "=== 5) 验证登录 ==="
try() {
  local t=$1 u=$2 p=$3; local h=()
  [ "$t" != "-" ] && h=(-H "__tenant: $t")
  out=$(curl -sk -X POST $API/connect/token -H 'Content-Type: application/x-www-form-urlencoded' "${h[@]}" \
    --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p")
  echo "$out" | grep -q access_token && echo "  OK   $t/$u" || echo "  FAIL $t/$u"
}
try qidi qidi-admin 123456
try qidi wwq 123456
try qidi zmq 1234567
try qidi qidi-ent 123456
try guozhou guozhou 123456
try guozhou guozhou-teacher 123456
try - admin '1q2w3E*'
try - league-admin 123456
