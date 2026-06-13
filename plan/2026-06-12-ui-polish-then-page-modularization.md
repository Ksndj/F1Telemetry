# F1Telemetry 逐页 UI 美化与页面模块化计划

> **给后续执行代理：** 执行本计划时，跨多个页面的并行审计建议使用 `superpowers:subagent-driven-development`；单页面里程碑执行建议使用 `superpowers:executing-plans`。所有步骤使用复选框跟踪，每个里程碑必须保持可构建、可回退、可独立验证。

**目标：** 先在稳定化边界内逐页完成 UI 美化，等所有页面交互稳定并通过验证后，再开始页面独立模块化计划。

**架构原则：** 保持当前 WPF + MVVM 结构。UI 美化优先限制在 `F1Telemetry.App/Views`；共享的显式样式放在 `F1Telemetry.App/Styles`；只有 XAML 无法安全表达时，才在 `F1Telemetry.App/Behaviors` 增加 UI 行为。页面模块化阶段再逐步抽离页面 ViewModel 和页面局部资源，不跨层移动业务逻辑。

**技术栈：** WPF/XAML、MVVM、.NET、xUnit UI/XAML 静态测试，以及现有 `F1Telemetry.App`、`F1Telemetry.Core`、`F1Telemetry.Analytics`、`F1Telemetry.Storage`、`F1Telemetry.AI`、`F1Telemetry.TTS`、`F1Telemetry.Udp` 项目。

## UI 美化执行策略（按页面分阶段推进）

### 总体原则

F1Telemetry 的 UI 美化不再采用大范围一次性全局改造方式，改为按页面分阶段、小步推进、逐页验收。

从当前阶段开始，`实时概览` 与 `分析播报` 两个页面作为新的 UI 参考基准页。后续其他页面的 UI 美化，统一参考这两个页面的视觉风格、布局逻辑、控件样式与交互方式，逐页推进，不允许跨多个页面同时做大规模样式改动。

### 基准页定义

以下两个页面作为 UI 风格基准：

1. `实时概览`
2. `分析播报`

后续所有页面的 UI 美化，都要对齐以下要素：

- 深色蓝黑主背景。
- 顶部状态栏卡片样式。
- 左侧导航栏视觉风格。
- 页面主标题与副说明的层级。
- 卡片式内容容器。
- 统一圆角、边框、阴影、间距。
- 统一按钮、下拉框、输入框样式。
- 统一空状态（无数据 / 等待数据 / 不可用）。
- 统一滚动区域、分页区、日志区的视觉表现。
- 统一图标大小、标题字号、正文层级和色彩。

### 执行方式：先出示例图，再做页面 UI

从本阶段开始，每个待改造页面都必须先生成示例图，再进入实际 UI 开发。

固定执行顺序：

1. 选择一个页面作为当前改造目标。
2. 参考 `实时概览` + `分析播报` 的 UI 风格，先生成该页面的示例图 / 设计稿。
3. 对示例图进行确认，明确布局、卡片分区、控件位置、小窗口 / 大窗口延展能力，以及是否保留原业务信息结构。
4. 示例图确认后，才允许开始该页面 UI 实现。
5. 实现完成后，必须做截图对比和交互回归验证。
6. 当前页面稳定后，才能进入下一个页面。

### 严格限制：禁止全局一起改

为防止全局 UI 改动引发连锁回归，新增以下限制：

- 不允许一次性同时改多个页面。
- 不允许为了统一风格直接替换全局所有控件模板。
- 不允许在未完成单页验收前顺手修改其他页面。
- 不允许先改全局 Style 再回头修页面。
- 不允许把 `ComboBox`、`Popup`、`ScrollViewer`、`DataGrid`、分页控件等高风险控件做全局隐式重写。
- 不允许把 UI 美化和业务逻辑修复混在同一个阶段大批量推进。

### 页面改造顺序

已作为基准页，不再大改，只做必要小修：

1. `实时概览`
2. `分析播报`

后续逐页改造顺序：

3. `弯角分析`
4. `多会话对比`
5. `赛后复盘`
6. `单圈历史`
7. `对手`
8. `事件日志`
9. `AI / TTS`
10. `设置`

说明：

- `实时概览`、`分析播报` 作为风格模板页。
- `弯角分析` 优先，因为当前它与基准页风格接近，适合作为首个按模板扩展的页面。
- `多会话对比`、`赛后复盘`、`单圈历史` 涉及图表、筛选器、分页，属于中高复杂度页面。
- `设置`、`AI / TTS` 控件种类多、交互复杂，放在后面处理。

