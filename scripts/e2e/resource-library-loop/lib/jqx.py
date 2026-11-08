#!/usr/bin/env python3
"""极简 JSON 查询助手（无第三方依赖，供 run.sh 使用）。

用法:
    jqx.py get    <file> [dotted.path]                 # 取值（不存在输出空行）
    jqx.py find   <file> [dotted.path] <key> <val> [outkey]
    jqx.py deep   <file> <key> <val> [outkey]   # 递归整棵树查找（嵌套 children 用）
    jqx.py len    <file> [dotted.path]                 # 数组长度
    jqx.py count  <file> [dotted.path] <key> <val>     # 数组里满足 key==val 的元素个数
    jqx.py has    <file> [dotted.path]                 # 存在则 exit 0
    jqx.py raw    <file>                               # 原样输出
"""
import json
import sys


def load(path):
    try:
        with open(path, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:
        return None


def dig(data, dotted):
    """按 'a.b.0.c' 取值；空路径表示根节点。"""
    cur = data
    if not dotted:
        return cur
    for part in dotted.split("."):
        if part == "":
            continue
        if isinstance(cur, list):
            try:
                cur = cur[int(part)]
            except Exception:
                return None
        elif isinstance(cur, dict):
            if part not in cur:
                return None
            cur = cur[part]
        else:
            return None
    return cur


def as_list(data):
    return data if isinstance(data, list) else []


def print_value(val):
    if val is None:
        print("")
    elif isinstance(val, bool):
        print("true" if val else "false")
    elif isinstance(val, (dict, list)):
        print(json.dumps(val, ensure_ascii=False))
    else:
        print(val)


def walk_find(node, key, want):
    """递归遍历整棵 JSON 树，找 key==want 的第一个字典。"""
    if isinstance(node, dict):
        if str(node.get(key)) == str(want):
            return node
        for v in node.values():
            hit = walk_find(v, key, want)
            if hit is not None:
                return hit
    elif isinstance(node, list):
        for v in node:
            hit = walk_find(v, key, want)
            if hit is not None:
                return hit
    return None


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    cmd = sys.argv[1]
    file_path = sys.argv[2]
    data = load(file_path)
    if data is None:
        print("")
        return 0

    rest = sys.argv[3:]

    if cmd == "get":
        path = rest[0] if rest else ""
        print_value(dig(data, path))
        return 0

    if cmd == "find":
        path = rest[0] if rest else ""
        key = rest[1] if len(rest) > 1 else ""
        want = rest[2] if len(rest) > 2 else ""
        outkey = rest[3] if len(rest) > 3 else "id"
        for item in as_list(dig(data, path)):
            if isinstance(item, dict) and str(item.get(key)) == str(want):
                print_value(item.get(outkey))
                return 0
        print("")
        return 0

    if cmd == "deep":
        key = rest[0] if rest else ""
        want = rest[1] if len(rest) > 1 else ""
        outkey = rest[2] if len(rest) > 2 else "id"
        hit = walk_find(data, key, want)
        print_value(hit.get(outkey) if isinstance(hit, dict) else None)
        return 0

    if cmd == "len":
        path = rest[0] if rest else ""
        print(len(as_list(dig(data, path))))
        return 0

    if cmd == "count":
        path = rest[0] if rest else ""
        key = rest[1] if len(rest) > 1 else ""
        want = rest[2] if len(rest) > 2 else ""
        n = 0
        for item in as_list(dig(data, path)):
            if isinstance(item, dict) and str(item.get(key)) == str(want):
                n += 1
        print(n)
        return 0

    if cmd == "has":
        path = rest[0] if rest else ""
        val = dig(data, path)
        return 0 if val not in (None, [], {}, "") else 1

    if cmd == "raw":
        print(json.dumps(data, ensure_ascii=False))
        return 0

    print(__doc__)
    return 1


if __name__ == "__main__":
    sys.exit(main())
