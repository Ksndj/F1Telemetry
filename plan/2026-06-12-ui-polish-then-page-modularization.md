# F1Telemetry 逐页 UI 美化与页面模块化计划

> **给后续执行代理：** 执行本计划时，跨多个页面的并行审计建议使用 `superpowers:subagent-driven-development`；单页面里程碑执行建议使用 `superpowers:executing-plans`。所有步骤使用复选框跟踪，每个里程碑必须保持可构建、可回退、可独立验证。

**目标：** 先在稳定化边界内逐页完成 UI 美化，等所有页面交互稳定并通过验证后，再开始页面独立模块化计划。

**架构原则：** 保持当前 WPF + MVVM 结构。UI 美化优先限制在 `F1Telemetry.App/Views`；共享的显式样式放在 `F1Telemetry.App/Styles`；只有 XAML 无法安全表达时，才在 `F1Telemetry.App/Behaviors` 增加 UI 行为。页面模块化阶段再逐步抽离页面 ViewModel 和页面局部资源，不跨层移动业务逻辑。

**技术栈：** WPF/XAML、MVVM、.NET、xUnit UI/XAML 静态测试，以及现有 `F1Telemetry.App`、`F1Telemetry.Core`、`F1Telemetry.Analytics`、`F1Telemetry.Storage`、`F1Telemetry.AI`、`F1Telemetry.TTS`、`F1Telemetry.Udp` 项目。

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

逐页推进。每个页面里程碑必须完成测试、构建、提交、推送后，再进入下一个页面。

### 1. 分析播报

- [ ] 美化 `F1Telemetry.App/Views/ChartsView.xaml` 的操作区控件。
- [ ] 确保模式 `ComboBox` 显示用户可读文本，而不是 raw enum 或类型名。
- [ ] 鼠标位于下拉框内部时，滚轮只滚动下拉列表，不带动主页滚动。
- [ ] 保持 AI 关闭时生成按钮禁用、报告详情清空等行为有测试覆盖。
- [ ] 验证数据不足时图表区域仍显示空状态。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "AiAnalysis|Dashboard|ComboBox|Charts" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 2. AI / TTS

- [ ] 在分析播报稳定后，再美化 `F1Telemetry.App/Views/AiTtsView.xaml`。
- [ ] 保持语音、麦克风、说话模式、轮胎库存、日志区在小窗口下可用。
- [ ] 保持所有 `ComboBox` 显示用户可读文本，不显示 `ViewModel.ToString()` 类型名。
- [ ] 长语音/设备列表必须能在下拉框内部滚动和选择。
- [ ] `+/-` 步进按钮保持居中、可读、不遮挡。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "AiTts|ComboBox|Settings" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 3. 设置

- [ ] 分区美化 `F1Telemetry.App/Views/SettingsView.xaml`：App 日志、UDP Raw Log、方向盘语音 AI、版本更新。
- [ ] 长路径必须截断显示，Tooltip 显示完整路径。
- [ ] 只读字段保持 `OneWay` 或 `TextBlock`。
- [ ] 麦克风和说话模式下拉框必须能滚动、拖动滚动条和选择。
- [ ] 可编辑设置保留现有 `TwoWay` 和 `UpdateSourceTrigger`。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Settings|ComboBox|MainWindow" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 4. 单圈历史

- [ ] 在核心下拉框行为稳定后，美化 `F1Telemetry.App/Views/LapHistoryView.xaml`。
- [ ] 保持 session/lap 关键绑定不变。
- [ ] 页面整体支持纵向滚动，表格区域支持内部横向滚动。
- [ ] 删除、刷新、分页按钮使用页面显式样式，不引入全局隐式样式。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LapHistory|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 5. 赛后复盘

- [ ] 美化 `F1Telemetry.App/Views/PostRaceReviewView.xaml`。
- [ ] 保持图表绑定和空状态不变。
- [ ] 验证稀疏数据下图表坐标不会出现不安全的负范围。
- [ ] 导出和刷新控件在小窗口中仍可访问。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "PostRaceReview|Chart|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 6. 多会话对比

- [ ] 美化 `F1Telemetry.App/Views/SessionComparisonView.xaml`。
- [ ] 赛道筛选显示 `DisplayName`，不能显示 `SessionComparisonTrackFilterViewModel`。
- [ ] 保持图表绑定不变。
- [ ] 验证图表坐标和空状态。
- [ ] 已选会话卡片必须可读、可删除、不重叠。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "SessionComparison|ComboBox|Chart" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 7. 弯角分析

- [ ] 在共享下拉框行为稳定后，美化 `F1Telemetry.App/Views/CornerAnalysisView.xaml`。
- [ ] session、lap、reference 选择器在长列表下仍可用。
- [ ] 保持图表和弯角分析绑定不变。
- [ ] 验证小窗口下页面滚动可访问。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "CornerAnalysis|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 8. 对手

- [ ] 美化 `F1Telemetry.App/Views/OpponentsView.xaml`。
- [ ] 空状态使用用户可读文案：等待对手数据、对手遥测受限。
- [ ] 内部编码只放 Tooltip 或日志，不作为正文主显示。
- [ ] 使用页面局部卡片/列表美化，不引入全局模板。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Opponents" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 9. 事件日志

- [ ] 美化 `F1Telemetry.App/Views/LogsView.xaml`。
- [ ] 时间、分类、摘要保持可见。
- [ ] 长文本截断，Tooltip 显示完整内容。
- [ ] 保持 `LogEntries` 绑定不变。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LogsView|Logs|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 10. 实时概览回归检查

- [ ] `F1Telemetry.App/Views/OverviewView.xaml` 作为风格基准，不作为重写目标。
- [ ] 只修复其他页面工作中暴露出的明确回归。
- [ ] 验证顶部状态区和 UDP 端口组仍保持整体不换行。

推荐测试：

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Overview|MainWindow|UdpPort" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

## 单页面里程碑检查清单

每个页面 UI 美化提交都按这个清单执行。

- [ ] 先查看页面 XAML、ViewModel 绑定和现有测试。
- [ ] 能用测试表达的问题，优先补最窄 UI/XAML 测试。
- [ ] 只对当前页面做局部 XAML/样式改动。
- [ ] 不改 ViewModel 数据流。
- [ ] 小窗口检查：页面可纵向滚动，表格/列表必要时可内部滚动。
- [ ] 下拉框检查：能打开、能内部滚动、能选择、不显示对象名。
- [ ] 禁用控件检查：禁用状态文字仍可读。
- [ ] 运行页面对应的过滤测试命令。
- [ ] 运行解决方案 build。
- [ ] 涉及运行时交互时，手动打开 App 验证。
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
- 明确保留的关键绑定。
- 测试/build 结果。
- 人工验证结果。
- 当前分支。
- commit hash。
- push 状态。
- 剩余风险或已知 UI 限制。
