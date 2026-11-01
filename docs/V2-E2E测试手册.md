# V2.0 E2E 测试手册（多模型联合验证用）

> 目标读者：执行验证的模型 / 工程师。**每个用例都可直接复制执行**，包含前置、步骤、期望、判定标准。
> 设计为**模块化并行**：M1~M8 之间无依赖，可分配给不同模型同时跑（唯一共享前提是 M0 环境自检）。
> 相关文档：`docs/V2-README.md`（文档索引）、`docs/V2-验收与回退手册.md`（业务验收口径）、`docs/V2-E2E验证报告.md`（已测结果）。
>
> **不想手打命令？** 本文每个模块都有对应脚本，直接跑即可（等价于本手册的自动化版本）：
> ```bash
> ./scripts/test/v2-e2e-modules.sh M0   # 环境自检（必做）
> ./scripts/test/v2-e2e-modules.sh M1   # 审核内核与库生命周期
> ./scripts/test/v2-e2e-modules.sh M2   # 内容准入与可见性（含文件越权高危用例）
> ./scripts/test/v2-e2e-modules.sh M3   # 共建任务
> ./scripts/test/v2-e2e-modules.sh M4   # 跨校引用
> ./scripts/test/v2-e2e-modules.sh M5   # 企业/产教/有效期（唯一会改功能开关的模块，建议最后跑）
> ./scripts/test/v2-e2e-modules.sh M6   # 权限/AI/检索治理
> ./scripts/test/v2-e2e-modules.sh M7   # 前端 UI 人工清单（仅打印，不执行）
> ```
> 手工执行本手册的价值：**逐条核对"期望"文字**，脚本只断言关键点。
> 两者结果不一致时，以手工核对为准并记录差异。

---

## 0. 环境自检（所有模型开工前必做）

```bash
API=https://localhost:44305

# 1) API 是否在跑
curl -sk -o /dev/null -w "health=%{http_code}\n" $API/health-status
# 期望：health=200。若为 000 → API 没启动（./dev.sh start api），不要继续，直接报告环境阻塞
```

```bash
# 2) 取令牌工具（复制到你的 shell）
API=https://localhost:44305
tok() { # tok <tenant|-> <user> <pass>
  local t=$1 u=$2 p=$3 a=(-sk -X POST $API/connect/token -H 'Content-Type: application/x-www-form-urlencoded')
  [ "$t" != "-" ] && a+=(-H "__tenant: $t")
  curl "${a[@]}" --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p" \
    | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
}

# 3) 六个令牌（长度 >1000 为正常）
for spec in "guozhou:guozhou-admin:123456" "qidi:qidi-admin:123456" "qidi:wwq:123456" "qidi:zmq:1234567" "guozhou:ent-demo:123456" "-:admin:1q2w3E*" "-:league-admin:123456"; do
  T=${spec%%:*}; rest=${spec#*:}; U=${rest%%:*}; P=${rest#*:}
  tk=$(tok "$T" "$U" "$P"); echo "$T/$U token_len=${#tk}"
done
# 期望：7 行都是 token_len≈1200~1400；若 400/400/000 → 账号密码或环境有问题，报告后停止
```

**租户 ID（脚本里会用到）**

| 租户 | ID |
|---|---|
| guozhou | `3a23961d-ebad-94e2-5179-643fff1681b1` |
| qidi | `3a2034d1-6c43-2219-8149-45182659c849` |

**账号（本地测试环境）**

| 租户 | 账号 | 密码 | 角色 | 用途 |
|---|---|---|---|---|
| qidi | `qidi-admin` | 123456 | SchoolAdmin | 建库/校审/建任务/验收/核实去向 |
| qidi | `wwq` | 123456 | Teacher | 共享资源、提交交付、引用 |
| qidi | `zmq` | 1234567 | Student | 学习、投递、越权验证 |
| guozhou | `guozhou-admin` | 123456 | SchoolAdmin | 共建库主建校 |
| guozhou | `guozhou-teacher` | 123456 | Teacher+SchoolAdmin | 注意：双角色，测"纯教师"需另用 wwq |
| guozhou | `ent-demo` | 123456 | EnterpriseUser | 企业工作台（已认证企业） |
| host | `admin` | `1q2w3E*` | host admin | 平台侧、两级审核都能做 |
| host | `league-admin` | 123456 | LeagueAdmin | **联盟终审**（必须与初审不同人） |

**数据发现（可选，需本地 psql）**

```bash
psql_q() { PGPASSWORD=postgres psql -h localhost -p 5433 -U postgres -d KnowledgeHub -t -A -c "$1"; }
# 取一个 qidi 草稿资源（用于共享入库）
psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false AND \"Status\" IN (0,2,3) ORDER BY \"CreationTime\" DESC LIMIT 1"
```

---

## 通用约定