### 单页 UI 改造标准流程

每个页面都按以下步骤执行：

1. 页面分析：列出当前页面的业务模块、交互控件和高风险点，如下拉框、滚动、分页、图表、弹出层。
2. 生成示例图：基于 `实时概览` + `分析播报` 的风格生成当前页面 UI 示例图，保持同一套视觉语言，不改变页面核心业务结构，优化信息分区和可读性，兼容正常窗口、较小窗口、滚动访问，并明确空状态、数据状态、错误状态。
3. 示例图确认：确认整体风格、与基准页一致性、开发可实现性，以及是否避免小窗口挤压和控件重叠。
4. 页面实现：只实现当前页面，不扩散到其他页面。
5. 回归验证：验证页面布局、小窗口兼容、滚动行为、下拉框行为、按钮状态、空状态文案、图表显示、分页逻辑，并确认不影响其他页面。
6. 截图归档：保留改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

### 每页完成标准（DoD）

每个页面只有满足以下条件，才算完成：

1. 页面视觉风格与 `实时概览` / `分析播报` 一致。
2. 页面布局清晰，卡片区块分明。
3. 常规窗口下无错位、遮挡、压缩异常。
4. 小窗口下可滚动访问，不出现核心信息不可见。
5. 下拉框、滚动条、分页、按钮等交互正常。
6. 空状态文案统一、视觉统一。
7. 不引入其他页面回归问题。
8. 已完成截图验收。
9. 已完成该页面专属测试与人工验证。

### 控件统一规则

后续页面改造时，统一遵守以下控件规则：

- 按钮：主按钮、次按钮、禁用按钮统一风格；尺寸不要过大，避免压缩布局；页面内同类按钮高度一致。
- 下拉框：下拉框样式统一；必须正确显示 `DisplayName`，不显示对象类型名；下拉列表展开后可滚动、可点击、可拖动滚动条；页面滚动时不得出现漂移、消失、不可操作问题。
- 输入框：输入框与下拉框、按钮的高度保持协调；只读项不能使用错误绑定方式导致崩溃或异常。
- 卡片：标题、内容、说明层级统一；空状态时使用统一文案和统一视觉占位。
- 图表：图表区域固定、清晰；数据不足时显示空状态，不强行绘制异常图；不出现负数坐标、异常缩放、滚轮冲突；图表滚动与页面滚动需明确分离。

### UI 美化阶段与稳定修复分离

从本阶段开始，计划中明确区分稳定修复阶段与 UI 美化阶段。

稳定修复阶段只修：

- 崩溃。
- 数据缺失。
- 逻辑错误。
- 错误状态。
- 回归问题。

UI 美化阶段只做：

- 布局优化。
- 视觉统一。
- 卡片整理。
- 控件样式统一。
- 页面信息层级优化。

要求：

- 一个阶段不要同时做大量业务逻辑和大量 UI 改造。
- UI 美化阶段如果发现阻断性功能问题，先暂停该页美化，转回稳定修复。

### 分支与提交策略

UI 美化阶段必须按页面单独推进，建议使用如下分支命名：

- `feature/ui-corner-analysis-polish`
- `feature/ui-session-comparison-polish`
- `feature/ui-post-race-review-polish`
- `feature/ui-lap-history-polish`
- `feature/ui-opponents-polish`
- `feature/ui-event-log-polish`
- `feature/ui-ai-tts-polish`
- `feature/ui-settings-polish`

提交要求：

- 一个分支只处理一个页面。
- 一个 PR 只提交一个页面的 UI 改造。
- PR 中必须附改造前截图、示例图、改造后截图、小窗口截图、已知限制说明。

### 下一步执行计划

当前 UI 美化执行顺序调整为：

1. 阶段 1：冻结 `实时概览` 与 `分析播报`，只做必要小修，不再反复改版。
2. 阶段 2：`弯角分析`，先生成示例图，确认后实现 UI，完成回归验证。
3. 阶段 3：`多会话对比`，先生成示例图，确认图表区、筛选区、差异摘要区布局，实现 UI，验证图表与滚动交互。
4. 阶段 4：`赛后复盘`，先生成示例图，重点处理图表、事件时间线、AI 报告区，再实现 UI。
5. 阶段 5：`单圈历史`，先生成示例图，重点处理会话列表、圈历史表格、最近圈摘要。
6. 阶段 6：`对手` / `事件日志` / `AI / TTS` / `设置`，逐页生成示例图、逐页确认、逐页实现、逐页验收。

