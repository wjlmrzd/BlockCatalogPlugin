# Issue #02: SuffixPatternEngine.cs 图号批量重编引擎

## 背景
目前插件只具备单点或离散覆盖属性块能力，缺乏根据"前缀+起始号+长度补零"规则自动批量洗牌重写全图纸属性的业务层引擎。

## 目标
编写全新的 `SuffixPatternEngine.cs` 类，实现批量重编图纸图号或序号的功能。

## 具体任务

### 2.1 创建核心方法
```csharp
public bool BulkRenameAttributes(
    List<AttributeBlockData> sortedBlocks,
    string targetTag,
    string prefix,
    string suffix,
    int startNum,
    int numLength
)
```

### 2.2 编号生成规则
计算公式：`targetValue = prefix + (startNum + index).ToString().PadLeft(numLength, '0') + suffix`

示例：
- prefix = "建施-"
- suffix = ""
- startNum = 1
- numLength = 2
- 结果：建施-01, 建施-02, 建施-03, ...

### 2.3 事务处理
1. 遍历 sortedBlocks 列表
2. 使用 BlockId (ObjectId) 通过 AutoCAD Transaction 锁定对应的 BlockReference
3. 遍历该块引用下的所有 AttributeReference
4. 匹配对应 Tag 名称
5. 写入 targetValue 到 TextString 属性
6. 调用 .UpdateField() 强制刷新图面显示

### 2.4 异常处理
- 必须包含严谨的事务提交（tr.Commit()）
- 异常捕获（tr.Abort()）
- 资源释放机制

### 2.5 返回值
返回布尔值代表是否全部批量重改成功

## 验收标准
- [ ] 编译通过
- [ ] BulkRenameAttributes 方法正确生成编号序列
- [ ] 正确通过 Transaction 批量写入属性
- [ ] 异常情况下正确回滚
- [ ] 返回值准确反映执行结果

## 涉及文件
- 新建 `SuffixPatternEngine.cs`
- 参考 `AttributeModifier.cs` 事务逻辑
