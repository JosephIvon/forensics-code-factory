# trae_factory —— 上位机代码工厂

> 一个**在浏览器里跑的运动控制上位机代码工厂**：填轴数与工艺，生成一套可编译的
> C# 运动控制工程（适配层 + 契约测试 + 真值链文档 + zip 包）。
>
> 本仓的特别之处不在"能生成"，而在**生成出来的东西带着真值链**：
> 每一条 P/Invoke 都能追到它是从哪份厂商知识库抽出来的、核到了哪一步。

---

## 一、怎么跑

### 方式 A：浏览器单文件（无需安装）

打开 `上位机代码工厂.html`（单文件交付 ✓，双击即可 ✓）。

这个 HTML 是**生成出来的** ✓——改代码请改 `gen/` 下的模块，然后：

```bash
node concat.js        # 把 gen/*.js + factory_ui.html 合成 上位机代码工厂.html
```

### 方式 B：命令行（可脚本化、可复现）

```bash
node test_gen.js gts        # 单品牌
node test_gen.js all        # 全品牌套件
node test_gen.js acs        # ACS（需厂商 DLL，不在全品牌套件内）
```

产物写到 `out/mc_<brand>/`（解包目录）+ `out/MotionControlKit_<brand>.zip`（包）✓。

**依赖**：

| 依赖 | 是否必需 | 说明 |
|---|---|---|
| Node.js | 必需 | 本机验证环境 v24.21.0 ✓ |
| **mcopilot 仓**（真值知识库） | 可选但强烈建议 | 生成时**实时读取** `mcopilot/knowledge/sdks/*.json`；找不到则整套 P/Invoke 按"未标注"降级（生成不中断 ✓） |

知识库位置按此顺序找 ✓：

1. 环境变量 `MCOPILOT_ROOT` 指向 mcopilot 仓根 ✓；
2. **同级兄弟目录** `../mcopilot`（三仓平级摆放时的默认 ✓）；
3. 都没有 → 整体降级，并在产物文档里写明"知识库不可达" ✓。

> 本仓**不复制**任何知识库内容 ✓——生成时读、生成即继承最新口径 ✓。
> 知识库侧的复核命令：`mcopilot/scripts/verify_traekb_citations.py` ✓。

---

## 二、生成物里有什么

```
out/mc_<brand>/
├── src/
│   ├── Adapters/        品牌适配器（C# → P/Invoke 封装 + 单位换算 + 线程锁）
│   ├── Core/            IMotionController 接口、轴配置、结果对象、控制器工厂
│   └── Virtual/         虚拟控制器（无硬件全流程仿真）
├── samples/Program.cs   上位机主程序（示例工艺：换卡只改一行）
├── docs/
│   ├── TruthChain.md    P/Invoke 真值链审计（逐条：本套件口径 vs mcopilot 口径）
│   ├── JointEval.md     共享行为评测集对照（本套件对哪些行为断言负责）
│   ├── ApiChecklist.md  API 核对清单
│   └── CapabilityMatrix.md  品牌能力差异矩阵
├── build/               build_net48.bat（Windows 自带 csc）+ GlueDemo.csproj（dotnet）
└── README.md            该套件自己的说明（含 30 秒跑起来）
```

`out_v_unique/` 是**去重后的补充件**（各品牌独有、不重复的部分）✓，
多出 `bin/GlueDemo.exe`、`docs/tech-debt.md`、`glue_report.csv` 这几样 ✓。

---

## 三、真值链：每条 P/Invoke 都带三根轴

产物里的每个 P/Invoke 声明都标着 `[源 | 核对 | 提取]` 三根轴 ✓：

- **源** = 这条结论从哪来（`verified` = 有出处；未标注 = 不知道）
- **核对** = 验到哪一步（`manual` 人工 / `hardware` 真机 / `auto` 机器比对 / `none` 没核对）
- **提取** = 签名对是否等于调用对（`judgment` = 有语义陷阱，**必须人签字才能当事实用**）

