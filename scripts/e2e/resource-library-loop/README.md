# 教学资源库闭环 E2E 测试

一条命令跑通「教学资源库 → 课程 → 课程资源 → 章节资源 → 互转 → 准入审核 → 学生学习」全链路，
**本地与远程服务器同一套脚本**（全部连接信息走环境变量）。

```
scripts/e2e/resource-library-loop/
├── run.sh        # 主测试脚本（14 步，失败即退出并打印响应体）
├── preflight.sh  # 只读环境预检（API 可达 / 租户 / 四个账号 / 功能开关）
├── cleanup.sh    # 清理本次产生的测试数据
├── remote.sh     # 远程执行器（scp + ssh，本地看输出）
└── lib/
    ├── jqx.py    # 无依赖 JSON 查询助手
    └── mkpdf.py  # 生成结构合法的最小 PDF（当测试文件用）
```

---

## 1. 快速开始

### 本地

```bash
cd scripts/e2e/resource-library-loop
./preflight.sh          # 先预检（只读）
./run.sh                # 跑闭环
./cleanup.sh            # 清理本次产生的对象（可选）
```

### 远程服务器

```bash
# 远程 API 一般是 http://<IP>:44354（容器对外端口）
API_BASE=http://119.45.170.4:44354 \
TENANT_NAME=qidi \
ADMIN_USER=qidi-admin ADMIN_PASS=123456 \
TEACHER_USER=wwq   TEACHER_PASS=123456 \
STUDENT_USER=zmq   STUDENT_PASS=1234567 \
REVIEWER_USER=admin REVIEWER_PASS='1q2w3E*' \
./remote.sh 119.45.170.4 ubuntu
```

`remote.sh` 会把脚本 scp 到远程 `~/knowledgehub-e2e` 目录再执行，输出直接打在本地终端。

> 远程若没有宿主 `admin`（审核人），把 `REVIEWER_USER` / `REVIEWER_PASS` 换成租户内
> **第二个**院校管理员，并设置 `REVIEWER_TENANT=qidi`（原因见 §4「审核回避本人」）。

---

## 2. 环境变量

| 变量 | 默认值 | 说明 |
|---|---|---|
| `API_BASE` | `https://localhost:44305` | API 根地址（远程用 `http://IP:44354`） |
| `TENANT_NAME` | `qidi` | 租户 slug |
| `ADMIN_USER` / `ADMIN_PASS` | `qidi-admin` / `123456` | 院校管理员（建库、分配教师、资源校审） |
| `TEACHER_USER` / `TEACHER_PASS` | `wwq` / `123456` | 老师（建课程、上传资源、互转、发布） |
| `STUDENT_USER` / `STUDENT_PASS` | `zmq` / `1234567` | 学生（选课、看资源、下载、进度） |
| `REVIEWER_USER` / `REVIEWER_PASS` | `admin` / `1q2w3E*` | 审核人（默认 = 宿主 admin） |
| `REVIEWER_TENANT` | 空（= 宿主） | 审核人所在租户；填 `qidi` 表示租户内第二管理员 |
| `LIB_NAME` / `COURSE_TITLE` | `数据分析资源库` / `Python 数据分析` | 资源库名 / 课程名 |
| `RUN_TAG` | 时间戳 | 名称后缀，避免与历史数据混淆 |
| `CURL_SSL_ARGS` | `-k` | 传给 curl 的额外参数（自签证书） |
| `KEEP_TMP` | `0` | `1` = 保留临时目录便于排查 |

运行结果里的对象 Id 会写入 `.last-run-ids.json`（`cleanup.sh` 依赖它）。

---

## 3. 覆盖的链路（14 步）

| # | 步骤 | 关键接口 |
|---|---|---|
| 0 | 解析租户 | `GET /api/public/tenants` |
| 1 | 四账号登录 | `POST /connect/token`（password 授权） |
| 2 | 查老师 / 学生 Id | `GET /api/identity/users?filter=` |
| 3 | 建**私有**教学资源库 | `POST /api/app/resource-library` |
| 4 | 通过建库审核 | `POST /api/app/review-ticket/decide` |
| 5 | 把老师分配到库 | `POST /api/app/resource-library/{id}/assign-teacher` |
| 6 | 建课程并挂进库 | `POST /api/app/course` |
| 7 | 上传课程资源（真实文件）+ 关联课程 | `chunk-upload/{initiate,upload,complete}` → `POST /api/app/resource` → `POST /api/app/course-resource` |
| 8 | 建章节（含二级）+ 关联章节资源 + 章节知识点资源 | `POST /api/app/chapter` → `POST /api/app/chapter-resource` → `POST /api/app/knowledge-resource` |
| 9 | 资源提交审核 → 校审通过 | `POST /api/app/resource/submit-for-review/{id}` → `POST /api/app/resource/audit` |
| 10 | 课程资源 → 资源库资源（互转） | `POST /api/app/resource-conversion/convert-to-library` |
| 11 | 通过库准入审核 | `POST /api/app/review-ticket/decide` |
| 12 | 发布课程 + 老师导入学生 | `POST /api/app/course/{id}/publish` → `POST /api/app/student-course/enroll-student` |
| 13 | 学生侧验收 | `my-courses` / `course/{id}/detail` / `chapter-tree` / `course-resource` / `chapter-resource` / `resource-file/{id}/download` / `learning/record-progress` |
| 14 | 汇总 | 资源库统计 + 对象 Id 清单 |

