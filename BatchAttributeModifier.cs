using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 规则类型枚举
    /// </summary>
    public enum RuleType
    {
        Increment,      // 递增序号
        Decrement,      // 递减序号
        ReplaceFixed,   // 替换为固定值
        ReplaceExpr,    // 替换为表达式
        AppendPrefix,   // 追加前缀
        AppendSuffix,   // 追加后缀
        NumericAdd,     // 数值加法
        NumericSub,     // 数值减法
        NumericMultiply,// 数值乘法
        NumericDivide,  // 数值除法
        RegexReplace,   // 正则替换
        Clear,          // 清空属性
        CopyFrom        // 复制自其他属性
    }

    /// <summary>
    /// 条件类型枚举
    /// </summary>
    public enum ConditionType
    {
        None,           // 无条件
        Contains,       // 包含
        Equals,         // 等于
        NotEquals,      // 不等于
        Regex,          // 正则匹配
        Range,          // 数值范围
        LayoutContains, // 布局名包含
        Combined        // 组合条件
    }

    /// <summary>
    /// 条件组合运算符
    /// </summary>
    public enum CombineOperator
    {
        And,
        Or
    }

    /// <summary>
    /// 复制方式枚举
    /// </summary>
    public enum CopyMode
    {
        Direct,         // 直接复制
        Formatted,      // 格式化复制
        Expression      // 表达式计算
    }

    /// <summary>
    /// 编号模式枚举
    /// </summary>
    public enum NumberingMode
    {
        Global,         // 全局递增
        ByType,         // 按图纸类型分组
        ByLayout,       // 按布局分组
        CustomPrefix    // 自定义前缀
    }

    /// <summary>
    /// 属性规则配置（扩展版）
    /// </summary>
    public class AttributeRuleConfig
    {
        public string TargetTag { get; set; } = "";       // 目标属性标签
        public RuleType RuleType { get; set; } = RuleType.Increment;
        public bool Enabled { get; set; } = true;

        // 递增/递减参数
        public int StartNum { get; set; } = 1;
        public int Step { get; set; } = 1;
        public string Format { get; set; } = "{n}";

        // 替换参数
        public string ReplaceValue { get; set; } = "";
        public string ReplaceExpr { get; set; } = "";

        // 追加参数
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";

        // 数值运算参数
        public double Operand { get; set; } = 0;

        // 正则替换参数
        public string RegexPattern { get; set; } = "";
        public string RegexReplacement { get; set; } = "";

        // 复制参数
        public string CopySourceTag { get; set; } = "";
        public CopyMode CopyMode { get; set; } = CopyMode.Direct;
        public string CopyFormatTemplate { get; set; } = "";
        public string CopyExpression { get; set; } = "";

        // 条件参数
        public ConditionConfig Condition { get; set; }

        // 分组参数
        public string GroupByTag { get; set; } = "";
        public bool ResetPerGroup { get; set; } = false;

        // 类型缩写映射（用于按类型分组时）
        public Dictionary<string, string> TypeAbbreviations { get; set; } = new Dictionary<string, string>();

        // 同步复制目标（图号修改时同步写入其他属性）
        public List<CopyTarget> SyncCopyTargets { get; set; } = new List<CopyTarget>();

        public AttributeRuleConfig()
        {
            Condition = new ConditionConfig();
        }
    }

    /// <summary>
    /// 条件配置
    /// </summary>
    public class ConditionConfig
    {
        public ConditionType Type { get; set; } = ConditionType.None;
        public string TargetAttribute { get; set; } = "";
        public string ConditionValue { get; set; } = "";
        public string RegexPattern { get; set; } = "";
        public double RangeMin { get; set; } = 0;
        public double RangeMax { get; set; } = 100;
        public bool ExcludeMode { get; set; } = false;  // 满足条件的不修改

        // 组合条件
        public List<ConditionConfig> SubConditions { get; set; } = new List<ConditionConfig>();
        public CombineOperator Operator { get; set; } = CombineOperator.And;
    }

    /// <summary>
    /// 复制目标配置
    /// </summary>
    public class CopyTarget
    {
        public string Attribute { get; set; } = "";
        public string FormatTemplate { get; set; } = "";
    }

    /// <summary>
    /// 批量修改配置
    /// </summary>
    public class BatchModifyConfig
    {
        public List<AttributeRuleConfig> Rules { get; set; } = new List<AttributeRuleConfig>();
        public string TemplateName { get; set; } = "";

        public void AddRule(AttributeRuleConfig rule)
        {
            Rules.Add(rule);
        }

        public AttributeRuleConfig GetRuleForTag(string tag)
        {
            return Rules.FirstOrDefault(r => r.TargetTag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveRule(string tag)
        {
            Rules.RemoveAll(r => r.TargetTag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 图框选择配置
    /// </summary>
    public class FrameSelectionConfig
    {
        public NumberingMode Mode { get; set; } = NumberingMode.Global;
        public string FilterLayer { get; set; } = "";
        public string FilterBlockName { get; set; } = "";
        public List<string> SelectedLayouts { get; set; } = new List<string>();
        public bool OnlyAttributeBlocks { get; set; } = true;
        public bool ExcludeProcessed { get; set; } = false;
        public string FilterByType { get; set; } = "";  // 按图纸类型过滤
    }

    /// <summary>
    /// 批量属性修改执行器
    /// </summary>
    public class BatchAttributeModifier
    {
        private readonly NumberSequence _seq;

        public BatchAttributeModifier()
        {
            _seq = new NumberSequence();
        }

        /// <summary>
        /// 执行批量修改
        /// </summary>
        public List<ModifyPreviewItem> Execute(List<AttributeBlockData> blocks, BatchModifyConfig config)
        {
            var previews = new List<ModifyPreviewItem>();

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                ApplyRule(blocks, rule, previews);
            }

            return previews;
        }

        /// <summary>
        /// 只生成预览，不实际修改
        /// </summary>
        public List<ModifyPreviewItem> Preview(List<AttributeBlockData> blocks, BatchModifyConfig config)
        {
            var previews = new List<ModifyPreviewItem>();

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                PreviewRule(blocks, rule, previews);
            }

            return previews;
        }

        private void ApplyRule(List<AttributeBlockData> blocks, AttributeRuleConfig rule, List<ModifyPreviewItem> previews)
        {
            _seq.Template = rule.Format;
            _seq.StartNum = rule.StartNum;
            _seq.Step = Math.Abs(rule.Step);
            _seq.Reset();

            var filteredBlocks = FilterBlocksByCondition(blocks, rule.Condition);

            if (!string.IsNullOrEmpty(rule.GroupByTag) && rule.ResetPerGroup)
            {
                ApplyWithGrouping(filteredBlocks, rule, previews);
            }
            else
            {
                ApplyGlobal(filteredBlocks, rule, previews);
            }
        }

        private void PreviewRule(List<AttributeBlockData> blocks, AttributeRuleConfig rule, List<ModifyPreviewItem> previews)
        {
            _seq.Template = rule.Format;
            _seq.StartNum = rule.StartNum;
            _seq.Step = Math.Abs(rule.Step);
            _seq.Reset();

            var filteredBlocks = FilterBlocksByCondition(blocks, rule.Condition);

            for (int i = 0; i < filteredBlocks.Count; i++)
            {
                var block = filteredBlocks[i];
                if (!block.Attributes.ContainsKey(rule.TargetTag.ToUpperInvariant())) continue;

                string originalValue = block.Attributes[rule.TargetTag.ToUpperInvariant()];
                string newValue = CalculateNewValue(originalValue, i, rule, block);

                previews.Add(new ModifyPreviewItem
                {
                    BlockIndex = i,
                    BlockName = block.BlockName,
                    TagName = rule.TargetTag,
                    OriginalValue = originalValue,
                    NewValue = newValue,
                    HasChanged = originalValue != newValue
                });
            }
        }

        private void ApplyGlobal(List<AttributeBlockData> blocks, AttributeRuleConfig rule, List<ModifyPreviewItem> previews)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                string targetTagUpper = rule.TargetTag.ToUpperInvariant();
                if (!block.Attributes.ContainsKey(targetTagUpper)) continue;

                string originalValue = block.Attributes[targetTagUpper];
                string newValue = CalculateNewValue(originalValue, i, rule, block);
                block.Attributes[targetTagUpper] = newValue;

                previews.Add(new ModifyPreviewItem
                {
                    BlockIndex = i,
                    BlockName = block.BlockName,
                    TagName = rule.TargetTag,
                    OriginalValue = originalValue,
                    NewValue = newValue,
                    HasChanged = originalValue != newValue
                });

                // 同步复制到其他属性
                ApplySyncCopyTargets(block, newValue, rule.SyncCopyTargets);
            }
        }

        private void ApplyWithGrouping(List<AttributeBlockData> blocks, AttributeRuleConfig rule, List<ModifyPreviewItem> previews)
        {
            string lastGroupValue = null;
            int groupIndex = 0;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                string currentGroupValue = block.GetAttribute(rule.GroupByTag) ?? "";

                if (currentGroupValue != lastGroupValue)
                {
                    _seq.StartNum = rule.StartNum;
                    _seq.Reset();
                    groupIndex = 0;
                    lastGroupValue = currentGroupValue;
                }

                string targetTagUpper = rule.TargetTag.ToUpperInvariant();
                if (!block.Attributes.ContainsKey(targetTagUpper)) continue;

                string originalValue = block.Attributes[targetTagUpper];
                string newValue = CalculateNewValue(originalValue, groupIndex, rule, block);

                // 添加类型缩写前缀
                if (rule.TypeAbbreviations.Count > 0 && rule.RuleType == RuleType.Increment)
                {
                    string abbr = GetAbbreviation(currentGroupValue, rule.TypeAbbreviations);
                    if (!string.IsNullOrEmpty(abbr))
                    {
                        newValue = rule.Prefix + abbr + "-" + newValue + rule.Suffix;
                    }
                    else
                    {
                        newValue = rule.Prefix + newValue + rule.Suffix;
                    }
                }
                else
                {
                    newValue = ApplyPrefixSuffix(newValue, rule);
                }

                block.Attributes[targetTagUpper] = newValue;

                previews.Add(new ModifyPreviewItem
                {
                    BlockIndex = i,
                    BlockName = block.BlockName,
                    TagName = rule.TargetTag,
                    OriginalValue = originalValue,
                    NewValue = newValue,
                    HasChanged = originalValue != newValue,
                    GroupValue = currentGroupValue
                });

                // 同步复制到其他属性
                ApplySyncCopyTargets(block, newValue, rule.SyncCopyTargets);

                groupIndex++;
                _seq.Next();
            }
        }

        private string CalculateNewValue(string original, int index, AttributeRuleConfig rule, AttributeBlockData block)
        {
            switch (rule.RuleType)
            {
                case RuleType.Increment:
                    return _seq.Format(index);

                case RuleType.Decrement:
                    int num = rule.StartNum - index * Math.Abs(rule.Step);
                    return FormatNumber(num, rule.Format);

                case RuleType.ReplaceFixed:
                    return rule.ReplaceValue;

                case RuleType.ReplaceExpr:
                    return EvaluateExpression(rule.ReplaceExpr, original, index, block);

                case RuleType.AppendPrefix:
                    return rule.Prefix + original;

                case RuleType.AppendSuffix:
                    return original + rule.Suffix;

                case RuleType.NumericAdd:
                    if (double.TryParse(original, out double valAdd))
                        return (valAdd + rule.Operand).ToString();
                    return original;

                case RuleType.NumericSub:
                    if (double.TryParse(original, out double valSub))
                        return (valSub - rule.Operand).ToString();
                    return original;

                case RuleType.NumericMultiply:
                    if (double.TryParse(original, out double valMul))
                        return (valMul * rule.Operand).ToString();
                    return original;

                case RuleType.NumericDivide:
                    if (double.TryParse(original, out double valDiv) && rule.Operand != 0)
                        return (valDiv / rule.Operand).ToString();
                    return original;

                case RuleType.RegexReplace:
                    try
                    {
                        return Regex.Replace(original, rule.RegexPattern, rule.RegexReplacement);
                    }
                    catch { return original; }

                case RuleType.Clear:
                    return "";

                case RuleType.CopyFrom:
                    string sourceValue = block.GetAttribute(rule.CopySourceTag) ?? "";
                    return ApplyCopyMode(sourceValue, rule);

                default:
                    return original;
            }
        }

        private string ApplyPrefixSuffix(string value, AttributeRuleConfig rule)
        {
            if (!string.IsNullOrEmpty(rule.Prefix))
                value = rule.Prefix + value;
            if (!string.IsNullOrEmpty(rule.Suffix))
                value = value + rule.Suffix;
            return value;
        }

        private string ApplyCopyMode(string sourceValue, AttributeRuleConfig rule)
        {
            switch (rule.CopyMode)
            {
                case CopyMode.Direct:
                    return sourceValue;

                case CopyMode.Formatted:
                    return rule.CopyFormatTemplate.Replace("{值}", sourceValue)
                                                    .Replace("{value}", sourceValue);

                case CopyMode.Expression:
                    return EvaluateExpression(rule.CopyExpression, sourceValue, 0, null);

                default:
                    return sourceValue;
            }
        }

        private string FormatNumber(int num, string format)
        {
            // 简单格式化
            if (format.Contains("{nn}")) return num.ToString("D2");
            if (format.Contains("{nnn}")) return num.ToString("D3");
            if (format.Contains("{nnnn}")) return num.ToString("D4");
            return num.ToString();
        }

        private string EvaluateExpression(string expr, string original, int index, AttributeBlockData block)
        {
            if (string.IsNullOrEmpty(expr)) return original;

            string result = expr;
            result = result.Replace("{值}", original);
            result = result.Replace("{value}", original);
            result = result.Replace("{序号}", (index + 1).ToString());
            result = result.Replace("{索引}", index.ToString());

            if (block != null)
            {
                foreach (var attr in block.Attributes)
                {
                    result = result.Replace($"{{{attr.Key}}}", attr.Value);
                }
            }

            // 尝试计算数学表达式
            if (ContainsMathOperators(result))
            {
                try
                {
                    var dt = new DataTable();
                    var computed = dt.Compute(result, "");
                    result = computed?.ToString() ?? result;
                }
                catch { }
            }

            return result;
        }

        private bool ContainsMathOperators(string s)
        {
            return s.Contains("+") || s.Contains("-") || s.Contains("*") || s.Contains("/");
        }

        private string GetAbbreviation(string value, Dictionary<string, string> abbreviations)
        {
            if (abbreviations == null || string.IsNullOrEmpty(value)) return "";

            // 精确匹配
            if (abbreviations.TryGetValue(value, out string abbr))
                return abbr;

            // 包含匹配
            foreach (var kv in abbreviations)
            {
                if (value.Contains(kv.Key))
                    return kv.Value;
            }

            return "";
        }

        private void ApplySyncCopyTargets(AttributeBlockData block, string newValue, List<CopyTarget> targets)
        {
            foreach (var target in targets)
            {
                if (string.IsNullOrEmpty(target.Attribute)) continue;

                string targetTagUpper = target.Attribute.ToUpperInvariant();
                if (!block.Attributes.ContainsKey(targetTagUpper)) continue;

                string targetValue = target.FormatTemplate.Replace("{值}", newValue)
                                                         .Replace("{value}", newValue);
                block.Attributes[targetTagUpper] = targetValue;
            }
        }

        private List<AttributeBlockData> FilterBlocksByCondition(List<AttributeBlockData> blocks, ConditionConfig condition)
        {
            if (condition == null || condition.Type == ConditionType.None)
                return blocks;

            var result = new List<AttributeBlockData>();

            foreach (var block in blocks)
            {
                bool matches = EvaluateCondition(block, condition);

                // 排除模式下，匹配的不处理
                if (condition.ExcludeMode)
                    matches = !matches;

                if (matches)
                    result.Add(block);
            }

            return result;
        }

        private bool EvaluateCondition(AttributeBlockData block, ConditionConfig condition)
        {
            switch (condition.Type)
            {
                case ConditionType.None:
                    return true;

                case ConditionType.Contains:
                    string attrValContains = block.GetAttribute(condition.TargetAttribute) ?? "";
                    return attrValContains.Contains(condition.ConditionValue);

                case ConditionType.Equals:
                    string attrValEquals = block.GetAttribute(condition.TargetAttribute) ?? "";
                    return attrValEquals.Equals(condition.ConditionValue, StringComparison.OrdinalIgnoreCase);

                case ConditionType.NotEquals:
                    string attrValNotEquals = block.GetAttribute(condition.TargetAttribute) ?? "";
                    return !attrValNotEquals.Equals(condition.ConditionValue, StringComparison.OrdinalIgnoreCase);

                case ConditionType.Regex:
                    string attrValRegex = block.GetAttribute(condition.TargetAttribute) ?? "";
                    try
                    {
                        return Regex.IsMatch(attrValRegex, condition.RegexPattern);
                    }
                    catch { return false; }

                case ConditionType.Range:
                    string attrValRange = block.GetAttribute(condition.TargetAttribute) ?? "";
                    if (double.TryParse(attrValRange, out double val))
                        return val >= condition.RangeMin && val <= condition.RangeMax;
                    return false;

                case ConditionType.LayoutContains:
                    // 使用块的布局名称属性
                    string layoutName = block.LayoutName ?? "";
                    return layoutName.Contains(condition.ConditionValue);

                case ConditionType.Combined:
                    return EvaluateCombinedCondition(block, condition);

                default:
                    return true;
            }
        }

        private bool EvaluateCombinedCondition(AttributeBlockData block, ConditionConfig condition)
        {
            if (condition.SubConditions.Count == 0) return true;

            bool result = EvaluateCondition(block, condition.SubConditions[0]);

            for (int i = 1; i < condition.SubConditions.Count; i++)
            {
                bool subResult = EvaluateCondition(block, condition.SubConditions[i]);

                if (condition.Operator == CombineOperator.And)
                    result = result && subResult;
                else
                    result = result || subResult;
            }

            return result;
        }
    }

    /// <summary>
    /// 修改预览项
    /// </summary>
    public class ModifyPreviewItem
    {
        public int BlockIndex { get; set; }
        public string BlockName { get; set; }
        public string TagName { get; set; }
        public string OriginalValue { get; set; }
        public string NewValue { get; set; }
        public bool HasChanged { get; set; }
        public string GroupValue { get; set; } = "";
    }
}