# Issue #01: SortEngine.cs 几何容差排序重构

## 背景
当前排序引擎的 Z 形或 S 形排序采用绝对坐标值比较，CAD 中多张图纸由于微小的手动对齐误差（如 Y 坐标差 0.001mm），会导致横排判断彻底错乱。

## 目标
引入 `Tolerance` 模糊矩阵算法，当两张图纸的 Y 轴高度差绝对值小于设定的阈值时，强制判定在同一横排。

## 具体任务

### 1.1 扩展 SortType 枚举
新增以下排序模式：
- `LeftRight_TopBottom` (左右 上下)
- `TopBottom_LeftRight` (上下 左右)
- `SelectionOrder` (原始选择顺序) - 已有
- `NumericOrder` (按提取到的属性值进行自然数排序)

### 1.2 修改 Sort 方法签名
```csharp
public List<AttributeBlockData> Sort(List<AttributeBlockData> blocks, SortType type, double tolerance = 500.0, bool isReverse = false)
```

### 1.3 重写"左右 上下"比较逻辑
- 首先比较两个 AttributeBlockData 对象的 Position.Y 坐标
- 如果 `Math.Abs(a.Position.Y - b.Position.Y) < tolerance`，认为在同一水平排，转而比较 Position.X（从小到大升序）
- 如果差值大于等于 tolerance，按 Y 坐标从大到小降序排列

### 1.4 重写"上下 左右"比较逻辑
同理引入 X 轴方向上的 tolerance 模糊判断

### 1.5 添加反序支持
如果 `isReverse` 为 true，在返回前执行 `.Reverse()`

## 验收标准
- [ ] 编译通过
- [ ] 原有四种排序模式功能保持不变
- [ ] 新增 tolerance 参数有效（差值小于阈值时视为同一排）
- [ ] 新增 isReverse 参数有效
- [ ] 新增 NumericOrder 排序功能正常

## 涉及文件
- `SortEngine.cs`