脚本同时覆盖几条**反例断言**（不通过只告警、不中断）：

- 未关联课程的资源直接挂章节 → 被拒绝
- 同一资源重复关联课程 → 被拒绝
- 学生访问私有资源库 → 403（私有库语义）

以及一条**回归断言**（必须通过）：

- 删除「带知识点资源」的章节 → 204（曾经直接 500，见 §4.8）

---

## 4. 这套链路踩到的业务规则（回归时重点关注）

1. **审核「回避本人」**
   `ReviewWorkflow.DecideAsync` 硬校验「提交人不得审核自己提交的单」。
   → 建库人（院校管理员）**不能**审自己建的库。单管理员租户里必须由第二个人审；
   本脚本默认用宿主 `admin` 做「院校初审」。**这是最容易在远程环境卡住的一步。**

2. **库准入审核是独立于资源校审的第二道关**
   资源库开启 `KnowledgeHub.LibraryReview` 后：
   - 课程资源 → 资源库（互转）会生成一条 `ResourcePublication` 准入记录 + 审核单；
   - 审核通过后内容才真正进库（`publicationStatus = Published`），并写回资源主字段。

3. **私有库的学习闸门**
   `ResourceLibraryLearningPolicy`：
   - 校级私有库 → **仅课程老师导入（已有选课记录）的学员**可学；
   - 所以学生自助选课会返回「已经选修该课程」（老师已导入），这是预期行为而不是 bug。

4. **学生只看得到审核通过的资源**
   `CourseResourceAppService.GetByCourseAsync` 对非课程编辑者只返回
   `SchoolApproved / LeagueApproved` 的资源 → 资源必须先走校审。

5. **章节资源必须先关联课程**
   `ChapterResourceAppService.CreateAsync` 校验资源已在该课程的 `CourseResource` 里。

6. **建资源时带 `courseIds` 与「课程资源」接口是同一张表**
   `ResourceAppService.SyncResourceCoursesAsync` 写的就是 `CourseResource`，
   所以先带 `courseIds` 再调 `POST /api/app/course-resource` 会返回「该资源已关联到当前课程」。

7. **枚举值备忘**
   `ResourceLibraryVisibility` 0=私有 / 1=共享 / 2=共建；
   `ResourceLibraryStatus` 0=Draft / 1=Active / 2=Archived / 3=PendingReview；
   `ResourceStatus` 2=SchoolApproved；
   `CourseStatus` 0=Draft / 5=Published；
   `ResourcePublicationStatus` 2=Published。

8. **🐞 本次回归发现并已修复：删除「带知识点资源」的章节 500**
   - 现象：`DELETE /api/app/chapter/{id}` → HTTP 500，
     日志里是
     `23503: update or delete on table "AppChapters" violates foreign key constraint
      "FK_AppKnowledgeResources_AppChapters_ChapterId"`。
   - 根因：`KnowledgeResource` 是软删除实体（`ISoftDelete`），软删只置 `IsDeleted=true`、行还在；
     而 `AppKnowledgeResources.ChapterId → AppChapters` 的外键是 `NO ACTION`，
     所以只要章节下还留着任何一行知识点资源（哪怕已被软删），删章节就会被外键卡住。
     `ChapterAppService.DeleteByCourseAsync` 同理。
   - 修复（`src/KnowledgeHub.Application/Courses/ChapterAppService.cs`）：
     删章节前先把该章节（含子章节）下的知识点资源**软删并把 `ChapterId` 置空**
     （NULL 不参与外键校验）；已软删的行也要处理（临时 `DataFilter.Disable<ISoftDelete>()` 才查得到）。
     语义上也更合理：章节没了，它下面的知识点内容不该继续挂在课程里当孤儿。
   - 回归断言已写进 `run.sh` 第 8 步。

---

## 5. 清理

```bash
./cleanup.sh              # 清理最近一次 run.sh 的对象
./cleanup.sh --all        # 清理本目录下所有 .last-run-ids*.json 记录
./cleanup.sh --library <libId>
```

- 资源：`DELETE /api/app/resource/{id}`（硬删，顺带清课程/章节/资源库关联）
- 课程：`DELETE /api/app/course/{id}`（软删；**注意**：软删课程不会连带删章节，
  章节会变成孤儿，且因「只能修改自己创建的章节」规则，之后用接口也删不掉它们）
- 资源库：V2 语义是「归档」——`DELETE /api/app/resource-library/{id}` 提交归档申请，
  再由审核人通过对应审核单。
  ⚠️ 归档的库**没有硬删接口**，会保留在管理端列表里（状态 = 已归档）；
  反复跑测试会堆积，需要彻底清除时由管理员手工处理（或直接清库）。

---

## 6. 依赖

- `bash`、`curl`、`python3`（仅用标准库）、常规 coreutils
- 远程服务器同样满足（Ubuntu 自带 python3 / curl）
- 无需数据库直连、无需 jq