**当前各品牌 P/Invoke 条数**（数字取自本仓 `out/` 的真值链文档，生成时实算 ✓）：

| 品牌 | P/Invoke | 钉扎 | 未收录（降级） | 核对进度 |
|---|---|---|---|---|
| 固高 GTS/GTN | 22 | 19 | 3 | none 19 |
| 正运动 ZMotion | 20 | 17 | 3 | none 17 |
| 雷赛 DMC/EMC | 16 | 13 | 3 | none 13 |
| 软赢 SoftWin | 0（骨架） | 0 | 0 | — |
| 高川 GaoChuan | 0（骨架） | 0 | 0 | — |
| ACS SPiiPlus | 0 | 0 | 0 | 需厂商 DLL，不在全品牌套件 |
| **全品牌套件合计** | **58** | **49** | **9** | none 49 |

**请注意最后两列的诚实读法** ✓：

- `钉扎 49` = 这 49 条**在 mcopilot 知识库里找到了出处**（签名字段已对齐 ✓）；
- 但 `核对 none 49` = **这 49 条一条都没核对过** ✓ ——
  钉扎只解决"这条从哪来"，**不解决"它对不对"** ✓。
  按本仓的 R2 纪律：`judgment` 条目必须人工或真机签字才能当事实用，
  其余条目在生成代码里已标 `TODO[需核对厂商手册]` ✓。
- `降级 9` = 知识库里没收录的，生成代码里按 `[源 未标注 | 核对 none | 提取 judgment]` 处理 ✓。

---

## 四、已知边界（不承诺的事）

1. **没有真机验证过** ✓ —— 全套件的核对进度是 `none` ✓（见上表 ✓）。
   生成的是**骨架 + 已标注的 P/Invoke 声明**，不是"能上生产的驱动" ✓。
2. **软赢 / 高川是骨架** ✓ —— 0 条 P/Invoke，接口形态待厂商手册核对 ✓。
3. **ACS 需要厂商 DLL** ✓ —— 不在全品牌套件内 ✓。
4. **知识库不可达时整体降级** ✓ —— 生成不中断，但产物里的 P/Invoke 全部按未标注处理 ✓。
5. **共享评测集（接缝 3）尚未闭合** ✓ —— mcopilot 侧的 `knowledge/shared_eval.json`
   当前**未入库** ✓，因此 `docs/JointEval.md` 里只有工厂侧的断言，没有 mcopilot 侧对照行 ✓。
6. **能力矩阵基于厂商公开手册常识与行业经验** ✓ —— 产物里原话写着
   "上真机前以官方最新手册为准" ✓。

---

## 五、许可与第三方材料

- 本仓自有的代码、生成器与生成产物：**MIT**（见 `LICENSE` ✓）。
- 本仓**不含第三方原件** ✓；其真值引用来自 mcopilot 仓的官方绑定知识库，
  **生成时实时读取、不复制入库** ✓。
- 详见 `NOTICE.md` ✓。

## 六、仓库结构

```
gen/                生成器模块（唯一事实源）
├── index.js        入口：generate() 组装全套件
├── core.js         轴配置、控制器工厂、单位换算、行为断言
├── adapters_kb.js  品牌知识库（函数签名、参数、返回类型）
├── adapter_common.js  适配器公共骨架
├── modbus.js       Modbus RTU/TCP 客户端与 CRC
├── truth_kb.js     真值链接缝（生成时读 mcopilot 知识库，逐条标注 P/Invoke）
└── zip.js          打包
factory_ui.html     界面模板（concat.js 的输入）
上位机代码工厂.html   单文件交付物（concat.js 的输出）
test_gen.js         命令行入口
concat.js           合并成单文件
ocr.ps1             手册页 OCR 辅助（Windows OCR API，用于人工抄录核对）
out/                生成产物（已入库，可直接看）
```