### 最终目标

最终目标不是快速一次性把所有页面做成新样式，而是：

- 保持系统稳定。
- 保持业务功能正确。
- 在稳定基础上逐页统一视觉风格。
- 用 `实时概览` + `分析播报` 作为模板，逐步完成整套界面的统一升级。

## 范围边界

- [ ] 不继续之前的大范围全局 UI 重构。
- [ ] 不为全部 `Button`、`ComboBox`、`TextBox`、`CheckBox`、`Slider`、`DataGrid` 增加粗暴隐式样式。
- [ ] UI 美化阶段不改 UDP parser、数据库 schema、AI/TTS/RaceAssistant 业务逻辑或 ViewModel 数据流。
- [ ] 不使用 `Viewbox` 包住整页。
- [ ] 不通过拉大窗口 `MinWidth` 来规避布局问题。
- [ ] 顶部 UDP 端口组保持整体不换行。
- [ ] 只读字段保持 `OneWay` 或改用 `TextBlock`。
- [ ] 每个页面一个小里程碑、一个提交，方便窄范围回退。

## 当前页面地图

- `F1Telemetry.App/Views/OverviewView.xaml` - 实时概览。
- `F1Telemetry.App/Views/ChartsView.xaml` - 分析播报。
- `F1Telemetry.App/Views/LapHistoryView.xaml` - 单圈历史。
- `F1Telemetry.App/Views/PostRaceReviewView.xaml` - 赛后复盘。
- `F1Telemetry.App/Views/SessionComparisonView.xaml` - 多会话对比。
- `F1Telemetry.App/Views/CornerAnalysisView.xaml` - 弯角分析。
- `F1Telemetry.App/Views/OpponentsView.xaml` - 对手。
- `F1Telemetry.App/Views/LogsView.xaml` - 事件日志。
- `F1Telemetry.App/Views/AiTtsView.xaml` - AI / TTS。
- `F1Telemetry.App/Views/SettingsView.xaml` - 设置。
- `F1Telemetry.App/Views/Shell/NavigationTemplateSelector.cs` - 页面路由。
- `F1Telemetry.App/Styles/SharedStyles.xaml`、`ThemeColors.xaml`、`ScrollBarStyles.xaml`、`ShellStyles.xaml` - 共享 UI 资源。

## 阶段 0 - 基线审计

- [ ] 记录当前分支、HEAD 和工作区未提交文件。
- [ ] 列出所有仍存在 raw 对象名、默认白色控件、长路径不截断、滚动/下拉交互脆弱的页面 XAML。
- [ ] 开始修改前先确认每个页面已有的测试覆盖。
- [ ] 如果问题能用静态 XAML/UI contract 表达，先补窄范围测试，再改页面。
- [ ] 大输出写入 `.logs/`，最终只汇报摘要。

基线验证命令：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "MainWindow|ComboBox|Settings|AiBroadcast|PostRaceReview|SessionComparison|LapHistory|CornerAnalysis|Logs|Opponents" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
dotnet build F1Telemetry.sln --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

## 阶段 1 - 逐页 UI 美化

逐页推进。每个页面里程碑必须先完成页面分析、示例图、确认、单页实现、回归验证、截图归档，再执行测试、构建、提交、推送。当前页面稳定后，才能进入下一个页面。

### 1. 冻结基准页：实时概览 + 分析播报

- [ ] 冻结 `F1Telemetry.App/Views/OverviewView.xaml`，作为风格基准，不再大改。
- [ ] 冻结 `F1Telemetry.App/Views/ChartsView.xaml`，作为风格基准，不再大改。
- [ ] 只做阻断级或明确回归的小修，不反复改版。
- [ ] 归档两个基准页的常规窗口截图、小窗口截图和关键交互截图。
- [ ] 记录基准页的深色蓝黑背景、顶部状态卡片、左侧导航、标题层级、卡片容器、控件样式、空状态和滚动区域表现。
- [ ] `分析播报` 保持模式 `ComboBox` 用户可读、AI 关闭时生成按钮禁用、报告详情清空、数据不足时图表空状态等现有验证。
- [ ] `实时概览` 保持顶部状态区和 UDP 端口组整体不换行。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Overview|MainWindow|UdpPort|AiAnalysis|Dashboard|ComboBox|Charts" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 2. 弯角分析

