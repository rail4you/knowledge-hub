#!/usr/bin/env python3
"""
沈阳工业大学演示租户种子脚本（远程环境幂等执行）。

做的事：
  1. 建租户 syut（学校档案名 = 沈阳工业大学，登录页机构下拉显示中文）
  2. 租户管理员 syut-admin（把 ABP 自动建的管理员改名成这个）
  3. 教师：白晨生 / 宋罡（Teacher 角色）
  4. 示范学生：张明 / 李静（Student 角色）
  5. 两门课（用各自老师的账号创建，TeacherId 才是对应老师）：
       Python 程序设计与应用 —— 白晨生
       市场营销学           —— 宋罡
     每个课程建 8~10 章（含章描述）并发布
  6. 两名示范学生选上这两门课

幂等：已存在的租户 / 用户 / 课程 / 章节 / 选课都会跳过，可反复执行。

用法：
  python3 scripts/iketong-syut-seed.py                 # 全量（默认远程 iketong）
  python3 scripts/iketong-syut-seed.py --dry-run        # 只打印将要做的事
  KH_BASE=http://localhost:44305 python3 scripts/iketong-syut-seed.py   # 打本地
"""

import argparse
import base64
import json
import os
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("KH_BASE", "https://www.iketong.com")

# ── 目标学校 ─────────────────────────────────────────────
TENANT_SLUG = os.environ.get("KH_TENANT", "syut")
SCHOOL_NAME = os.environ.get("KH_SCHOOL_NAME", "沈阳工业大学")
TENANT_ADMIN = os.environ.get("KH_TENANT_ADMIN", "syut-admin")
PASSWORD = os.environ.get("KH_PASSWORD", "123456")

HOST_ADMIN = os.environ.get("KH_HOST_ADMIN", "admin")
HOST_ADMIN_PASS = os.environ.get("KH_HOST_ADMIN_PASS", "1q2w3E*")

# ── 教师 / 学生 ──────────────────────────────────────────
TEACHERS = [
    {"userName": "baichensheng", "name": "白晨生", "course": "python"},
    {"userName": "songgang", "name": "宋罡", "course": "marketing"},
]
STUDENTS = [
    {"userName": "syut-student1", "name": "张明"},
    {"userName": "syut-student2", "name": "李静"},
]

