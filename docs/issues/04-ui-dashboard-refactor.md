# Issue #04: BlockCatalogPanel.cs 看板式UI重构

## 背景
现有 TabControl 渐进式多页切换结构打断工程师作图心流。需要重构为一站式全看板（Single-Dashboard View）架构。

## 目标
使用 TableLayoutPanel 将主控制面板划分为四个高密度控制区域，实现高密度信息平铺。

## 具体任务

### 4.1 移除 TabControl
- 删除 `tabControl`、`tabExtract`、`tabStyle`、`tabGenerate` 控件
- 改用三列式 `TableLayoutPanel` 根布局

### 4.2 新布局结构
```
┌──────────────┬──────────────────────┬───────────────┐
│ 总控与排序   │   图形缓冲区         │ 目录输出定义  │
│ (Width:25%)  │   (Width:45%)       │ (Width:30%)   │
│              │ ┌────────────────┐  │               │
│ ○ 工作模式   │ │ DataGridView   │  │ 列宽公式     │
│ ○ 排序模式   │ │ (核心网格)     │  │ 字高/字宽    │
│ □ 反序       │ └────────────────┘  │               │
│              │ ┌────────────────┐  │ [同步改写图号]│
│ [重置][导入] │ │ 缀参数重编设置 │  │ [生成目录表格]│
│ [导出]       │ └────────────────┘  │               │
└──────────────┴──────────────────────┴───────────────┘
│                    日志区 (Height:120px)              │
└──────────────────────────────────────────────────────┘
```

### 4.3 Column 0: 总控与排序区 (25%)
GroupBox "总控与排序"，包含：
- 工作模式 RadioButtons：`更改图号` / `生成目录`
- 排序模式 RadioButtons：`左右上下`、`上下左右`、`选择序`、`数值序`
- CheckBox：`反序`
- 按钮：`重置`、`导入`、`导出`

### 4.4 Column 1: 图形缓冲区 + 缀参数区 (45%)
垂直拆分上下两个区域：

**上方 GroupBox "图形缓冲区"：**
- DataGridView 主体网格
- 右侧快捷按钮列：`选块`、`删块`、`上移`、`下移`
- 图框块名下拉去重选择框

**下方 GroupBox "缀参数重编设置"：**
- `缀始` (TextBox)
- `缀长` (TextBox)
- `[ ] 连续` (CheckBox)
- `前/后缀` RadioButtons
- `缀序样式格式串` (TextBox)

### 4.5 Column 2: 目录输出与定义区 (30%)
GroupBox "目录输出与定义"，包含：
- `列宽表达式` (TextBox，支持 `16+31+65` 解析)
- `间距表达式` (TextBox)
- `行高` (NumericUpDown)
- `字高` (NumericUpDown)
- `[ ] 显示表头` (CheckBox)
- 大按钮：`同步至图纸属性`
- 大按钮：`指定点生成目录`

### 4.6 DataGridView 美化细节
- `BorderStyle = BorderStyle.None`
- `SelectionMode = DataGridViewSelectionMode.FullRowSelect`
- `MultiSelect = false`
- 背景色复用 `Theme.Card`
- 文本色复用 `Theme.Text`
- 网格线色复用 `Theme.Border`

### 4.7 DataGridView 行拖拽换序
- 实现 MouseDown/MouseMove/MouseUp 事件
- 实现 DoDragDrop 行交换逻辑
- `DataGridViewRow.Tag` 绑定 `ObjectId` 或 `Handle`
- 增加虚拟列：`当前提取值`、`重编预览值`

### 4.8 日志区
- RichTextBox，高度 120px，Dock Bottom
- 保留现有功能

## 验收标准
- [ ] 编译通过
- [ ] 移除所有 TabControl 相关代码
- [ ] 三列式 TableLayoutPanel 布局正常显示
- [ ] DataGridView 数据绑定正常
- [ ] DataGridView 行拖拽换序功能正常
- [ ] 所有 RadioButton 组正确互斥
- [ ] 日志区正常显示

## 涉及文件
- `BlockCatalogPanel.cs`
- 可能需要调整 `ThemeConfig.cs` 颜色定义