1. **写操作会改测试环境数据**。每个模块末尾有清理命令，**务必执行**。
2. 判定 PASS 需要「接口返回 + 数据库/后续查询」双重佐证，避免只看 HTTP 200。
3. 遇到 `000` 一律先怀疑环境（API 挂了），不要记为功能失败。
4. 遇到 `401/302` 表示未登录或租户头缺失。
5. 报告时请用第 9 节的模板，逐用例贴**实际返回片段**。

---

# M1 审核内核与库生命周期（负责人：模型 A）

**前置**：`qidi-admin`、`admin` 令牌；qidi 已开启 `LibraryReview`（M0 未开则先开，见 M5.0 的开关命令）

### M1-1 建库进入待院校审核
```bash
T=$(tok qidi qidi-admin 123456); API=https://localhost:44305
LIB=$(curl -sk -X POST $API/api/app/resource-library -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{"name":"M1-校内库","visibility":0}' \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print(d.get("id",""));import sys;print("status=",d.get("status"),file=sys.stderr)')
echo "LIB=$LIB"
```
**期望**：`status=3`（PendingReview）
**判定**：status 必须为 3（0=开关没开或旧行为，需报告）

### M1-2 生成审核单并校验级数
```bash
A=$(tok - admin '1q2w3E*')
TID=$(curl -sk "$API/api/app/review-ticket?TargetId=$LIB" -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;i=json.load(sys.stdin)["items"][0];print(i["id"]);import sys;print("stages=",i["totalStages"],"stage=",i["stageName"],file=sys.stderr)')
echo "TID=$TID"
```
**期望**：`stages=1 stage=院校初审`（校内私有库=单级）
**判定**：级数=1；若=2 说明策略解析按共建库走了（BUG）

### M1-3 自审必须被拒（关键安全用例）
```bash
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d "{\"ticketId\":\"$TID\",\"decision\":0}"
```
**期望**：`code=KnowledgeHub:Review:CannotReviewOwn`
**判定**：出现该 code=PASS；若返回 200 且库变已发布=**严重 BUG**

### M1-4 他人校审通过
```bash
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" \
  -H 'Content-Type: application/json' -d "{\"ticketId\":\"$TID\",\"decision\":0}" | head -c 200
curl -sk "$API/api/app/resource-library/$LIB" -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;print("status=",json.load(sys.stdin).get("status"))'
```
**期望**：`status=1`（Active）

### M1-5 改库走「变更审核」，期间按旧配置服务
```bash
curl -sk -X PUT $API/api/app/resource-library/$LIB -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{"description":"M1-改库后"}' -o /dev/null
curl -sk "$API/api/app/resource-library/$LIB" -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print("status=",d.get("status"),"desc=",d.get("description"))'
```
**期望**：`status=6`（ChangePending）且 `desc` 仍是旧值
**判定**：状态=6 且 description 未变=PASS（说明未提前应用）

### M1-6 驳回 → 重提（轮次 +1）
```bash
LIB2=$(curl -sk -X POST $API/api/app/resource-library -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{"name":"M1-驳回重提","visibility":0}' | python3 -c 'import json,sys;print(json.load(sys.stdin)["id"])')
TID2=$(curl -sk "$API/api/app/review-ticket?TargetId=$LIB2" -H "Authorization: Bearer $T" -H "__tenant: qidi" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' \
  -d "{\"ticketId\":\"$TID2\",\"decision\":1,\"reason\":\"名称不规范\"}" -o /dev/null
curl -sk -X POST $API/api/app/review-ticket/$TID2/resubmit -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{}' | python3 -c 'import json,sys;d=json.load(sys.stdin);print("status=",d.get("statusName"),"round=",d.get("roundNo"))'
```
**期望**：`status=待审核 round=2`

### M1-7 并发审批只成功一次
```bash
PAR=$(curl -sk -X POST $API/api/app/resource-library -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{"name":"M1-并发","visibility":0}' | python3 -c 'import json,sys;print(json.load(sys.stdin)["id"])')
PT=$(curl -sk "$API/api/app/review-ticket?TargetId=$PAR" -H "Authorization: Bearer $T" -H "__tenant: qidi" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
L=$(tok - league-admin 123456)
( curl -sk -o /dev/null -w "%{http_code}" -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$PT\",\"decision\":0}" > /tmp/p1 ) &
( curl -sk -o /dev/null -w "%{http_code}" -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $L" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$PT\",\"decision\":0}" > /tmp/p2 ) &
wait; echo "codes=$(cat /tmp/p1)/$(cat /tmp/p2)"
```
**期望**：一个 `200`、一个 `403`（或 500），且 `AppReviewActions` 里该单「通过」记录只有 1 条
```bash
psql_q "SELECT count(*) FROM \"AppReviewActions\" WHERE \"TicketId\"='$PT' AND \"Operation\"=1"
```

