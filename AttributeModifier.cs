using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 属性批量修改器
    /// </summary>
    public class AttributeModifier
    {
        public class ModifyRule
        {
            /// <summary>
            /// 要修改的属性标签（为空则修改所有）
            /// </summary>
            public string TagName { get; set; } = "";

            /// <summary>
            /// 前缀
            /// </summary>
            public string Prefix { get; set; } = "";

            /// <summary>
            /// 后缀
            /// </summary>
            public string Suffix { get; set; } = "";

            /// <summary>
            /// 起始号（用于数字递增）
            /// </summary>
            public int StartNum { get; set; } = 1;

            /// <summary>
            /// 步长
            /// </summary>
            public int Step { get; set; } = 1;

            /// <summary>
            /// 通配符（* 或 ?）
            /// </summary>
            public string Wildcard { get; set; } = "*";

            /// <summary>
            /// 替换为的字符串
            /// </summary>
            public string ReplaceWith { get; set; } = "";

            /// <summary>
            /// 是否使用正则表达式
            /// </summary>
            public bool UseRegex { get; set; } = false;

            /// <summary>
            /// 是否前后缀模式（false=后缀）
            /// </summary>
            public bool PrefixBefore { get; set; } = true;
        }

        /// <summary>
        /// 序号递增配置
        /// </summary>
        public class SequenceConfig
        {
            public string TargetTag { get; set; } = "";      // 目标属性标签
            public string Template { get; set; } = "{n}";    // 序号模板 "{n}", "{nn}", "{A}"
            public int StartNum { get; set; } = 1;           // 起始号
            public int Step { get; set; } = 1;               // 步长
            public string Prefix { get; set; } = "";         // 前缀
            public string Suffix { get; set; } = "";         // 后缀
            public string ConditionTag { get; set; } = "";   // 分组条件属性（按此分组递增）
            public bool ResetPerGroup { get; set; } = false; // 每组重新计数
        }

        /// <summary>
        /// 批量修改属性块属性
        /// </summary>
        public void Modify(List<AttributeBlockData> blocks, ModifyRule rule)
        {
            if (blocks == null || blocks.Count == 0) return;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var attrs = block.Attributes;

                if (string.IsNullOrEmpty(rule.TagName))
                {
                    // 修改所有属性
                    foreach (var key in attrs.Keys)
                    {
                        attrs[key] = ApplyRule(attrs[key], rule, i);
                    }
                }
                else
                {
                    // 修改指定属性
                    if (attrs.ContainsKey(rule.TagName))
                    {
                        attrs[rule.TagName] = ApplyRule(attrs[rule.TagName], rule, i);
                    }
                }
            }
        }

        /// <summary>
        /// 对单个值应用修改规则
        /// </summary>
        private string ApplyRule(string value, ModifyRule rule, int index)
        {
            if (value == null) value = "";

            // 处理通配符替换
            if (!string.IsNullOrEmpty(rule.Wildcard) && rule.Wildcard != "*")
            {
                string pattern = rule.Wildcard.Replace("*", ".*").Replace("?", ".");
                if (rule.UseRegex)
                {
                    try
                    {
                        value = Regex.Replace(value, pattern, rule.ReplaceWith);
                    }
                    catch { }
                }
                else
                {
                    // 简单通配符匹配（仅支持 *）
                    if (rule.Wildcard.Contains("*"))
                    {
                        string regex = "^" + rule.Wildcard.Replace("*", ".*") + "$";
                        if (Regex.IsMatch(value, regex))
                            value = rule.ReplaceWith;
                    }
                }
            }

            // 处理前缀/后缀
            if (!string.IsNullOrEmpty(rule.Prefix) && rule.PrefixBefore)
                value = rule.Prefix + value;

            if (!string.IsNullOrEmpty(rule.Suffix) && !rule.PrefixBefore)
                value = value + rule.Suffix;

            return value;
        }

        /// <summary>
        /// 批量修改序号属性（专门用于序号递增）
        /// </summary>
        public void ModifySequence(List<AttributeBlockData> blocks, string tagName, ModifyRule rule)
        {
            if (blocks == null || blocks.Count == 0) return;

            int num = rule.StartNum;
            foreach (var block in blocks)
            {
                if (block.Attributes.ContainsKey(tagName))
                {
                    string newValue = rule.PrefixBefore
                        ? rule.Prefix + num.ToString() + rule.Suffix
                        : num.ToString();
                    block.Attributes[tagName] = newValue;
                }
                num += rule.Step;
            }
        }

        /// <summary>
        /// 按分组递增序号
        /// </summary>
        public void ModifySequenceByGroup(List<AttributeBlockData> blocks, SequenceConfig config)
        {
            if (blocks == null || blocks.Count == 0) return;
            if (string.IsNullOrEmpty(config.TargetTag)) return;

            var seq = new NumberSequence
            {
                Template = config.Template,
                StartNum = config.StartNum,
                Step = config.Step
            };

            if (config.ResetPerGroup && !string.IsNullOrEmpty(config.ConditionTag))
            {
                // 按条件属性分组递增
                string lastCondition = null;

                foreach (var block in blocks)
                {
                    string condition = block.GetAttribute(config.ConditionTag) ?? "";

                    if (condition != lastCondition)
                    {
                        // 新组，重置序号
                        seq.StartNum = config.StartNum;
                        lastCondition = condition;
                    }

                    string targetTagUpper = config.TargetTag.ToUpperInvariant();
                    if (block.Attributes.ContainsKey(targetTagUpper))
                    {
                        string newValue = config.Prefix + seq.Format(seq.CurrentIndex) + config.Suffix;
                        block.Attributes[targetTagUpper] = newValue;
                    }
                    seq.Next();
                }
            }
            else
            {
                // 全局递增
                for (int i = 0; i < blocks.Count; i++)
                {
                    string targetTagUpper = config.TargetTag.ToUpperInvariant();
                    if (blocks[i].Attributes.ContainsKey(targetTagUpper))
                    {
                        string newValue = config.Prefix + seq.Format(i) + config.Suffix;
                        blocks[i].Attributes[targetTagUpper] = newValue;
                    }
                }
            }
        }

        /// <summary>
        /// 将修改后的属性写回CAD数据库
        /// </summary>
        public int WriteBackToCad(List<AttributeBlockData> blocks)
        {
            if (blocks == null || blocks.Count == 0) return 0;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            int successCount = 0;

            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var block in blocks)
                {
                    try
                    {
                        var br = tr.GetObject(block.BlockId, OpenMode.ForWrite) as BlockReference;
                        if (br == null) continue;

                        // 遍历属性引用
                        foreach (ObjectId arId in br.AttributeCollection)
                        {
                            var ar = tr.GetObject(arId, OpenMode.ForWrite) as AttributeReference;
                            if (ar == null) continue;

                            string tag = ar.Tag.Trim().ToUpperInvariant();
                            if (block.Attributes.TryGetValue(tag, out string newValue))
                            {
                                ar.TextString = newValue;
                            }
                        }

                        successCount++;
                    }
                    catch { }
                }

                tr.Commit();
            }

            return successCount;
        }
    }
}
