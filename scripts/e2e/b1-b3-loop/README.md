# B1–B3（+ P2 第一批）整体回归 E2E

课程教学改造（P0 + P1 的 B1/B2/B3 三批）的端到端回归脚本。
**一条命令验证 11 个环节**，从「老师建课」一路跑到「打印 PDF」。

```bash
cd scripts/e2e/b1-b3-loop

./preflight.sh          # 只读预检：API / 租户 / 三账号 / 权限 / 控制器 / Gotenberg / ffmpeg
./run.sh                # 跑完整链路（默认保留数据，写 .last-run）
./verify-db.sh          # 数据库侧独立交叉验证（32 项，不依赖 run.sh 的断言）
./cleanup.sh            # 清理本次产物（API 删除 + SQL 硬删）
KEEP=1 ./run.sh         # 同上（保留数据便于人工核对）
AUTO_CLEANUP=1 ./run.sh # 跑完自动清理
SKIP_VIDEO=1 ./run.sh   # 没有 ffmpeg 时跳过视频上传/流式播放
```

远程（iketong / 正式服）：

```bash
API_BASE=http://47.99.245.49:44354 ./run.sh
API_BASE=http://47.99.245.49:44354 PGHOST=47.99.245.49 PGPORT=5433 ./verify-db.sh
PGHOST=47.99.245.49 PSQL_IN_CONTAINER=1 ./verify-db.sh   # 宿主没装 psql 时走容器
```

## 覆盖的 11 个环节

| 批次 | 环节 | 验的关键点 |
|---|---|---|
| **B1** | 1. 老师建课 → 发布 | 课程状态到 `Published(5)` |
| B1 | 2. 课程教学信息 | `hasContent`、多行文本换行保留 |
| B1 | 3. 教材 | 主教材 `isPrimary` 唯一 |
| B1 | 4. 知识点树 | `nodeType`（0 单元/1 知识项）、**创建即维护 `sortPath`**、上移后同级顺序 |
| B1 | 5. 掌握度 | 习题绑知识点 → 学生答对 → `hasEvidence=true`、Beta 先验（1 题 → 56% 而不是 100%） |
| **B2** | 6. 签到 | 匿名解析**不含名单字段**、重复签到幂等（`isNew=false`）、名单人数 |
| B2 | 7. 讨论答疑 | 学生提问 → **老师回复**（不要求选课）→ 采纳 → 已解决 |
| B2 | 8. 课堂练习 | 快照题数、错答→按正确答案**覆盖提交**、教师统计（1 份 attempt） |
| B2 | 9. 小组任务 | 随机分组、一组一份提交、评分 |
| **B3** | 10. 课程视频 | 上传、**Range 播放 206 + accept-ranges**、匿名 401、同秒打点更新、时长回填 |
| B3 | 11. 打印 | 试卷 PDF / 成绩单 PDF 均校验 `%PDF` 魔数 |

`verify-db.sh` 另外核对 37 条**数据库事实**：`SortPath` 无空值、掌握度证据、签到无重复组、
讨论采纳、练习「快照题数=场次题数」「作答明细=题数」、小组「同组多份提交=0」、
视频「同秒重复标记=0」、试卷题目数、学生行为日志条数、能力「课程支撑数 / 关联知识点数 / 支撑强度 / 证据知识点数」、导入「模板节点数 / SortPath 无空」、岗位「图谱数 / 任务数 / 任务能力数 / 要求等级」。

## 默认账号 / 参数

| 变量 | 默认 | 说明 |
|---|---|---|
| `API_BASE` | `https://localhost:44305` | |
| `TENANT_NAME` | `qidi` | 租户 |
| `TEACHER_USER` / `PASS` | `wwq` / `123456` | 建课、建知识点、出题、发起签到等 |
| `STUDENT_USER` / `PASS` | `zmq` / `1234567` | 签到、作答、提交、提问 |
| `ADMIN_USER` / `PASS` | `admin` / `1q2w3E*` | 预留（宿主审核） |
| `RUN_TAG` | 时间戳 | 课程标题 = `B1B3回归课程-<tag>`，便于定位与清理 |
| `GOTENBERG_URL` | `http://localhost:3000` | 打印依赖 |

## P2 相关注意

- **能力达成度只统计「有证据」的知识点**：无证据的不按 0 分拉低，所以 `knowledgeCount` 与
  `EvidencedKnowledgeCount` 可能不同，这是设计而非 bug。
- **知识点映射的自动写库门槛是 0.80**：低于门槛只给建议（避免映射错误污染达成度）。
- **发布课程必须带请求体**：`POST /course/{id}/publish` 空 body 会 400；脚本 `api()` 已统一补 `{}`。

## 踩过的坑（脚本里已固化）

| 坑 | 现象 | 处理 |
|---|---|---|
| 租户头名字 | 用 `Abp-Tenant-Id` 登录直接 `invalid_grant` | 必须是 **`Abp-TenantId`**（无第二个连字符） |
| 无 body 的 POST | `POST /course/{id}/publish` 返回 **415** | 统一带 `Content-Type: application/json` + `{}` |
| ABP 配置结构 | `.user.id` 取不到 | 当前用户字段是 **`currentUser`** |
| Python 布尔 | `hasEvidence` 打印 `True` 与 bash 的 `true` 不等 | SQL/JSON 侧统一转小写 |
| `$VAR` 紧跟中文 | `set -u` 报 `PRACTICE_Q（: unbound variable` | 一律写 `${VAR}` |
| 表名猜测 | `AppChapterExercise` / `AppLearningProgresses` 不存在 | 实际是 `AppChapterExercises` / `AppLearningProgress` |
| 软删表差异 | `AppVideoKnowledgeMarks` 没有 `IsDeleted` 列 | 该表是普通 Entity，不要带软删过滤 |

## 回归时发现的真实问题（已修）

1. **知识点新建后 `SortPath` 为空** —— `RebuildPaths` 会重新查库，同 UoW 内未落库的新节点查不到。
   已加 `SaveChangesAsync()`（`KnowledgeResourceAppService.RebuildPathsAsync`）。
2. **老师无法回复讨论** —— `ReplyAsync` 要求"已选课"，把老师挡在自己建的答疑区外。
   新增 `ClassroomSupport.EnsureCanParticipateAsync`：本课教师 / 有 `Courses.Edit` 直接放行，
   其余仍要求选课（签到 / 练习 / 小组任务保持严格，因为它们写"学生记录"）。