### M1-8 删库审核 → 归档（不硬删）
```bash
curl -sk -X DELETE $API/api/app/resource-library/$LIB -H "Authorization: Bearer $T" -H "__tenant: qidi" -o /dev/null
DT=$(curl -sk "$API/api/app/review-ticket?TargetId=$LIB" -H "Authorization: Bearer $T" -H "__tenant: qidi" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$DT\",\"decision\":0}" -o /dev/null
psql_q "SELECT \"Status\", \"IsDeleted\" FROM \"AppResourceLibraries\" WHERE \"Id\"='$LIB'"
```
**期望**：`Status=2`（Archived）、`IsDeleted=f`（行仍在）
**判定**：若有行且 IsDeleted=f=PASS；若查不到行=**BUG（硬删了）**

### M1 清理
```bash
for L in $LIB $LIB2 $PAR; do
  psql_q "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$L')" >/dev/null
  psql_q "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$L'" >/dev/null
  psql_q "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\"='$L'" >/dev/null
  psql_q "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\"='$L'" >/dev/null
done
```

---

# M2 内容准入与可见性（负责人：模型 B）

### M2-1 共享资源入私有库 → 生成准入记录
```bash
T=$(tok qidi qidi-admin 123456); W=$(tok qidi wwq 123456); API=https://localhost:44305
LIB=$(curl -sk -X POST $API/api/app/resource-library -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{"name":"M2-准入库","visibility":0}' | python3 -c 'import json,sys;print(json.load(sys.stdin)["id"])')
A=$(tok - admin '1q2w3E*')
TID=$(curl -sk "$API/api/app/review-ticket?TargetId=$LIB" -H "Authorization: Bearer $T" -H "__tenant: qidi" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$TID\",\"decision\":0}" -o /dev/null

# 分配教师（私有库必须分配才可见）
WWQ=$(psql_q "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq'")
curl -sk -X POST $API/api/app/resource-library/$LIB/assign-teacher -H "Authorization: Bearer $T" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d "{\"userId\":\"$WWQ\"}" -o /dev/null

RES=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false AND \"Status\" IN (0,2,3) ORDER BY \"CreationTime\" DESC LIMIT 1")
curl -sk -X POST $API/api/app/teaching-resource-library/$LIB/attach -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d "{\"contentType\":0,\"contentId\":\"$RES\"}" | head -c 160; echo
psql_q "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB' AND \"ContentId\"='$RES'"
```
**期望**：接口 200；`AppResourcePublications.Status=1`（PendingReview，**未直接入库**）

### M2-2 审核通过 → 发布 + 内容状态联动
```bash
PT=$(psql_q "SELECT t.\"Id\" FROM \"AppReviewTickets\" t JOIN \"AppResourcePublications\" p ON p.\"Id\"=t.\"TargetId\" WHERE p.\"LibraryId\"='$LIB' AND t.\"Status\"=0 LIMIT 1")
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$PT\",\"decision\":0}" -o /dev/null
psql_q "SELECT p.\"Status\", r.\"Status\", (r.\"LibraryId\" IS NOT NULL) FROM \"AppResourcePublications\" p JOIN \"AppResources\" r ON r.\"Id\"=p.\"ContentId\" WHERE p.\"LibraryId\"='$LIB'"
```
**期望**：`2`（Published）/ `3`（LeagueApproved）/ `t`（已挂库）

### M2-3 库内只展示已发布（关键）
```bash
# 再共享一个资源但**不审**
RES2=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false AND \"Id\"<>'$RES' LIMIT 1")
curl -sk -X POST $API/api/app/teaching-resource-library/$LIB/attach -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d "{\"contentType\":0,\"contentId\":\"$RES2\"}" -o /dev/null
curl -sk "$API/api/app/teaching-resource-library/$LIB/contents?MaxResultCount=50" -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print("total=",d.get("totalCount"));[print(" -",i["name"],i.get("publicationStatusName")) for i in d.get("items",[])]'
```
**期望**：`$RES2`（待审）**不出现**；`$RES` 出现且 `已发布`（提交人本人可见自己的待审项属正常，用另一个账号验证更严格）

### M2-4 文件 URL 不能绕过库授权（**高危用例**）
```bash
S=$(tok qidi zmq 1234567)
# 资源在私有库且学生未被分配
for act in preview download; do
  curl -sk -o /dev/null -w "  $act=%{http_code}\n" "$API/api/resource-file/$RES/$act" -H "Authorization: Bearer $S" -H "__tenant: qidi"
done
```
**期望**：两个都是 `403`
**判定**：若任一为 200 = **严重 BUG（越权）**，立即报告

