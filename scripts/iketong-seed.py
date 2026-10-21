#!/usr/bin/env python3
"""
iketong 生产环境（https://www.iketong.com，租户 qidi）演示数据脚本。

账号：
  - 教师端：wwq / 123456（Teacher 角色，用于智学分析全部操作）
  - 租户管理员：qidi-admin / 123456（SchoolAdmin，用于建学生 + 选课）
  - 学生：zmq / 123456，以及 demo1 / demo2 / demo3（密码 123456）

子命令：
  inspect   查看课程 / 选课 / 题库规模
  users     创建 demo1..3 并选上与 zmq 相同的两门课
  linkage   设置两门课程的前后关系（前置 → 后续）
  kp        为两门课程生成知识点
  paper     逐名学生出卷（HTML → JPG）→ 上传 → 自动 OCR
  ocr       重新触发并等待 OCR 完成
  confirm   把 OCR 结果折算成 100 分制并确认
  push      推送个人薄弱点报告
  collect   导出真实分析结果 JSON（写文档用）

用法：
  python3 scripts/iketong-seed.py inspect
  python3 scripts/iketong-seed.py users  --courses A,B
  python3 scripts/iketong-seed.py linkage --courses A,B
  python3 scripts/iketong-seed.py kp     --courses A,B
  python3 scripts/iketong-seed.py paper  --courses A,B --students zmq,demo1,demo2,demo3
  python3 scripts/iketong-seed.py confirm
  python3 scripts/iketong-seed.py push
  python3 scripts/iketong-seed.py collect
"""

import argparse
import base64
import json
import os
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = os.environ.get("KH_BASE", "https://www.iketong.com")
TENANT_NAME = os.environ.get("KH_TENANT", "qidi")
TEACHER_USER = os.environ.get("KH_TEACHER", "wwq")
TEACHER_PASS = os.environ.get("KH_TEACHER_PASS", "123456")
ADMIN_USER = os.environ.get("KH_ADMIN", "qidi-admin")
ADMIN_PASS = os.environ.get("KH_ADMIN_PASS", "123456")
STUDENT_PASS = os.environ.get("KH_STUDENT_PASS", "123456")
CLASS_NAME = os.environ.get("KH_CLASS", "中医体重管理 2301 班")
WORK = os.environ.get("KH_WORK", "/tmp/iketong-seed")

TENANT_ID = None
_STUDENT_INDEX = None

SSL_CTX = ssl.create_default_context()
SSL_CTX.check_hostname = False
SSL_CTX.verify_mode = ssl.CERT_NONE
urllib.request.install_opener(urllib.request.build_opener(urllib.request.HTTPSHandler(context=SSL_CTX)))

OPEN_ENDED_TYPES = {4, 5, 6, 7, 8}  # 简答/论述/案例分析/名词解释/计算：卷面分值翻倍


# ════════════════════════════ HTTP ════════════════════════════

def http(method, path, body=None, token=None, timeout=300):
    data = None
    headers = {"Accept": "application/json"}
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if TENANT_ID:
        headers["__tenant"] = TENANT_ID

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
        print(json.dumps(res, ensure_ascii=False)[:1200] if isinstance(res, (dict, list)) else str(res)[:1200])
    sys.exit(1)


def ok(msg):
    print(f"✓ {msg}")


# ════════════════════════ 登录 / 上下文 ════════════════════════

def resolve_tenant():
    global TENANT_ID
    code, res = http("GET", f"/api/abp/multi-tenancy/tenants/by-name/{urllib.parse.quote(TENANT_NAME)}")
    if code != 200 or not isinstance(res, dict):
        die(f"找不到租户 {TENANT_NAME}", res)
    TENANT_ID = res.get("tenantId") or res.get("id")
    ok(f"租户 {res.get('name')} = {TENANT_ID}")


