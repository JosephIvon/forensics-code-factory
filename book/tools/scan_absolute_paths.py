#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""scan_absolute_paths.py —— 公开前脱敏扫描（书 + 仓内文档）。

抓四类东西：
  ① **家目录 / 用户名**：机器上有、换台机器就没有，且能反推是谁的机器。
  ② **本机工作区路径**：写进书里，读者按它去找文件必然落空。
  ③ **脱敏标识值**（`Versuchsstand` / `141.58.` / 盘符）：这三样在数据侧已经令牌化，
     文档侧就不许再出现——出现了就等于"数据脱敏了、文档没脱"。
     ⚠ 这一档是从 axiscope 卷的工具链继承来的；本仓当前的数据里**没有**这三类值，
     所以它在本仓实际恒为 0 处。保留是为了工具链一致，不是因为它在本仓查出了什么——
     每次运行会把命中数打出来，恒 0 就是恒 0，不许被当成"查过了所以安全"。
  ④ **绝对墙钟（日期 + 时刻）**：形如 `YYYY-MM-DD HH:MM:SS`。
     本仓的生成器会把**生成那一刻**写进每份产物（`gen/adapter_common.js:7`），
     于是它天然存在。判据见 `scan_wallclocks()` 的说明：
     生成物区之外判死，生成物区内**每文件最多 1 处**（产物元数据，不删但卡上限）。

判据：命中即判失败。第 ③ 类**工具链路径**（如 Python 安装位置）不在此列——
那是通用安装位置，不是谁的隐私。

**③ 类的语境豁免**：脱敏 token 本身是**公开的**——它就写在
`src/axiscope/assertions.py` 的 `REDACT_TOKENS` 常量里（那是要被代码匹配的），
策略文件也必须写明"要抹哪几个字"，否则门禁无法被人复核。
所以 token **出现在策略/计量语境里不是泄露**，出现在数据里才是。
本工具据上下文判定：命中行的**同段上下文**里带策略标记（脱敏/令牌/token/
REDACT/计数/实测/归零…）的，记入「策略语境（已豁免）」，**不计失败**；
不带标记的**判失败**。