### M2 清理
```bash
psql_q "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$LIB') OR \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB'))" >/dev/null
psql_q "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB')" >/dev/null
psql_q "DELETE FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB'" >/dev/null
psql_q "DELETE FROM \"AppResourceLibraryTeachers\" WHERE \"LibraryId\"='$LIB'" >/dev/null
psql_q "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\"='$LIB'" >/dev/null
psql_q "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$LIB'" >/dev/null
psql_q "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\"='$LIB'" >/dev/null
```

---

# M3 共建任务（负责人：模型 C）

### M3-1 从模板建任务并发布
```bash
T=$(tok guozhou guozhou-admin 123456); API=https://localhost:44305
TPL=$(curl -sk "$API/api/app/build-task/task-templates/00000000-0000-0000-0000-000000000000" -H "Authorization: Bearer $T" -H "__tenant: guozhou" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(d[0]["id"] if d else "")')
LIB=$(curl -sk "$API/api/app/resource-library" -H "Authorization: Bearer $T" -H "__tenant: guozhou" | python3 -c 'import json,sys;d=json.load(sys.stdin);items=d if isinstance(d,list) else d.get("items",[]);print(next((x["id"] for x in items if x.get("isOwner")), ""))')
echo "TPL=$TPL LIB=$LIB"
TASK=$(curl -sk -X POST $API/api/app/build-task/task -H "Authorization: Bearer $T" -H "__tenant: guozhou" -H 'Content-Type: application/json' \
  -d "{\"libraryId\":\"$LIB\",\"templateId\":\"$TPL\",\"title\":\"M3-共建任务\",\"collaboratorSchoolIds\":[\"3a2034d1-6c43-2219-8149-45182659c849\"]}" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print(d.get("id",""));import sys;print("required=",d.get("requiredTotal"),"status=",d.get("statusName"),file=sys.stderr)')
curl -sk -X POST $API/api/app/build-task/$TASK/publish-task -H "Authorization: Bearer $T" -H "__tenant: guozhou" -o /dev/null
```
**期望**：`required>0`、发布后 `已发布`

### M3-2 协作校教师能看到并提交交付
```bash
W=$(tok qidi wwq 123456)
curl -sk "$API/api/app/build-task/tasks?OnlyMine=true&MaxResultCount=50" -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print("my_tasks=",d.get("totalCount"))'
# 期望 ≥1（协作校维度可见）
ITEM=$(psql_q "SELECT \"Id\" FROM \"AppBuildTaskItems\" WHERE \"TaskId\"='$TASK' ORDER BY \"SortOrder\" LIMIT 1")
RES=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false LIMIT 1")
DELIV=$(curl -sk -X POST $API/api/app/build-task/submit-delivery -H "Authorization: Bearer $W" -H "__tenant: qidi" -H 'Content-Type: application/json' \
  -d "{\"taskId\":\"$TASK\",\"taskItemId\":\"$ITEM\",\"contentType\":0,\"contentId\":\"$RES\"}" \
  | python3 -c 'import json,sys;print(json.load(sys.stdin).get("id",""))')
echo "DELIV=$DELIV"
```
**期望**：`my_tasks≥1`；提交成功；同时生成准入记录（验收≠发布）

### M3-3 验收 ≠ 发布
```bash
curl -sk -X POST $API/api/app/build-task/review-delivery -H "Authorization: Bearer $T" -H "__tenant: guozhou" -H 'Content-Type: application/json' \
  -d "{\"deliveryId\":\"$DELIV\",\"accept\":true,\"comment\":\"符合\"}" -o /dev/null
curl -sk "$API/api/app/build-task/$TASK/task" -H "Authorization: Bearer $T" -H "__tenant: guozhou" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print("accepted=",d.get("acceptedTotal"),"/",d.get("requiredTotal"),"status=",d.get("statusName"))'
psql_q "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"ContentId\"='$RES' AND \"LibraryId\"='$LIB' ORDER BY \"CreationTime\" DESC LIMIT 1"
```
**期望**：进度 ≥1/N；发布记录仍 `1`（PendingReview）
**判定**：若发布记录变 2（直接发布）= **BUG（验收替代了审核）**

### M3-4 协作校不可验收
```bash
curl -sk "$API/api/app/build-task/$TASK/task" -H "Authorization: Bearer $W" -H "__tenant: qidi" | python3 -c 'import json,sys;print("canReview=",json.load(sys.stdin).get("canReview"))'
```
**期望**：`canReview=False`

---

# M4 跨校引用（负责人：模型 D）

