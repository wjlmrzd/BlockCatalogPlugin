# BlockCatalogPlugin 优化 Issues 索引

## 实施顺序

| 节点 | Issue | 描述 | 优先级 |
|------|-------|------|--------|
| 节点1 | [01-sortengine-refactor](01-sortengine-refactor.md) | SortEngine.cs 几何容差排序重构 | P0 |
| 节点2.1 | [02-suffix-pattern-engine](02-suffix-pattern-engine.md) | SuffixPatternEngine.cs 图号批量重编引擎 | P0 |
| 节点2.2 | [03-column-width-formula](03-column-width-formula.md) | CatalogStyle.cs 列宽公式解析 | P1 |
| 节点3 | [04-ui-dashboard-refactor](04-ui-dashboard-refactor.md) | BlockCatalogPanel.cs 看板式UI重构 | P1 |

## 开发顺序建议

按照"一问一答，改完一个类再改下一个类"的增量模式：

1. **先执行 Issue #01** - 重构排序引擎，解决微米级容差错位问题
2. **再执行 Issue #02 + #03** - 补齐缺失的前后缀批量图号重编业务引擎
3. **最后执行 Issue #04** - 完成 UI 换代，实现 DataGridView 行拖拽换序

## Issue 详细说明

### Issue #01: SortEngine.cs 几何容差排序重构
- 新增 tolerance 参数，避免微小坐标误差导致排序错乱
- 新增 isReverse 反序参数
- 新增 NumericOrder 排序模式

### Issue #02: SuffixPatternEngine.cs 图号批量重编引擎
- 全新业务类
- 实现 BulkRenameAttributes 方法
- 支持前缀+起始号+长度补零规则

### Issue #03: CatalogStyle.cs 列宽公式解析
- 新增 ApplyFormulaWidths 方法
- 支持 "16+31+65+20" 格式连写解析

### Issue #04: BlockCatalogPanel.cs 看板式UI重构
- 移除 TabControl，改用三列式 TableLayoutPanel
- 新增 DataGridView 核心网格
- 实现行拖拽换序功能
- 新布局：总控区(25%) | 图形缓冲区+缀参数区(45%) | 目录输出区(30%)