- [ ] 先分析 `F1Telemetry.App/Views/CornerAnalysisView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成 `弯角分析` 示例图 / 设计稿，对齐 `实时概览` + `分析播报` 的视觉语言。
- [ ] 示例图确认后再实现页面 UI。
- [ ] session、lap、reference 选择器在长列表下仍可用。
- [ ] 保持图表和弯角分析绑定不变。
- [ ] 验证小窗口下页面滚动可访问。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "CornerAnalysis|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 3. 多会话对比

- [ ] 先分析 `F1Telemetry.App/Views/SessionComparisonView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，重点确认图表区、筛选区、差异摘要区布局。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 赛道筛选显示 `DisplayName`，不能显示 `SessionComparisonTrackFilterViewModel`。
- [ ] 保持图表绑定不变。
- [ ] 验证图表坐标、空状态、滚动交互。
- [ ] 已选会话卡片必须可读、可删除、不重叠。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "SessionComparison|ComboBox|Chart" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 4. 赛后复盘

- [ ] 先分析 `F1Telemetry.App/Views/PostRaceReviewView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，重点处理图表、事件时间线、AI 报告区。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 保持图表绑定和空状态不变。
- [ ] 验证稀疏数据下图表坐标不会出现不安全的负范围。
- [ ] 导出和刷新控件在小窗口中仍可访问。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "PostRaceReview|Chart|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 5. 单圈历史

- [ ] 先分析 `F1Telemetry.App/Views/LapHistoryView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，重点处理会话列表、圈历史表格、最近圈摘要。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 保持 session/lap 关键绑定不变。
- [ ] 页面整体支持纵向滚动，表格区域支持内部横向滚动。
- [ ] 删除、刷新、分页按钮使用页面显式样式，不引入全局隐式样式。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LapHistory|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 6. 对手

- [ ] 先分析 `F1Telemetry.App/Views/OpponentsView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，确认对手列表、空状态、受限状态和详情区域布局。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 空状态使用用户可读文案：等待对手数据、对手遥测受限。
- [ ] 内部编码只放 Tooltip 或日志，不作为正文主显示。
- [ ] 使用页面局部卡片/列表美化，不引入全局模板。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Opponents" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 7. 事件日志

- [ ] 先分析 `F1Telemetry.App/Views/LogsView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，确认日志区、筛选区、空状态和长文本展示方式。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 时间、分类、摘要保持可见。
- [ ] 长文本截断，Tooltip 显示完整内容。
- [ ] 保持 `LogEntries` 绑定不变。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LogsView|Logs|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 8. AI / TTS