### M4-1 引用未发布内容必须被拒
```bash
Q=$(tok qidi qidi-admin 123456); W=$(tok qidi wwq 123456); API=https://localhost:44305
COURSE=$(psql_q "SELECT \"Id\" FROM \"AppCourses\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false AND \"TeacherId\"=(SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq') LIMIT 1")
RES=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"IsDeleted\"=false LIMIT 1")
LIB=$(psql_q "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"TenantId\"='3a2034d1-6c43-2219-8149-45182659c849' AND \"Status\"=1 LIMIT 1")
curl -sk -X POST $API/api/app/course-shared-ref -H "Authorization: Bearer $W" -H "__tenant: qidi" -H 'Content-Type: application/json' \
  -d "{\"courseId\":\"$COURSE\",\"libraryId\":\"$LIB\",\"contentId\":\"$RES\"}" | head -c 200; echo
```
**期望**：`该内容尚未在该资源库发布（或已下架），不能引用`（若该内容恰好已发布，换一个资源或跳过并记录）

### M4-2 引用已发布内容 → 学生访问判定
> 依赖 M2 或已有发布记录。找一条已发布到某库的资源：
```bash
ROW=$(psql_q "SELECT p.\"LibraryId\"||'|'||p.\"ContentId\" FROM \"AppResourcePublications\" p WHERE p.\"Status\"=2 AND p.\"ContentType\"=0 LIMIT 1")
LIB=${ROW%%|*}; RES=${ROW##*|}
REF=$(curl -sk -X POST $API/api/app/course-shared-ref -H "Authorization: Bearer $W" -H "__tenant: qidi" -H 'Content-Type: application/json' \
  -d "{\"courseId\":\"$COURSE\",\"libraryId\":\"$LIB\",\"contentId\":\"$RES\"}" | python3 -c 'import json,sys;print(json.load(sys.stdin).get("id",""))')
S=$(tok qidi zmq 1234567)
curl -sk -X POST $API/api/app/course-shared-ref/check-access/$REF -H "Authorization: Bearer $S" -H "__tenant: qidi" -H 'Content-Type: application/json' -d '{}'
```
**期望**：已选课学生 `canAccess=true`；未选课学生 `canAccess=false` + 「尚未加入该课程」

### M4-3 撤销后立即失效
```bash
curl -sk -X POST "$API/api/app/course-shared-ref/$REF/revoke?reason=M4测试" -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  -H 'Content-Type: application/json' -d '{}' -o /dev/null
curl -sk -X POST $API/api/app/course-shared-ref/check-access/$REF -H "Authorization: Bearer $S" -H "__tenant: qidi" -H 'Content-Type: application/json' -d '{}'
```
**期望**：`canAccess=false` + 「该资源已撤销引用或下架」

### M4-4 来源下架自动撤销引用（联动）
```bash
# 先建一条生效引用，再把发布记录下架，验证引用被自动撤销
psql_q "UPDATE \"AppResourcePublications\" SET \"Status\"=4 WHERE \"LibraryId\"='$LIB' AND \"ContentId\"='$RES'"
curl -sk "$API/api/app/course-shared-ref?Direction=all&MaxResultCount=50" -H "Authorization: Bearer $W" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print([ (x["statusName"],x.get("revokedReason")) for x in d.get("items",[])[:3]])'
```
> 注：通过接口触发 `WithdrawAsync`（教学资源库 `detach`）才会走自动撤销；直接改库需自行判断，此用例建议用 detach 触发：
```bash
curl -sk -X POST $API/api/app/teaching-resource-library/$LIB/detach/$RES -H "Authorization: Bearer $W" -H "__tenant: qidi" -o /dev/null
```
**期望**：引用 `已撤销` 且原因含「来源资源已下架」

---

# M5 企业 / 产教 / 有效期（负责人：模型 E）

### M5-0 开关前置（若未开）
```bash
A=$(tok - admin '1q2w3E*'); API=https://localhost:44305
for TEN in 3a23961d-ebad-94e2-5179-643fff1681b1 3a2034d1-6c43-2219-8149-45182659c849; do
  curl -sk -X PUT "$API/api/feature-management/features?providerName=T&providerKey=$TEN" -H "Authorization: Bearer $A" \
    -H 'Content-Type: application/json' -d '{"features":[
      {"name":"KnowledgeHub.LibraryReview","value":"true"},
      {"name":"KnowledgeHub.BuildTask","value":"true"},
      {"name":"KnowledgeHub.CrossSchoolRef","value":"true"},
      {"name":"KnowledgeHub.EnterpriseWorkspace","value":"true"},
      {"name":"KnowledgeHub.OrganizationValidity","value":"true"}]}' -o /dev/null -w "feat_$TEN=%{http_code}\n"
done
```

### M5-1 学校开通企业账号 → 自动建企业并绑定
```bash
G=$(tok guozhou guozhou-admin 123456)
U="m5-ent-$(date +%H%M%S)"
curl -sk -o /dev/null -w "create=%{http_code}\n" -X POST $API/api/app/tenant-user/user-for-tenant -H "Authorization: Bearer $G" -H "__tenant: guozhou" \
  -H 'Content-Type: application/json' -d "{\"userName\":\"$U\",\"emailAddress\":\"$U@demo.local\",\"password\":\"123456\",\"name\":\"M5\",\"surname\":\"HR\",\"isActive\":true,\"companyName\":\"M5企业-$U\",\"roleNames\":[\"EnterpriseUser\"]}"
EID=$(psql_q "SELECT m.\"EnterpriseProfileId\" FROM \"AppEnterpriseMembers\" m JOIN \"AbpUsers\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"UserName\"='$U'")
echo "EID=$EID"
```
**期望**：`create=200`；`EID` 非空（自动建企业 + 绑定）

