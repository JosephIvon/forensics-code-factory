# ASSETS —— 本卷清单与门禁

> 这张表由脚本生成（清单与行数都是实算），不手写。
> **行数是硬数字**：`tools/verify_book_numbers.py` 会逐件核对，
> 改了正文不改这张表，门禁立刻红。

## 一、清单





**全卷合计：25 件 / 3,368 行。**

### 卷首卷尾

| 文件 | 行数 |
|---|---|
| `前言.md` | 110 |
| `尾声.md` | 103 |

### 正文

| 文件 | 行数 |
|---|---|
| `chapters/00-导读.md` | 59 |
| `chapters/ch01-代码工厂.md` | 193 |
| `chapters/ch02-真值链.md` | 196 |
| `chapters/ch03-手册进知识库.md` | 202 |
| `chapters/ch04-双仓收尾实录.md` | 176 |

### 裁剪件

| 文件 | 行数 |
|---|---|
| `crops/裁剪版.md` | 111 |

### 入库的决策清单（快照，一字未动）

| 文件 | 行数 |
|---|---|
| `decision-lists/trae_factory-2da4559.md` | 66 |
| `decision-lists/axiscope-36e2c74.md` | 72 |

### 登记表

| 文件 | 行数 |
|---|---|
| `checks.json` | 37 |
| `known_findings.json` | 23 |
| `evidence/REGISTRY.json` | 42 |

### 门禁与工具

| 文件 | 行数 |
|---|---|
| `tools/_anchor.py` | 44 |
| `tools/audit_coords.py` | 354 |
| `tools/build_crops.py` | 208 |
| `tools/check_book_structure.py` | 170 |
| `tools/check_broken_links.py` | 313 |
| `tools/locate.py` | 77 |
| `tools/scan_absolute_paths.py` | 185 |
| `tools/verify_book_numbers.py` | 255 |
| `tools/verify_crops_verbatim.py` | 68 |

### 规格与清单

| 文件 | 行数 |
|---|---|
| `OUTLINE.md` | 102 |
| `README.md` | 86 |

### 本清单

| 文件 | 行数 |
|---|---|
| `ASSETS.md` | 116 |
## 二、门禁怎么跑

在**仓根**跑（不需要装依赖，Python 3.9+ 与 git 即可）：

```bash
python book/tools/audit_coords.py            # 坐标审计
python book/tools/check_book_structure.py    # 结构体检
python book/tools/verify_crops_verbatim.py   # 裁剪件逐字
python book/tools/verify_book_numbers.py     # 书里的数字
python book/tools/scan_absolute_paths.py     # 公开前脱敏
python book/tools/check_broken_links.py      # 断链
```

六道全 exit 0 才算过。**一道红就不许发。**

## 三、门禁自报（与本文件同批核对）

本次实跑坐标审计：**引用 22 ｜ 可解析 22 ｜ 失败 0 ｜ 行锚断言核对 9 处**

这一行不是装饰：`verify_book_numbers.py` 会**当场重跑一遍**审计，
把上面四个数与实跑的对比，对不上即判失败。
理由很直白——书里自报的门禁数字若没人核，那就是一句自我表扬。

## 四、两条硬规矩

1. **门禁匹配到 0 处即判失败**。匹配不到任何东西的门禁永远是绿的，那是空转，不是通过。
2. **登记不等于放行**。`known_findings.json` 里每条都要在书里披露；
   已清偿的登记项不再享受扫描豁免，值一旦回来立刻判红。

## 五、坐标的两种来源

本卷多数坐标在本仓；少数在兄弟仓（`forensics-mcopilot` 的
`knowledge/sdks/official_api/`、`axiscope` 的知识库条目）。
后者在审计输出里会标 `［跨仓:仓名］`，**不冒充本仓坐标**；
它们的锚登记在 `tools/_anchor.py` 的 `FOREIGN_ANCHORS`，结构门禁会核对。

干净克隆上兄弟仓不在位时，那几条会落进「仓外且不可判定」——
**这不是通过，是缺条件**。所以完成条件写了第 5 条：从干净克隆里跑一遍（见 `OUTLINE.md` §六）。