- [ ] 先分析 `F1Telemetry.App/Views/AiTtsView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，确认语音、麦克风、说话模式、轮胎库存、日志区布局。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 保持所有 `ComboBox` 显示用户可读文本，不显示 `ViewModel.ToString()` 类型名。
- [ ] 长语音/设备列表必须能在下拉框内部滚动和选择。
- [ ] `+/-` 步进按钮保持居中、可读、不遮挡。
- [ ] 小窗口下语音、麦克风、说话模式、轮胎库存、日志区仍可用。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "AiTts|ComboBox|Settings" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 9. 设置

- [ ] 先分析 `F1Telemetry.App/Views/SettingsView.xaml` 的业务模块、交互控件和高风险点。
- [ ] 先生成示例图，确认 App 日志、UDP Raw Log、方向盘语音 AI、版本更新等分区。
- [ ] 示例图确认后再实现页面 UI。
- [ ] 长路径必须截断显示，Tooltip 显示完整路径。
- [ ] 只读字段保持 `OneWay` 或 `TextBlock`。
- [ ] 麦克风和说话模式下拉框必须能滚动、拖动滚动条和选择。
- [ ] 可编辑设置保留现有 `TwoWay` 和 `UpdateSourceTrigger`。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Settings|ComboBox|MainWindow" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

## 单页面里程碑检查清单

每个页面 UI 美化提交都按这个清单执行。

- [ ] 先确认当前页面是本里程碑唯一 UI 改造目标。
- [ ] 先查看页面 XAML、ViewModel 绑定和现有测试。
- [ ] 列出当前页面的业务模块、交互控件和高风险点。
- [ ] 先生成当前页面示例图 / 设计稿。
- [ ] 示例图经过确认后，才开始页面 UI 实现。
- [ ] 能用测试表达的问题，优先补最窄 UI/XAML 测试。
- [ ] 只对当前页面做局部 XAML/样式改动。
- [ ] 不改 ViewModel 数据流。
- [ ] 小窗口检查：页面可纵向滚动，表格/列表必要时可内部滚动。
- [ ] 下拉框检查：能打开、能内部滚动、能选择、不显示对象名。
- [ ] 禁用控件检查：禁用状态文字仍可读。
- [ ] 运行页面对应的过滤测试命令。
- [ ] 运行解决方案 build。
- [ ] 涉及运行时交互时，手动打开 App 验证。
- [ ] 归档改造前截图、示例图、改造后截图、小窗口截图和关键交互截图。
- [ ] 记录已知限制，特别是小窗口、滚动、下拉框、图表、分页的剩余风险。
- [ ] 只提交当前里程碑相关文件并推送。

## 共享样式规则

- [ ] 共享资源保持 keyed、显式引用、按需使用。
- [ ] 除非有单独稳定化计划批准，不新增全局隐式控件模板。
- [ ] 至少两个页面稳定复用同一模式后，才抽成共享 keyed 样式。
- [ ] 优先使用页面局部布局修复，不用共享模板掩盖页面问题。
- [ ] `ComboBox` 的下拉项截断、Tooltip 完整文本、`MaxDropDownHeight` 可以作为稳定后共享 contract。
- [ ] 不添加会在鼠标位于下拉 Popup 内时关闭下拉框的页面级滚动监听。

## 阶段 2 - 模块化前置门槛

以下条件全部满足后，才能开始页面模块化。

- [ ] 阶段 1 列出的所有页面都通过对应 targeted test。
- [ ] `dotnet build F1Telemetry.sln --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal` 通过。
- [ ] 设置、AI/TTS、分析播报、多会话对比、弯角分析、单圈历史、赛后复盘通过人工冒烟。
- [ ] 没有阻塞级 ComboBox 回归。
- [ ] 没有页面依赖大范围全局样式重写才能可用。
- [ ] 当前仍存在的 UI 限制已经记录到计划或 issue。

## 阶段 3 - 页面独立模块化计划

只在阶段 2 通过后开始。第一目标是降低改动难度，不是立刻拆成很多项目。

### 页面模块定义

一个页面模块应包含：

- 页面 View XAML 和 code-behind。
- 当页面当前依赖过大的共享 ViewModel 表面时，增加或抽离页面专属 ViewModel。
- 只服务该页面的 converter、template、keyed resource。
- 覆盖该页面绑定和 UI contract 的聚焦测试。

### 推荐抽离顺序

- [ ] 将分析播报页面状态从过宽的 dashboard 职责中抽出，形成专属页面 ViewModel，同时保持现有公开行为。
- [ ] 将 AI/TTS 页面设置和显示状态抽成页面 ViewModel 或 coordinator facade。
- [ ] 在能降低绑定风险时，将设置页状态抽成独立 Settings ViewModel。
- [ ] 如果单圈历史页面状态表面仍然过宽，再为历史浏览状态增加页面级接口或 facade。
- [ ] 赛后复盘、多会话对比、弯角分析已有较专门的 ViewModel，除非出现真实重复或职责问题，否则暂不拆。

### 模块化边界

- [ ] 业务服务保持在现有领域项目中。
- [ ] UDP 解析保持在 `F1Telemetry.Udp`。
- [ ] 数据库 schema 和迁移代码不在该计划内修改。
- [ ] AI/TTS/RaceAssistant 逻辑保持在现有服务中。
- [ ] App shell 只负责导航、全局状态和页面组合。
- [ ] 不急着创建每页一个 csproj；只有当页面 ViewModel 和测试稳定后，再评估是否值得拆项目。

## 预期收益与成本

- 先逐页 UI 美化，可以避免模块化后固化已有 UI 问题。
- 每页一个里程碑，回归更容易定位和回退。
- 后续页面模块化会缩小绑定表面和测试范围，降低单页改动难度。
- 如果过早拆模块，可能因为共享状态复制、依赖绕路而增加复杂度。
- 最稳妥路径是：只有当某个页面反复造成维护问题时，才抽离它的明确所有权边界。

## 最终汇报模板

每个执行里程碑结束时必须汇报：

- 修改文件。
- 本次美化或模块化的页面。
- 示例图 / 设计稿确认结果。
- 截图归档结果。
- 明确保留的关键绑定。
- 测试/build 结果。
- 人工验证结果。
- 当前分支。
- commit hash。
- push 状态。
- 剩余风险或已知 UI 限制。