### M5-2 未认证企业不能贡献内容
```bash
ET=$(tok guozhou "$U" 123456)
LIBG=$(psql_q "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"OwnerTenantId\"='3a23961d-ebad-94e2-5179-643fff1681b1' AND \"Status\"=1 LIMIT 1")
RESG=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='3a23961d-ebad-94e2-5179-643fff1681b1' AND \"IsDeleted\"=false LIMIT 1")
curl -sk -X POST $API/api/app/teaching-resource-library/$LIBG/attach -H "Authorization: Bearer $ET" -H "__tenant: guozhou" \
  -H 'Content-Type: application/json' -d "{\"contentType\":0,\"contentId\":\"$RESG\"}" | head -c 200; echo
```
**期望**：`企业尚未通过认证或已过有效期`

### M5-3 认证两级 → 贡献成功并标记 Enterprise
```bash
curl -sk -X POST $API/api/app/enterprise/$EID/submit-certification -H "Authorization: Bearer $ET" -H "__tenant: guozhou" -o /dev/null
ETK=$(curl -sk "$API/api/app/review-ticket?TargetId=$EID" -H "Authorization: Bearer $A" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
L=$(tok - league-admin 123456)
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$ETK\",\"decision\":0}" -o /dev/null
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $L" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$ETK\",\"decision\":0}" -o /dev/null
curl -sk -X POST $API/api/app/teaching-resource-library/$LIBG/attach -H "Authorization: Bearer $ET" -H "__tenant: guozhou" \
  -H 'Content-Type: application/json' -d "{\"contentType\":0,\"contentId\":\"$RESG\"}" -o /dev/null -w "attach=%{http_code}\n"
psql_q "SELECT \"SubmitterType\",\"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIBG' AND \"ContentId\"='$RESG' ORDER BY \"CreationTime\" DESC LIMIT 1"
```
**期望**：认证后 `attach=200`；`SubmitterType=1`（Enterprise）、`Status=1`（仍待审）

### M5-4 产教需求全链路
```bash
DID=$(curl -sk -X POST $API/api/app/cooperation/demand -H "Authorization: Bearer $ET" -H "__tenant: guozhou" -H 'Content-Type: application/json' \
  -d '{"type":1,"title":"M5-合作需求","scope":2,"region":"杭州","majors":"养老"}' | python3 -c 'import json,sys;print(json.load(sys.stdin).get("id",""))')
curl -sk -X POST $API/api/app/cooperation/$DID/submit-demand -H "Authorization: Bearer $ET" -H "__tenant: guozhou" -o /dev/null
DT=$(curl -sk "$API/api/app/review-ticket?TargetId=$DID" -H "Authorization: Bearer $A" | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$DT\",\"decision\":0}" -o /dev/null
curl -sk -X POST $API/api/app/review-ticket/decide -H "Authorization: Bearer $L" -H 'Content-Type: application/json' -d "{\"ticketId\":\"$DT\",\"decision\":0}" -o /dev/null
# 外校在大厅可见
Q=$(tok qidi qidi-admin 123456)
curl -sk "$API/api/app/cooperation/hall?MaxResultCount=50" -H "Authorization: Bearer $Q" -H "__tenant: qidi" \
  | python3 -c 'import json,sys;d=json.load(sys.stdin);print("hall=",d.get("totalCount"),"has_target=",any(x["id"]=="'"$DID"'" for x in d.get("items",[])))'
# 响应 → 企业接受 → 建项目 → 归档需验收意见
RID=$(curl -sk -X POST $API/api/app/cooperation/respond -H "Authorization: Bearer $Q" -H "__tenant: qidi" -H 'Content-Type: application/json' -d "{\"demandId\":\"$DID\",\"note\":\"M5响应\"}" | python3 -c 'import json,sys;print(json.load(sys.stdin).get("id",""))')
curl -sk -X POST $API/api/app/cooperation/decide-response/$RID -H "Authorization: Bearer $ET" -H "__tenant: guozhou" -H 'Content-Type: application/json' -d '{"accept":true}' -o /dev/null
PID=$(curl -sk -X POST $API/api/app/cooperation/project -H "Authorization: Bearer $ET" -H "__tenant: guozhou" -H 'Content-Type: application/json' -d "{\"responseId\":\"$RID\",\"name\":\"M5项目\"}" | python3 -c 'import json,sys;print(json.load(sys.stdin).get("id",""))')
curl -sk -X POST $API/api/app/cooperation/advance-stage -H "Authorization: Bearer $Q" -H "__tenant: qidi" -H 'Content-Type: application/json' -d "{\"projectId\":\"$PID\",\"stage\":3}" | head -c 160; echo
```
**期望**：大厅 `has_target=True`；响应/接受/建项目均成功；**归档缺验收意见被拒**（提示「归档前必须填写验收意见」）