为什么必须分这一档：一个把策略文本也判成泄露的门禁，第二次运行就会被人
加白名单绕过——**加了白名单的门禁，就只剩"看起来在查"这一层了**。
"""
from __future__ import annotations

import io
import json
import os
import re
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover - 环境相关
    pass

BOOK = Path(__file__).resolve().parents[1]
REPO = BOOK.parent

# ① 家目录 / 用户名
HOME_PAT = re.compile(r"[A-Za-z]:\\Users\\[^\\\s\"'`]+")
# ② 本机工作区 / CI 落点
WS_PAT = re.compile(r"[A-Za-z]:\\(?:workspace|a|ci_test|ci_sim|tmp|home)[\\/][^\s\"'`]*")
# ③ 本仓自己的脱敏标识值（与 src/axiscope/assertions.py 的 REDACT_TOKENS 同源）
REDACT_PAT = re.compile(r"Versuchsstand|141\.58\.")

# ③ 的语境豁免：命中处附近出现这些标记 = 在讲脱敏**策略/计量**，不是在泄露数据
POLICY_CTX = re.compile(
    r"脱敏|令牌|token|REDACT|归零|实测|命中|计数|标识值|PII|not in serialized_bundle|"
    r"probe_pii|替换为|正则清除|清除|不留|不出现|被抹|抹掉",
    re.IGNORECASE,
)
CTX_LINES = 3   # 上下文窗口：命中行上下各 3 行

# ④ 绝对墙钟（日期 + 时刻）。毫秒可有可无：本仓生成器写的是不带毫秒的 19 字符形式，
# 但别的工具可能写带毫秒的——两种都该抓，所以毫秒不进正则的必需部分。
WALLCLOCK_PAT = re.compile(r"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}")

#: 正则自检夹具。**必须**能被 WALLCLOCK_PAT 命中。
#: 命中不了 = 正则坏了 → 全仓"一处都没有" → 看起来是绿的，其实没在查。
WALLCLOCK_FIXTURE = "2026-10-23 09:09:29"

#: 生成物区：这些目录下允许**每文件一处**生成时刻（产物元数据）。
GENERATED_PREFIXES = ("out/", "out_v_unique/")
#: 每文件上限。卡在 1：多出第 2 处说明生成器在别处也写时刻，或真有值混了进来。
WALLCLOCK_PER_FILE_CAP = 1

#: 自豁免：**只有一处**——本文件里的自检夹具必须是真值（它的全部作用就是能被命中）。
#: 上限卡在 1：本文件若出现第 2 处，说明有人把真值抄进了说明文字，照样判失败。
#: 本文件其余示例一律写成占位形式 `YYYY-MM-DD HH:MM:SS`——那是故意的，不是笔误。
SELF_EXEMPT = {
    "book/tools/scan_absolute_paths.py": (
        1, "自检夹具本身必须是能命中正则的真值——这是它存在的唯一理由"),
}

# 扫哪些目录：**全仓**。
# 原来只扫 `book/ docs/ README.md NOTICE.md`——那样 `out/` 与 `gen/` 是盲区，
# 而④恰恰主要落在那里。2026-10-24 实测：扩到全仓后 ①②③ 新增命中 0 处，
# 所以扩面不引入噪声，只补漏。
TARGET_DIRS = ["."]

SKIP_DIRS = {".git", "__pycache__", ".pytest_cache", "node_modules"}

#: 扫哪些后缀。既有文档类，也有源码/产物类——④要在产物里查，光看 .md 不够。
SCAN_SUFFIXES = (".md", ".json", ".txt", ".py", ".js", ".cs", ".ps1",
                 ".html", ".yml", ".yaml", ".tsv", ".csproj", ".sln")


def iter_files():
    for rel in TARGET_DIRS:
        p = REPO / rel.replace("/", os.sep)
        if p.is_file():
            yield p
        elif p.is_dir():
            for dp, dns, fns in os.walk(p):
                dns[:] = sorted(d for d in dns if d not in SKIP_DIRS)
                for fn in sorted(fns):
                    if fn.endswith(SCAN_SUFFIXES):
                        yield os.path.join(dp, fn)


def scan_wallclocks():
    """④ 绝对墙钟。返回 (违规, 豁免, 自检失败信息)。

    违规 = 生成物区之外的一律违规，生成物区内超上限的也违规。
    豁免 = 生成物区、且该文件命中数 ≤ 上限。
    """
    by_file: dict[str, list[tuple[int, str]]] = {}
    for path in iter_files():
        rel = os.path.relpath(path, str(REPO)).replace("\\", "/")
        try:
            lines = io.open(path, encoding="utf-8", errors="replace").read().splitlines()
        except OSError:
            continue
        for i, ln in enumerate(lines, 1):
            for m in WALLCLOCK_PAT.finditer(ln):
                by_file.setdefault(rel, []).append((i, m.group(0)))

    bad, exempt = [], []
    for rel, occ in sorted(by_file.items()):
        if rel in SELF_EXEMPT:
            cap, why = SELF_EXEMPT[rel]
        elif rel.startswith(GENERATED_PREFIXES):
            cap, why = WALLCLOCK_PER_FILE_CAP, "生成物区：产物元数据（哪一次跑出来的）"
        else:
            cap, why = 0, "生成物区之外——没有任何产品语义可以替它辩护"
        if len(occ) <= cap:
            exempt.append((rel, len(occ), occ[0][1], why))
        else:
            for i, v in occ:
                bad.append((rel, i, v))
    return bad, exempt


def load_known() -> dict:
    """读登记在册的已知项（`book/known_findings.json`）。"""
    p = BOOK / "known_findings.json"
    if not p.is_file():
        return {}
    try:
        d = json.loads(p.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return {f.get("id"): f for f in d.get("findings", []) if f.get("id")}


def disclosed_ids(book_text: str) -> set[str]:
    """全书文本里出现过的登记项 id。"""
    return set(re.findall(r"\bK-\d+\b", book_text))


def main() -> int:
    known = load_known()
    book_text = "\n".join(
        io.open(p, encoding="utf-8", errors="replace").read()
        for p in sorted(BOOK.rglob("*.md"))
    )
    seen_known = set()

    hits = []
    exempt = []
    for path in iter_files():
        try:
            text = io.open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        lines = text.splitlines()
        for i, line in enumerate(lines, 1):
            ctx = "\n".join(lines[max(0, i - 1 - CTX_LINES): i + CTX_LINES])
            for pat, label in ((HOME_PAT, "家目录/用户名"), (WS_PAT, "本机工作区路径"),
                               (REDACT_PAT, "脱敏标识值")):
                for m in pat.finditer(line):
                    rec = (label, os.path.relpath(path, str(REPO)).replace("\\", "/"),
                           i, m.group(0))
                    if label == "脱敏标识值":
                        # 登记在册的已知项：命中即查登记
                        # **已清偿的登记项不提供豁免**——它当初值就长在那个位置，
                        # 现在那个位置必须干净；值再出现就是复发，判失败。
                        registered = False
                        for kid, f in known.items():
                            if str(f.get("state", "open")) == "cleared":
                                continue
                            if (f.get("file") == rec[1] and int(f.get("line", -1)) == rec[2]
                                    and rec[3] in str(f.get("token", ""))):
                                seen_known.add(kid)
                                exempt.append(rec + (kid,))
                                registered = True
                                break
                        if registered:
                            continue
                        if POLICY_CTX.search(ctx):
                            exempt.append(rec)
                            continue
                    hits.append(rec)

    # 登记了但没在书里披露的 → 判失败（护栏：登记表不得变成沉默的白名单）
    # 已清偿的项不再要求「在扫描里被命中」——值已经不在仓里了，
    # 命中不到才是对的；但**披露**这一条照旧要求（不许悄悄销账）。
    undisclosed = []
    for kid, f in known.items():
        if kid not in disclosed_ids(book_text):
            undisclosed.append((kid, f.get("file"), f.get("line")))
        elif str(f.get("state", "open")) != "cleared" and kid not in seen_known:
            undisclosed.append((kid, f.get("file"), f.get("line")))

    cleared = [k for k, f in known.items() if str(f.get("state", "open")) == "cleared"]
    if cleared:
        print(f"已清偿的登记项（**不再豁免扫描**——值若重新出现即判失败）：{'、'.join(sorted(cleared))}")
        print()

    if exempt:
        print("策略语境 / 登记在册（不计失败）——这些 token 出现在脱敏策略、计量文本或已登记的已知项里：")
        for rec in exempt:
            kid = rec[4] if len(rec) > 4 else None
            tail = f"（登记项 {kid}）" if kid else ""
            print(f"  ○ [{rec[0]}] {rec[1]}:{rec[2]}  →  {rec[3][:40]}{tail}")
        print()

    if undisclosed:
        print("登记在册但**未在书中披露**（登记表不得变成沉默的白名单）：")
        for kid, f, i in undisclosed:
            print(f"  ✗ {kid}  {f}:{i}")
        print()

    # ---- ④ 绝对墙钟（单独一趟：它的豁免是"每文件上限"，不是"逐条判语境"）
    print()
    print("【④ 绝对墙钟（日期 + 时刻）】")
    if WALLCLOCK_PAT.search(WALLCLOCK_FIXTURE):
        print(f"  自检：正则命中夹具 `{WALLCLOCK_FIXTURE}` ✓")
        wc_bad, wc_exempt = scan_wallclocks()
    else:
        print(f"  [FAIL] 正则坏了：夹具 `{WALLCLOCK_FIXTURE}` 命中不了——"
              f"全仓会显示'一处都没有'，那是假绿")
        wc_bad, wc_exempt = [], []
        hits.append(("墙钟正则自检", "book/tools/scan_absolute_paths.py", 0, "夹具不命中"))
    if wc_exempt:
        print(f"  豁免（生成物区，每文件 ≤ {WALLCLOCK_PER_FILE_CAP} 处，共 {len(wc_exempt)} 个文件）：")
        for rel, n, sample, why in wc_exempt[:6]:
            print(f"    ○ {rel}  （{n} 处，例：{sample}）— {why}")
        if len(wc_exempt) > 6:
            print(f"    ○ …另 {len(wc_exempt) - 6} 个文件同口径（生成物区，每文件 ≤ 1 处）")
        print("    理由：这是**产物元数据**（这一份是哪一次跑出来的），不是本机/上游的观测时钟，")
        print("    也不含机器标识；删掉就改变产物语义，所以留，但上限卡死。")
    if wc_bad:
        print("  违规（生成物区之外，或生成物区内超上限）：")
        for rel, i, v in wc_bad:
            print(f"    ✗ {rel}:{i}  →  {v}")
        hits.extend(("绝对墙钟", rel, i, v) for rel, i, v in wc_bad)
    if not wc_bad and not wc_exempt:
        print("  全仓 0 处绝对墙钟 ✓")

    if hits or undisclosed:
        if hits:
            print("命中（公开前必须清零）：")
            for label, f, i, frag in hits:
                print(f"  ✗ [{label}] {f}:{i}  →  {frag[:60]}")
        print(f"\n合计 {len(hits)} 处未清（豁免/登记 {len(exempt)} 处）；"
              f"墙钟豁免 {len(wc_exempt)} 个文件；"
              f"登记未披露 {len(undisclosed)} 处；已清偿登记 {len(cleared)} 条。")
        return 1
    print(f"合计 0 处（豁免/登记 {len(exempt)} 处）；"
          f"墙钟豁免 {len(wc_exempt)} 个文件（生成物 + 自检夹具）；"
          f"已清偿登记 {len(cleared)} 条；必改项清零才算公开就绪。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