def login(username, password):
    form = urllib.parse.urlencode(
        {
            "grant_type": "password",
            "username": username,
            "password": password,
            "client_id": "KnowledgeHub_App",
            "scope": "offline_access KnowledgeHub",
        }
    ).encode()
    req = urllib.request.Request(
        f"{BASE}/connect/token",
        data=form,
        headers={"Content-Type": "application/x-www-form-urlencoded", "__tenant": TENANT_ID},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            payload = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        die(f"登录失败 {username}: {e.read().decode('utf-8', 'replace')[:300]}")
    token = payload["access_token"]
    body = token.split(".")[1]
    body += "=" * (-len(body) % 4)
    return token, json.loads(base64.urlsafe_b64decode(body))


def ctx():
    """教师上下文（wwq）：智学分析的全部操作都用它。"""
    resolve_tenant()
    token, claims = login(TEACHER_USER, TEACHER_PASS)
    ok(f"教师登录：{claims.get('preferred_username')}")
    return token, claims


# ════════════════════════ 查询 ════════════════════════

def list_courses(token):
    code, res = http("GET", "/api/app/course?skipCount=0&maxResultCount=200", token=token)
    if code != 200 or not isinstance(res, dict):
        die("取课程列表失败", res)
    return res.get("items", [])


def student_index(token):
    """扫所有课程的选课名单：用户名 → {id, name, studentNumber, courses[]}。教师权限即可。"""
    global _STUDENT_INDEX
    if _STUDENT_INDEX is not None:
        return _STUDENT_INDEX

    index = {}
    for course in list_courses(token):
        code, students = http("GET", f"/api/app/learning-analytics/course-students/{course['id']}", token=token)
        if code != 200 or not isinstance(students, list):
            continue
        for st in students:
            key = (st.get("userName") or "").lower()
            if not key:
                continue
            entry = index.setdefault(
                key,
                {
                    "id": st["studentId"],
                    "userName": st.get("userName"),
                    "name": st.get("studentName"),
                    "studentNumber": st.get("studentNumber"),
                    "courses": [],
                },
            )
            entry["courses"].append(
                {"courseId": course["id"], "courseTitle": course.get("title"), "status": st.get("status")}
            )
    _STUDENT_INDEX = index
    return index


def find_user(token, username):
    entry = student_index(token).get(username.lower())
    if entry:
        return entry
    code, res = http("GET", f"/api/app/tenant-user?filter={urllib.parse.quote(username)}&maxResultCount=200", token=token)
    if code == 200 and isinstance(res, dict):
        for item in res.get("items", []):
            if (item.get("userName") or "").lower() == username.lower():
                return {
                    "id": item["id"],
                    "userName": item.get("userName"),
                    "name": item.get("name"),
                    "studentNumber": item.get("studentNumber"),
                    "courses": [],
                }
    return None


def course_exercises(token, course_id):
    code, res = http("GET", f"/api/app/exercise/by-course/{course_id}", token=token)
    return res if (code == 200 and isinstance(res, list)) else []


def pick_paper_shape(token, course_id):
    """让卷面满分正好 100：先试全量题库，不行再只用客观题凑整除数。"""
    exercises = course_exercises(token, course_id)
    if not exercises:
        return 0, 0
    n = len(exercises)
    n_subj = sum(1 for e in exercises if e.get("type") in OPEN_ENDED_TYPES)
    weight = (n - n_subj) + 2 * n_subj
    if weight and 100 % weight == 0:
        return n, 100 // weight

    n_obj = n - n_subj
    for k in range(min(n_obj, 20), 0, -1):
        if 100 % k == 0:
            return k, 100 // k
    return n, 5


def all_batches(args):
    if getattr(args, "batch", None):
        return {"(arg)": args.batch}
    path = os.path.join(WORK, "batch.json")
    if not os.path.exists(path):
        die("没有批次记录，先执行 paper")
    return json.load(open(path))


def course_ids_arg(args):
    ids = [x.strip() for x in (getattr(args, "courses", None) or "").split(",") if x.strip()]
    if ids:
        return ids
    token, _ = ctx()
    zmq = find_user(token, "zmq") or die("找不到学生 zmq")
    return [c["courseId"] for c in zmq["courses"]][:2]


# ════════════════════════ 命令 ════════════════════════

def cmd_inspect(args):
    token, _ = ctx()
    zmq = find_user(token, "zmq") or die("找不到学生 zmq")
    ok(f"zmq = {zmq['id']}（{zmq.get('name')}）")

    print("\nzmq 的选课（含题库题量）：")
    for c in zmq["courses"]:
        n = len(course_exercises(token, c["courseId"]))
        print(f"  - {c['courseTitle']}  id={c['courseId']}  题库={n} 道  状态={c['status']}")

    print("\n本租户有题的课程：")
    for c in list_courses(token):
        n = len(course_exercises(token, c["id"]))
        if n:
            print(f"  - {c['title']}  id={c['id']}  {n} 道")

    print("\n学生名单：")
    for name in ("zmq", "demo1", "demo2", "demo3"):
        u = find_user(token, name)
        print(f"  - {name}: " + (f"{u['id']}（{u.get('name')}）" if u else "不存在"))


def cmd_users(args):
    token, _ = ctx()
    admin_token, _ = login(ADMIN_USER, ADMIN_PASS)
    ok(f"管理员登录：{ADMIN_USER}")

    course_ids = course_ids_arg(args)
    print(f"目标课程：{course_ids}")

    for name in ("demo1", "demo2", "demo3"):
        user = find_user(admin_token, name)
        if user:
            uid = user["id"]
            ok(f"用户已存在：{name} = {uid}")
        else:
            payload = {
                "userName": name,
                "name": name,
                "surname": "-",
                "emailAddress": f"{name}@iketong.local",
                "password": STUDENT_PASS,
                "isActive": True,
                "studentNumber": name,
                "className": CLASS_NAME,
                "roleNames": ["Student"],
            }
            code, res = http("POST", "/api/app/tenant-user/user-for-tenant", payload, token=admin_token)
            if code not in (200, 201) and isinstance(res, dict):
                payload.pop("roleNames", None)
                code, res = http("POST", "/api/app/tenant-user/user-for-tenant", payload, token=admin_token)
            if code not in (200, 201):
                die(f"创建用户 {name} 失败", res)
            uid = res["id"]
            ok(f"创建用户 {name} = {uid}")

        for cid in course_ids:
            code, res = http(
                "POST", "/api/app/student-course/enroll-student", {"studentId": uid, "courseId": cid}, token=admin_token
            )
            if code in (200, 201, 204):
                ok(f"  选课 {name} → {cid}")
            else:
                print(f"  ! 选课 {name} → {cid} 返回 {code}：{json.dumps(res, ensure_ascii=False)[:160]}")

    global _STUDENT_INDEX
    _STUDENT_INDEX = None  # 选课变了，索引失效


def cmd_linkage(args):
    token, _ = ctx()
    ids = course_ids_arg(args)
    if len(ids) < 2:
        die("需要两门课程才能设置前后关系")
    courses = {c["id"]: c for c in list_courses(token)}
    code, res = http(
        "POST",
        "/api/app/learning-analytics/save-course-link",
        {"predecessorCourseId": ids[0], "successorCourseId": ids[1]},
        token=token,
    )
    if code not in (200, 201):
        die("设置课程前后关系失败", res)
    ok(f"{courses.get(ids[0], {}).get('title')}（前置） → {courses.get(ids[1], {}).get('title')}（后续）")


def cmd_kp(args):
    token, _ = ctx()
    courses = {c["id"]: c for c in list_courses(token)}
    for cid in course_ids_arg(args):
        title = courses.get(cid, {}).get("title", cid)
        code, res = http("POST", f"/api/app/knowledge-point/generate/{cid}", token=token, timeout=1200)
        if code not in (200, 201):
            print(f"! {title} 知识点生成失败 {code}: {json.dumps(res, ensure_ascii=False)[:300]}")
        else:
            ok(f"{title}：{json.dumps(res, ensure_ascii=False)[:220]}")


def render_paper_jpg(html, out_prefix):
    """HTML → PDF（本地 Gotenberg）→ JPG（pdftoppm 160dpi）"""
    os.makedirs(WORK, exist_ok=True)
    html_path = f"{out_prefix}.html"
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(html)

    import shutil

    stage = os.path.join(WORK, "gotenberg-stage")
    os.makedirs(stage, exist_ok=True)
    shutil.copyfile(html_path, os.path.join(stage, "index.html"))

    pdf_path = f"{out_prefix}.pdf"
    subprocess.run(
        [
            "curl", "-sS", "-o", pdf_path,
            "-F", f"files=@{os.path.join(stage, 'index.html')};filename=index.html;type=text/html",
            "-F", "paperWidth=8.27",
            "-F", "paperHeight=11.7",
            "-F", "marginTop=0.2",
            "-F", "marginBottom=0.2",
            "-F", "marginLeft=0.2",
            "-F", "marginRight=0.2",
            "http://localhost:3000/forms/chromium/convert/html",
        ],
        check=True,
    )
    probe = subprocess.run(["file", pdf_path], capture_output=True, text=True).stdout
    if "PDF" not in probe:
        die(f"Gotenberg 未返回 PDF：{probe}")

    subprocess.run(["pdftoppm", "-jpeg", "-r", "160", pdf_path, f"{out_prefix}-page"], check=True)
    folder = os.path.dirname(out_prefix) or "."
    base = os.path.basename(out_prefix)
    pages = sorted(p for p in os.listdir(folder) if p.startswith(f"{base}-page") and p.endswith(".jpg"))
    return [os.path.join(folder, p) for p in pages]


def cmd_paper(args):
    token, _ = ctx()
    targets = [x.strip() for x in args.students.split(",") if x.strip()]
    courses = {c["id"]: c for c in list_courses(token)}
    batches = {}

    for course_id in course_ids_arg(args):
        title = courses.get(course_id, {}).get("title", course_id)
        qn, per = pick_paper_shape(token, course_id)
        if not qn:
            print(f"! 课程「{title}」没有习题，跳过")
            continue
        print(f"\n=== {title}：{qn} 题，基础分 {per} ===")

        for name in targets:
            user = find_user(token, name) or die(f"找不到学生 {name}")
            tpl = None
            for attempt in range(25):
                code, candidate = http(
                    "POST",
                    "/api/app/learning-analytics/generate-paper-template",
                    {
                        "courseId": course_id,
                        "title": f"{title} 期末试卷",
                        "studentId": user["id"],
                        "studentName": user.get("name") or name,
                        "studentNumber": user.get("studentNumber") or name,
                        "className": CLASS_NAME,
                        "questionCount": qn,
                        "fullScorePerQuestion": per,
                        "randomScore": True,
                        "includeAnswer": True,
                    },
                    token=token,
                )
                if code not in (200, 201):
                    die(f"{name} 生成试卷失败", candidate)
                tpl = candidate
                if tpl.get("totalFullScore") == 100:
                    break
            else:
                print(f"  ! {name} 卷面满分 {tpl.get('totalFullScore')}（未凑到 100），确认时按比例折算")

            pages = render_paper_jpg(tpl["html"], os.path.join(WORK, f"paper-{name}-{course_id[:8]}"))
            ok(
                f"{name}: {tpl['questionCount']} 题 / 满分 {tpl['totalFullScore']} / 得分 {tpl['totalScore']}"
                f" → {len(pages)} 页"
            )

            group = f"{name}-{uuid.uuid4().hex[:8]}"
            files = []
            for idx, path in enumerate(pages, start=1):
                with open(path, "rb") as f:
                    b64 = base64.b64encode(f.read()).decode()
                files.append(
                    {
                        "fileName": os.path.basename(path),
                        "contentType": "image/jpeg",
                        "base64": b64,
                        "pageIndex": idx,
                        "mergeGroup": group,
                    }
                )

            code, up = http(
                "POST",
                "/api/app/learning-analytics/upload-paper-script",
                {
                    "courseId": course_id,
                    "courseName": title,
                    "title": f"{title} 期末试卷",
                    "examDate": args.exam_date or f"{time.strftime('%Y-%m-%d')}T00:00:00",
                    "studentId": user["id"],
                    "studentName": user.get("name") or name,
                    "studentNumber": user.get("studentNumber") or name,
                    "files": files,
                },
                token=token,
                timeout=900,
            )
            if code not in (200, 201) or not isinstance(up, dict) or "batchId" not in up:
                die(f"{name} 上传失败", up)
            ok(f"{name} 上传成功 batch={up['batchId']}")
            batches[title] = up["batchId"]

    os.makedirs(WORK, exist_ok=True)
    with open(os.path.join(WORK, "batch.json"), "w") as f:
        json.dump(batches, f, ensure_ascii=False, indent=2)
    print("\n批次：")
    for k, v in batches.items():
        print(f"  {k}: {v}")


def cmd_ocr(args):
    token, _ = ctx()
    for title, batch_id in all_batches(args).items():
        code, res = http("POST", f"/api/app/learning-analytics/run-paper-ocr/{batch_id}", token=token, timeout=1200)
        ok(f"[{title}] OCR 触发 → {code}")
        for i in range(40):
            time.sleep(10)
            code, rows = http("GET", f"/api/app/learning-analytics/paper-scripts/{batch_id}", token=token)
            if code != 200 or not isinstance(rows, list):
                continue
            state = [(r.get("studentName"), r.get("totalScore"), r.get("ocrConfidence"), r.get("confirmed")) for r in rows]
            print(f"  [{i * 10}s] {json.dumps(state, ensure_ascii=False)[:400]}")
            if rows and all((r.get("totalScore") or 0) > 0 or r.get("confirmed") for r in rows):
                break


def cmd_confirm(args):
    """确认校对结果，并把分数折算成 100 分制。"""
    token, _ = ctx()
    for title, batch_id in all_batches(args).items():
        print(f"\n=== 确认 {title} ===")
        code, rows = http("GET", f"/api/app/learning-analytics/paper-scripts/{batch_id}", token=token)
        if code != 200 or not isinstance(rows, list):
            print(f"! 取试卷列表失败 {code}")
            continue
        for row in rows:
            name = row.get("studentName")
            if row.get("confirmed"):
                ok(f"{name} 已确认，跳过")
                continue
            items = row.get("items") or []
            if not items:
                print(f"! {name} 没有题目明细")
                continue

            full = sum(float(i.get("fullScore") or 0) for i in items) or 1
            raw = sum(float(i.get("score") or 0) for i in items)
            scaled = [
                {
                    "id": i["id"],
                    "questionNo": i.get("questionNo"),
                    "exerciseId": i.get("exerciseId"),
                    "fullScore": round(float(i.get("fullScore") or 0) / full * 100, 1),
                    "score": round(float(i.get("score") or 0) / full * 100, 1),
                }
                for i in items
            ]
            diff = round(100 - sum(s["fullScore"] for s in scaled), 1)
            if abs(diff) >= 0.05:
                scaled[-1]["fullScore"] = round(scaled[-1]["fullScore"] + diff, 1)
                scaled[-1]["score"] = min(scaled[-1]["score"], scaled[-1]["fullScore"])

            total = round(sum(s["score"] for s in scaled), 1)
            code, res = http(
                "POST",
                f"/api/app/learning-analytics/confirm-paper-script/{row['id']}",
                {"totalScore": total, "items": scaled},
                token=token,
            )
            if code not in (200, 201, 204):
                print(f"! {name} 确认失败：{json.dumps(res, ensure_ascii=False)[:240]}")
            else:
                ok(f"{name} 确认：原始 {raw:g}/{full:g} → {total}/100")


def cmd_dedupe(args):
    """OCR 偶发对同一张卷生成多份分析结果：每个学生只保留题目最全、置信度最高的一份。"""
    token, _ = ctx()
    for title, batch_id in all_batches(args).items():
        code, rows = http("GET", f"/api/app/learning-analytics/paper-scripts/{batch_id}", token=token)
        if code != 200 or not isinstance(rows, list):
            print(f"! 取 {title} 试卷列表失败")
            continue

        by_student = {}
        for row in rows:
            by_student.setdefault(row.get("studentId"), []).append(row)

        for student_id, items in by_student.items():
            if len(items) <= 1:
                continue
            keep = max(
                items,
                key=lambda r: (len(r.get("items") or []), r.get("ocrConfidence") or 0, -(r.get("totalScore") or 0)),
            )
            for row in items:
                if row["id"] == keep["id"]:
                    continue
                code, _ = http("DELETE", f"/api/app/learning-analytics/paper-script/{row['id']}", token=token)
                print(f"  - [{title}] 删除重复结果 {row.get('studentName')} {row['id']} → {code}")
            ok(f"[{title}] 保留 {keep.get('studentName')} {keep['id']}（{len(keep.get('items') or [])} 题）")


def cmd_push(args):
    token, _ = ctx()
    for title, batch_id in all_batches(args).items():
        code, res = http("POST", f"/api/app/learning-analytics/push-reports/{batch_id}", token=token, timeout=1200)
        if code not in (200, 201):
            print(f"! [{title}] 推送失败 {code}: {json.dumps(res, ensure_ascii=False)[:240]}")
        else:
            ok(f"[{title}] 推送完成：{res}")


def cmd_collect(args):
    token, _ = ctx()
    out = {"tenant": TENANT_NAME, "batches": {}, "currentWeights": None, "reports": {}}

    for title, batch_id in all_batches(args).items():
        _, cw = http("GET", f"/api/app/learning-analytics/class-weakness/{batch_id}", token=token)
        _, ti = http("GET", f"/api/app/learning-analytics/teaching-insight/{batch_id}", token=token)
        _, rows = http("GET", f"/api/app/learning-analytics/paper-scripts/{batch_id}", token=token)
        course_id = (cw or {}).get("courseId") if isinstance(cw, dict) else None

        evaluation = None
        if course_id:
            code, ev = http(
                "POST",
                f"/api/app/learning-analytics/evaluate-recommendation?courseId={course_id}&topK=5",
                token=token,
                timeout=900,
            )
            evaluation = ev if code == 200 else {"error": code}

        out["batches"][title] = {
            "batchId": batch_id,
            "courseId": course_id,
            "classWeakness": cw,
            "teachingInsight": ti,
            "recommendationEvaluation": evaluation,
            "scripts": rows,
        }

    _, weights = http("GET", "/api/app/learning-analytics/recommendation-weights", token=token)
    out["currentWeights"] = weights

    # 学生端报告（用学生自己的 token 读，验证学生能看到）
    for name in [x.strip() for x in (args.students or "zmq,demo1,demo2,demo3").split(",")]:
        try:
            stoken, claims = login(name, STUDENT_PASS)
        except SystemExit:
            continue
        code, reports = http("GET", "/api/app/learning-analytics/my-reports", token=stoken)
        detail = None
        if code == 200 and isinstance(reports, list) and reports:
            _, detail = http("GET", f"/api/app/learning-analytics/my-report/{reports[0]['id']}", token=stoken)
        out["reports"][name] = {"reports": reports if code == 200 else code, "firstDetail": detail}

    os.makedirs(WORK, exist_ok=True)
    path = os.path.join(WORK, "collect.json")
    with open(path, "w") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    ok(f"已导出 {path}")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("inspect").set_defaults(func=cmd_inspect)

    p = sub.add_parser("users")
    p.add_argument("--courses")
    p.set_defaults(func=cmd_users)

    p = sub.add_parser("linkage")
    p.add_argument("--courses")
    p.set_defaults(func=cmd_linkage)

    p = sub.add_parser("kp")
    p.add_argument("--courses")
    p.set_defaults(func=cmd_kp)

    p = sub.add_parser("paper")
    p.add_argument("--students", default="zmq")
    p.add_argument("--courses")
    p.add_argument("--exam-date", dest="exam_date")
    p.set_defaults(func=cmd_paper)

    p = sub.add_parser("ocr")
    p.add_argument("--batch")
    p.set_defaults(func=cmd_ocr)

    p = sub.add_parser("confirm")
    p.add_argument("--batch")
    p.set_defaults(func=cmd_confirm)

    p = sub.add_parser("dedupe")
    p.add_argument("--batch")
    p.set_defaults(func=cmd_dedupe)

    p = sub.add_parser("push")
    p.add_argument("--batch")
    p.set_defaults(func=cmd_push)

    p = sub.add_parser("collect")
    p.add_argument("--batch")
    p.add_argument("--students", default="zmq,demo1,demo2,demo3")
    p.set_defaults(func=cmd_collect)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