### M5-5 单位有效期：过期禁写、读放行、续期恢复
```bash
GZ=3a23961d-ebad-94e2-5179-643fff1681b1
curl -sk -X POST $API/api/app/school-validity/set-validity -H "Authorization: Bearer $A" -H 'Content-Type: application/json' \
  -d "{\"tenantId\":\"$GZ\",\"validFrom\":\"2025-01-01T00:00:00Z\",\"validTo\":\"2025-06-30T00:00:00Z\",\"readOnlyAfterExpiry\":true}" -o /dev/null
G=$(tok guozhou guozhou-admin 123456)
curl -sk -o /dev/null -w "read=%{http_code}\n" "$API/api/app/resource-library" -H "Authorization: Bearer $G" -H "__tenant: guozhou"
curl -sk -o /dev/null -w "write=%{http_code}\n" -X POST $API/api/app/resource-library -H "Authorization: Bearer $G" -H "__tenant: guozhou" \
  -H 'Content-Type: application/json' -d '{"name":"M5-过期建库","visibility":0}'
# 续期
curl -sk -X POST $API/api/app/school-validity/set-validity -H "Authorization: Bearer $A" -H 'Content-Type: application/json' \
  -d "{\"tenantId\":\"$GZ\",\"validFrom\":null,\"validTo\":null,\"readOnlyAfterExpiry\":true}" -o /dev/null
curl -sk -o /dev/null -w "write_after_renew=%{http_code}\n" -X POST $API/api/app/resource-library -H "Authorization: Bearer $G" -H "__tenant: guozhou" \
  -H 'Content-Type: application/json' -d '{"name":"M5-续期建库","visibility":0}'
```
**期望**：`read=200` / `write=403`（含中文提示 `单位已过有效期`）/ `write_after_renew=200`
**必须**：测完确认已续期（`read` 恢复 200 且可写），否则会影响其它模型

---

# M6 权限 / AI / 检索治理（负责人：模型 F）

### M6-1 学生 AI 语料不含「私有库未分配」资源
```bash
S=$(tok qidi zmq 1234567); API=https://localhost:44305
QIDI=3a2034d1-6c43-2219-8149-45182659c849
before=$(curl -sk "$API/api/app/chat/resources-with-page-index" -H "Authorization: Bearer $S" -H "__tenant: qidi" | python3 -c 'import json,sys;print(len(json.load(sys.stdin)))')
LIB=$(psql_q "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"TenantId\"='$QIDI' AND \"Visibility\"=0 AND \"Status\"=1 LIMIT 1")
RES=$(psql_q "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$QIDI' AND \"Status\" IN (2,3) AND \"IsDeleted\"=false LIMIT 1")
psql_q "INSERT INTO \"AppResourcePublications\" (\"Id\",\"LibraryId\",\"ContentType\",\"ContentId\",\"ContentTenantId\",\"Status\",\"SubmitterType\",\"PublishedAt\",\"TenantId\",\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") VALUES (gen_random_uuid(),'$LIB',0,'$RES','$QIDI',2,0,now(),'$QIDI','{}','x',now(),false)" >/dev/null
after=$(curl -sk "$API/api/app/chat/resources-with-page-index" -H "Authorization: Bearer $S" -H "__tenant: qidi" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(len(d));import sys;print("has_target=",any(x["id"]=="'"$RES"'" for x in d),file=sys.stderr)')
echo "before=$before after=$after"
psql_q "DELETE FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB' AND \"ContentId\"='$RES'" >/dev/null
```
**期望**：`after = before - 1` 且 `has_target=False`；清理后恢复

### M6-2 检索默认只返回已审核
```bash
curl -sk -X POST $API/api/app/search/search -H 'Content-Type: application/json' -d '{"query":"测试","skipCount":0,"maxResultCount":5}' | head -c 200; echo
```
**期望**：不报错；结果中不含草稿/待审资源（需抽查 1~2 条资源的 `Status`）

### M6-3 越权直连一律 403
```bash
S=$(tok qidi zmq 1234567); W=$(tok qidi wwq 123456)
for c in "GET /api/app/review-ticket?MaxResultCount=5" "POST /api/app/build-task/task"; do
  m=${c%% *}; p=${c#* }
  curl -sk -o /dev/null -w "  student $m $p -> %{http_code}\n" -X $m "$API$p" -H "Authorization: Bearer $S" -H "__tenant: qidi" \
    -H 'Content-Type: application/json' -d '{"libraryId":"00000000-0000-0000-0000-000000000000","title":"x"}'
done
curl -sk -o /dev/null -w "  teacher delete library -> %{http_code}\n" -X DELETE "$API/api/app/resource-library/00000000-0000-0000-0000-000000000000" -H "Authorization: Bearer $W" -H "__tenant: qidi"
```
**期望**：全部 `403`（或 302）