# ── 课程数据 ─────────────────────────────────────────────
COURSES = {
    "python": {
        "title": "Python 程序设计与应用",
        "description": (
            "面向零基础学生的 Python 入门与工程实践课程。以「装好环境 → 写会程序 → 做出小应用」为主线，"
            "覆盖语法基础、流程控制、函数与模块、数据结构、文件与异常、面向对象，"
            "并完成数据分析与可视化、自动化办公两个综合案例。"
        ),
        "semester": "2026-2027-1",
        "credits": 3,
        "semesterHours": 48,
        "difficulty": 1,
        "chapters": [
            ("第 1 章 Python 与开发环境", "课程导览、Python 是什么、安装与虚拟环境、IDLE 与 VS Code、第一个程序与调试入门。"),
            ("第 2 章 变量、数据类型与运算符", "变量与命名规范、数字与字符串、布尔值、类型转换、输入输出、常见运算符与优先级。"),
            ("第 3 章 流程控制", "if / elif / else 分支、while 与 for 循环、range 与遍历、循环中的 break / continue、嵌套与调试技巧。"),
            ("第 4 章 容器类型", "列表的增删改查与切片、元组、字典的键值操作、集合与去重、列表推导式、容器选择的经验法则。"),
            ("第 5 章 函数与模块", "函数定义与参数（位置 / 关键字 / 默认 / 可变）、返回值与作用域、递归入门、模块与包的导入、标准库速查。"),
            ("第 6 章 文件与异常处理", "文本与 CSV 读写、with 语句、路径操作、异常捕获与自定义异常、日志初体验。"),
            ("第 7 章 面向对象基础", "类与对象、属性与方法、构造与 self、封装与继承、常见设计误区、用类重构一个成绩管理小工具。"),
            ("第 8 章 第三方库与数据分析入门", "pip 与依赖管理、NumPy 数组运算、pandas 数据清洗与统计、matplotlib 绘图。"),
            ("第 9 章 综合案例：班级成绩分析", "读取成绩表 → 清洗缺失值 → 统计排名与分布 → 输出图表与结论报告。"),
            ("第 10 章 综合案例：自动化办公脚本", "批量重命名与归档文件、Excel 批量汇总、生成周报文本、把脚本打包成可复用工具。"),
        ],
    },
    "marketing": {
        "title": "市场营销学",
        "description": (
            "以「理解市场 → 找准客群 → 设计组合 → 落地增长」为逻辑主线的市场营销核心课程，"
            "覆盖市场调研、STP 战略、4P/4C 组合、品牌与内容营销、数字渠道与私域运营、"
            "销售管理与营销效果评估，并配套两个完整案例（校园新消费品牌、传统制造企业转型）。"
        ),
        "semester": "2026-2027-1",
        "credits": 3,
        "semesterHours": 48,
        "difficulty": 2,
        "chapters": [
            ("第 1 章 市场营销导论", "营销的定义与边界、需要/需求/欲望、营销观念演变、营销在组织中的位置、课程地图与考核方式。"),
            ("第 2 章 市场环境与消费者行为", "宏观与微观环境分析、消费者决策五阶段、影响购买的因素、B2B 与 B2C 差异、用户访谈实操。"),
            ("第 3 章 市场调研与数据洞察", "调研设计（定性 / 定量）、问卷与访谈、抽样与样本量、数据清洗与交叉分析、从数据到洞察的表达。"),
            ("第 4 章 市场细分、目标市场与定位（STP）", "细分维度与有效性标准、目标市场选择策略、定位地图与差异化主张、定位落地到传播语。"),
            ("第 5 章 产品与品牌策略", "产品整体概念与层次、产品组合与生命周期、新产品开发流程、品牌资产与品牌架构、命名与包装。"),
            ("第 6 章 定价策略", "成本/需求/竞争三种导向、价格弹性、常见定价战术（渗透 / 撇脂 / 组合 / 心理定价）、促销与价格战应对。"),
            ("第 7 章 渠道与供应链协同", "渠道结构与中间商职能、线上线下融合、渠道冲突与治理、库存与履约体验、渠道绩效评估。"),
            ("第 8 章 整合营销传播与内容营销", "传播模型与信息设计、广告/公关/促销/人员推销组合、内容与社媒运营、KOL 合作与效果衡量。"),
            ("第 9 章 数字营销与私域增长", "搜索与信息流、短视频与直播电商、私域社群与会员体系、AARRR 漏斗与增长实验、数据埋点与归因。"),
            ("第 10 章 营销管理与综合案例", "营销计划书结构、预算与 ROI、营销组织与协同、案例复盘（校园新消费品牌 / 传统制造企业转型）。"),
        ],
    },
}

SSL_CTX = ssl.create_default_context()
SSL_CTX.check_hostname = False
SSL_CTX.verify_mode = ssl.CERT_NONE
urllib.request.install_opener(
    urllib.request.build_opener(urllib.request.HTTPSHandler(context=SSL_CTX))
)

TENANT_ID = None
DRY_RUN = False


# ════════════════════════════ HTTP ════════════════════════════