### M6-4 策略与图标静态检查（无需运行）
```bash
cd /Users/bai/projects/KnowledgeHub
python3 - <<'PY'
import io, re
perms = io.open("src/KnowledgeHub.Domain.Shared/KnowledgeHubPermissions.cs", encoding="utf-8").read()
names=set()
for cls in re.finditer(r'public static class (\w+)\s*\{(.*?)\n    \}', perms, re.S):
    k,b=cls.group(1),cls.group(2)
    for m in re.finditer(r'public const string \w+ = (Default \+ "\.(\w+)"|GroupName \+ "\.(\w+)")', b):
        names.add(f"KnowledgeHub.{k}.{m.group(2) or m.group(3)}")
    if f'const string Default = GroupName + ".{k}"' in b: names.add(f"KnowledgeHub.{k}")
used=set()
for f in ["angular/src/app/app.routes.ts","angular/src/app/route.provider.ts","angular/src/app/alliance-route.provider.ts","angular/src/app/special-edu-route.provider.ts","angular/src/app/voice-assistant-route.provider.ts"]:
    try: used |= set(re.findall(r"requiredPolicy:\s*'([^']+)'", io.open(f, encoding="utf-8").read()))
    except FileNotFoundError: pass
missing=sorted(p for p in used if p.startswith("KnowledgeHub") and p not in names)
print("策略数:", len(used), "悬空:", len(missing), missing)
PY
```
**期望**：`悬空: 0`

---

# M7 前端 UI（负责人：模型 G，人工浏览器）

用 `qidi-admin`（机构：启迪学院111）与 `ent-demo`（机构：国州学院）分别登录。

| # | 页面 | 操作 | 期望 |
|---|------|------|------|
| U1 | `/admin/libraries` | 看列表 | 有「审核状态」列（8 态）+「提交审核/申请删除」 |
| U2 | `/admin/libraries/audits` | 点「查看」 | 弹窗含阶段步进、变更内容表、审核时间线；自审时显示「不能自审」 |
| U3 | `/admin/libraries/tasks` | 切「任务模板」Tab | 显示已发布库模板（如「通用专业教学资源库模板 v1」） |
| U4 | `/teaching/shared-refs` | 新建引用 | 选库→只列已发布资源→选课程；成功后列表出现「生效中」，统计卡 +1 |
| U5 | `/admin/employment/outcomes` | 点「核实」 | 「未核实」→「已核实」；`/admin/employment/statistics` 的「已核实去向」+1 |
| U6 | `/enterprise/workbench` | `ent-demo` 登录 | 直达企业工作台；侧边栏仅 6 组；显示企业名/认证状态 |
| U7 | `/admin/cooperation` | 切「企业账号」Tab | 企业列表 + 「开通企业账号」弹窗可用 |
| U8 | `/admin/school-validity` | `admin` 登录 | 各校有效期列表 + 设置弹窗 |
| U9 | 学生菜单 | `zmq` 登录 | **无任何管理端菜单**（审核中心/共建任务/企业工作台不可见） |
| U10 | 控制台 | 打开任意含图标页面 | 无 `IconNotFoundError` |

---

# 9. 报告模板（每个模型回填）

```markdown
## 模块：M<x> <名称>
环境：API health=200；令牌 7/7 成功
| 用例 | 结果 | 证据（关键返回/DB 查询） |
|------|------|------------------------|
| M1-1 | PASS | status=3 |
| M1-3 | PASS | code=KnowledgeHub:Review:CannotReviewOwn |
| ... | FAIL | 期望 403 实得 200：<返回片段> |
清理：已执行（说明清理了哪些表）
结论：PASS n / FAIL m；发现的问题：<描述+影响面>
```

---

# 10. 已知环境限制（先排除再判 BUG）

| 现象 | 原因 | 处理 |
|------|------|------|
| `health=000` | API 未启动 | `./dev.sh start api` |
| 索引重灌（`reindex-all-approved`）后索引仍无 `libraryId` | 演示资源文件无法解析 | 属环境限制，不是功能失败 |
| 岗位/需求创建后状态是草稿 | 需学校审核 `review-job` / 需求走两级 | 按用例步骤审核后再判定 |
| 企业账号首次贡献被拒 | 企业未认证（设计如此） | 先走认证两级 |
| 私有库教师看不到 | 需主建校 `assign-teacher` 分配（设计如此） | 先分配 |