def http(method, path, body=None, token=None, tenant_id=None, timeout=120):
    data = None
    headers = {"Accept": "application/json"}
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if tenant_id:
        headers["__tenant"] = tenant_id

    req = urllib.request.Request(f"{BASE}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            text = resp.read().decode("utf-8", "replace")
            return resp.status, (json.loads(text) if text.strip() else None)
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(text)
        except Exception:
            return e.code, {"raw": text[:400]}


def die(msg, res=None):
    print(f"✗ {msg}")
    if res is not None:
        print(json.dumps(res, ensure_ascii=False)[:1500] if isinstance(res, (dict, list)) else str(res)[:1500])
    sys.exit(1)


def ok(msg):
    print(f"✓ {msg}")


def info(msg):
    print(f"  {msg}")


def err_text(res):
    if isinstance(res, dict):
        e = res.get("error") or {}
        return e.get("message") or res.get("raw") or json.dumps(res, ensure_ascii=False)[:200]
    return str(res)[:200]


# ════════════════════════ 登录 ════════════════════════

def login(username, password, tenant_id=None):
    form = urllib.parse.urlencode(
        {
            "grant_type": "password",
            "username": username,
            "password": password,
            "client_id": "KnowledgeHub_App",
            "scope": "offline_access KnowledgeHub",
        }
    ).encode()
    headers = {"Content-Type": "application/x-www-form-urlencoded"}
    if tenant_id:
        headers["__tenant"] = tenant_id
    req = urllib.request.Request(f"{BASE}/connect/token", data=form, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            payload = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        die(f"登录失败 {username}: {e.read().decode('utf-8', 'replace')[:300]}")
    token = payload["access_token"]
    body = token.split(".")[1]
    body += "=" * (-len(body) % 4)
    return token, json.loads(base64.urlsafe_b64decode(body))


# ════════════════════════ 租户 ════════════════════════

def ensure_tenant(host_token):
    global TENANT_ID
    code, res = http("GET", f"/api/multi-tenancy/tenants?filter={TENANT_SLUG}&maxResultCount=50", token=host_token)
    if code != 200:
        die("查询租户失败", res)
    for item in res.get("items", []):
        if item["name"].lower() == TENANT_SLUG.lower():
            TENANT_ID = item["id"]
            ok(f"租户已存在：{item['name']} = {TENANT_ID}")
            return

    body = {
        "name": TENANT_SLUG,
        "adminEmailAddress": f"{TENANT_ADMIN}@{TENANT_SLUG}.local",
        "adminPassword": PASSWORD,
    }
    if DRY_RUN:
        ok(f"[dry-run] 将创建租户 {TENANT_SLUG}（管理员 {TENANT_ADMIN}）")
        return
    code, res = http("POST", "/api/multi-tenancy/tenants", body, token=host_token)
    if code not in (200, 201):
        die("创建租户失败", res)
    TENANT_ID = res["id"]
    ok(f"创建租户 {TENANT_SLUG} = {TENANT_ID}（自动建管理员 {body['adminEmailAddress']}）")


def ensure_school_name(host_token):
    """学校档案名决定登录页机构下拉的显示名（中文）。"""
    if not TENANT_ID:
        return
    body = {"name": SCHOOL_NAME}
    if DRY_RUN:
        ok(f"[dry-run] 将把学校档案名设为 {SCHOOL_NAME}")
        return
    code, res = http("POST", f"/api/app/school/save-by-tenant-id/{TENANT_ID}", body, token=host_token)
    if code not in (200, 201):
        die("设置学校档案失败", res)
    ok(f"学校档案名：{res.get('name', SCHOOL_NAME)}")


def ensure_tenant_admin(host_token):
    """
    租户管理员。

    ⚠️ ABP 10 的 `POST /api/multi-tenancy/tenants` **不再自动建管理员用户**（只播种角色），
    所以这里用项目自带的 `tenant-user/user-for-tenant` 接口补建：
    该接口内部 `CurrentTenant.Change(input.TenantId)`，宿主管理员可给任意租户建用户。

    注意两点：
    1) 宿主管理员令牌的租户上下文恒为 null（CurrentUser 解析器先命中），
       `__tenant` / `Abp-TenantId` 头对普通接口不生效 —— 建用户必须走这个带 TenantId 的接口，
       不能直接打 `/api/identity/users`。
    2) 新租户只会被播下 Teacher / Student / SchoolAdmin / LeagueAdmin / EnterpriseUser 五个角色，
       **没有 ABP 内置的 `admin` 角色**（它随被移除的管理员一起消失了）。
       本产品的「租户管理员」就是 `SchoolAdmin`（院校管理员，权限覆盖用户/课程/就业等），
       所以这里给 SchoolAdmin。
    """
    if not TENANT_ID:
        return

    body = {
        "tenantId": TENANT_ID,
        "userName": TENANT_ADMIN,
        "emailAddress": f"{TENANT_ADMIN}@{TENANT_SLUG}.local",
        "password": PASSWORD,
        "name": "院校管理员",
        "surname": "",
        "isActive": True,
        "roleNames": ["SchoolAdmin"],
    }
    if DRY_RUN:
        ok(f"[dry-run] 将创建租户管理员 {TENANT_ADMIN}")
        return

    code, res = http("POST", "/api/app/tenant-user/user-for-tenant", body, token=host_token)
    if code in (200, 201):
        ok(f"创建租户管理员 {TENANT_ADMIN} = {res.get('id')}")
        return
    msg = err_text(res)
    if "already" in msg.lower() or "exist" in msg.lower() or code == 409:
        ok(f"租户管理员已存在：{TENANT_ADMIN}")
        return
    die("创建租户管理员失败", res)


# ════════════════════════ 用户 ════════════════════════

def find_user(token, user_name):
    code, res = http(
        "GET", f"/api/identity/users/by-username/{urllib.parse.quote(user_name)}", token=token
    )
    if code == 200 and isinstance(res, dict) and res.get("id"):
        return res
    return None


def ensure_user(token, user_name, display_name, role):
    existing = find_user(token, user_name)
    if existing:
        code, roles = http("GET", f"/api/identity/users/{existing['id']}/roles", token=token)
        names = [r["name"] for r in roles] if code == 200 and isinstance(roles, list) else []
        if role not in names:
            if DRY_RUN:
                ok(f"[dry-run] 将给 {user_name} 补角色 {role}")
            else:
                code, res = http(
                    "PUT", f"/api/identity/users/{existing['id']}/roles",
                    {"roleNames": names + [role]}, token=token,
                )
                if code not in (200, 204):
                    die(f"给 {user_name} 补角色失败", res)
                ok(f"{user_name} 补角色 {role}")
        else:
            ok(f"用户已存在：{user_name}（{role}）")
        return existing["id"]

    if DRY_RUN:
        ok(f"[dry-run] 将创建用户 {user_name}（{display_name}，{role}）")
        return None

    body = {
        "userName": user_name,
        "emailAddress": f"{user_name}@{TENANT_SLUG}.local",
        "name": display_name,
        "surname": "",
        "password": PASSWORD,
        "isActive": True,
        "roleNames": [role],
    }
    code, res = http("POST", "/api/app/tenant-user/user-for-tenant", body, token=token)
    if code not in (200, 201):
        die(f"创建用户 {user_name} 失败", res)
    ok(f"创建用户 {user_name} = {res['id']}（{display_name}，{role}）")
    return res["id"]


# ════════════════════════ 课程 ════════════════════════

def my_courses(token):
    code, res = http("GET", "/api/app/course?skipCount=0&maxResultCount=200&onlyMine=true", token=token)
    if code != 200 or not isinstance(res, dict):
        die("查询我的课程失败", res)
    return res.get("items", [])


def ensure_course(teacher_token, spec):
    for c in my_courses(teacher_token):
        if c["title"] == spec["title"]:
            ok(f"课程已存在：{spec['title']} = {c['id']}（{c.get('status')}）")
            return c["id"], c

    body = {
        "title": spec["title"],
        "description": spec["description"],
        "semester": spec["semester"],
        "credits": spec["credits"],
        "semesterHours": spec["semesterHours"],
        "difficulty": spec["difficulty"],
        "majorIds": [],
        "libraryIds": [],
    }
    if DRY_RUN:
        ok(f"[dry-run] 将创建课程《{spec['title']}》")
        return None, None

    code, res = http("POST", "/api/app/course", body, token=teacher_token)
    if code not in (200, 201):
        die(f"创建课程《{spec['title']}》失败", res)
    ok(f"创建课程《{spec['title']}》= {res['id']}")
    return res["id"], res


def ensure_chapters(teacher_token, course_id, spec):
    code, existing = http("GET", f"/api/app/chapter/chapters-by-course/{course_id}", token=teacher_token)
    if code != 200 or not isinstance(existing, list):
        die("查询章节失败", existing)
    if existing:
        ok(f"章节已存在（{len(existing)} 章），跳过")
        return

    for idx, (title, desc) in enumerate(spec["chapters"], start=1):
        if DRY_RUN:
            info(f"[dry-run] 第 {idx} 章 {title}")
            continue
        code, res = http(
            "POST", "/api/app/chapter",
            {"courseId": course_id, "title": title, "description": desc, "sortOrder": idx},
            token=teacher_token,
        )
        if code not in (200, 201):
            die(f"创建章节《{title}》失败", res)
    if not DRY_RUN:
        ok(f"创建 {len(spec['chapters'])} 章")


def ensure_published(teacher_token, course_id):
    code, res = http("POST", f"/api/app/course/{course_id}/publish", {"isShared": False}, token=teacher_token)
    if code not in (200, 201, 204):
        die("发布课程失败", res)
    ok(f"课程已发布（status={res.get('status') if isinstance(res, dict) else '?'}）")


# ════════════════════════ 选课 ════════════════════════

def ensure_enrolled(student_token, course_id, label):
    code, mine = http("GET", "/api/app/student-course?skipCount=0&maxResultCount=200", token=student_token)
    enrolled = set()
    if code == 200 and isinstance(mine, dict):
        for item in mine.get("items", []):
            cid = item.get("courseId") or (item.get("course") or {}).get("id")
            if cid:
                enrolled.add(cid)
    if course_id in enrolled:
        ok(f"{label} 已选课")
        return
    if DRY_RUN:
        ok(f"[dry-run] {label} 将选课")
        return
    code, res = http("POST", f"/api/app/course/enroll/{course_id}", token=student_token)
    if code not in (200, 201, 204):
        die(f"{label} 选课失败", res)
    ok(f"{label} 已选课")


# ════════════════════════ 主流程 ════════════════════════

def main():
    global DRY_RUN
    parser = argparse.ArgumentParser(description="沈阳工业大学演示租户种子数据")
    parser.add_argument("--dry-run", action="store_true", help="只打印计划，不写数据")
    args = parser.parse_args()
    DRY_RUN = args.dry_run

    print(f"环境：{BASE}{'（dry-run）' if DRY_RUN else ''}")
    host_token, _ = login(HOST_ADMIN, HOST_ADMIN_PASS)
    ok("宿主管理员登录成功")

    ensure_tenant(host_token)
    ensure_school_name(host_token)
    ensure_tenant_admin(host_token)
    if DRY_RUN and not TENANT_ID:
        print("\n[dry-run] 租户尚不存在，后续用户 / 课程创建跳过")
        return

    admin_token, _ = login(TENANT_ADMIN, PASSWORD, tenant_id=TENANT_ID)
    ok(f"租户管理员登录成功（{TENANT_ADMIN}）")

    for t in TEACHERS:
        ensure_user(admin_token, t["userName"], t["name"], "Teacher")
    for s in STUDENTS:
        ensure_user(admin_token, s["userName"], s["name"], "Student")

    course_ids = {}
    for t in TEACHERS:
        print(f"\n—— 教师：{t['name']}（{t['userName']}）")
        teacher_token, _ = login(t["userName"], PASSWORD, tenant_id=TENANT_ID)
        spec = COURSES[t["course"]]
        course_id, _ = ensure_course(teacher_token, spec)
        if course_id:
            ensure_chapters(teacher_token, course_id, spec)
            ensure_published(teacher_token, course_id)
            course_ids[t["course"]] = course_id

    print("\n—— 选课")
    for s in STUDENTS:
        student_token, _ = login(s["userName"], PASSWORD, tenant_id=TENANT_ID)
        for key, cid in course_ids.items():
            ensure_enrolled(student_token, cid, f"{s['name']}《{COURSES[key]['title']}》")

    print("\n汇总")
    print(f"  租户：{SCHOOL_NAME}（{TENANT_SLUG}）= {TENANT_ID}")
    print(f"  管理员：{TENANT_ADMIN} / {PASSWORD}")
    for t in TEACHERS:
        print(f"  教师：{t['name']}（{t['userName']} / {PASSWORD}）→ {COURSES[t['course']]['title']}")
    for s in STUDENTS:
        print(f"  学生：{s['name']}（{s['userName']} / {PASSWORD}）")
    ok("完成")


if __name__ == "__main__":
    main()
