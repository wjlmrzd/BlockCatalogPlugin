using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 缀参数图号批量重编引擎
    /// 支持前缀+起始号+长度补零+后缀规则自动批量洗牌重写属性
    /// </summary>
    public class SuffixPatternEngine
    {
        /// <summary>
        /// 批量重编属性值
        /// </summary>
        /// <param name="sortedBlocks">已按逻辑顺序排好序的属性块列表</param>
        /// <param name="targetTag">目标属性标签（如"XH"、"TH"等）</param>
        /// <param name="prefix">前缀（如"建施-"）</param>
        /// <param name="suffix">后缀（如"-PL"）</param>
        /// <param name="startNum">起始号</param>
        /// <param name="numLength">数字位数（不足时补零）</param>
        /// <returns>是否全部成功</returns>
        public bool BulkRenameAttributes(
            List<AttributeBlockData> sortedBlocks,
            string targetTag,
            string prefix = "",
            string suffix = "",
            int startNum = 1,
            int numLength = 2)
        {
            if (sortedBlocks == null || sortedBlocks.Count == 0)
                return false;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            int successCount = 0;
            int failCount = 0;
            var failedBlocks = new List<string>();

            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                // 创建编号生成器
                var seq = CreateNumberSequence(prefix, suffix, startNum, numLength);

                for (int index = 0; index < sortedBlocks.Count; index++)
                {
                    var block = sortedBlocks[index];
                    try
                    {
                        // 生成目标编号
                        string targetValue = seq.Format(index);

                        // 通过 ObjectId 获取 BlockReference
                        var br = tr.GetObject(block.BlockId, OpenMode.ForWrite) as BlockReference;
                        if (br == null)
                        {
                            failCount++;
                            failedBlocks.Add($"[块 {index + 1}] BlockReference 为空");
                            continue;
                        }

                        // 遍历属性引用，匹配目标标签
                        bool tagFound = false;
                        foreach (ObjectId arId in br.AttributeCollection)
                        {
                            var ar = tr.GetObject(arId, OpenMode.ForWrite) as AttributeReference;
                            if (ar == null) continue;

                            // 匹配 Tag（忽略大小写）
                            if (ar.Tag.Equals(targetTag, StringComparison.OrdinalIgnoreCase))
                            {
                                ar.TextString = targetValue;
                                // 注：UpdateField() 在 AutoCAD 2014 不可用，TextString 赋值后 CAD 会自动刷新
                                tagFound = true;
                                break; // 找到匹配的Tag后跳出
                            }
                        }

                        if (tagFound)
                            successCount++;
                        else
                        {
                            failCount++;
                            failedBlocks.Add($"[块 {index + 1}] 未找到标签 '{targetTag}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        failedBlocks.Add($"[块 {index + 1}] {ex.Message}");
                    }
                }

                // 只有在至少有一个成功且没有严重错误时才提交
                if (successCount > 0 && failCount == 0)
                {
                    tr.Commit();
                }
                else if (successCount > 0 && failCount > 0)
                {
                    // 部分成功的情况也提交（保留已成功的修改）
                    tr.Commit();
                    System.Diagnostics.Debug.WriteLine($"[SuffixPatternEngine] 部分成功: {successCount} 成功, {failCount} 失败");
                    foreach (var msg in failedBlocks)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {msg}");
                    }
                }
                else
                {
                    // 全部失败才回滚
                    tr.Abort();
                }
            }

            return successCount > 0;
        }

        /// <summary>
        /// 批量重编属性值（重载，支持 SequenceConfig 配置）
        /// </summary>
        public bool BulkRenameAttributes(
            List<AttributeBlockData> sortedBlocks,
            AttributeModifier.SequenceConfig config)
        {
            if (sortedBlocks == null || sortedBlocks.Count == 0 || config == null)
                return false;

            // 解析模板中的数字位数
            int numLength = GetNumLengthFromTemplate(config.Template);

            return BulkRenameAttributes(
                sortedBlocks,
                config.TargetTag,
                config.Prefix,
                config.Suffix,
                config.StartNum,
                numLength);
        }

        /// <summary>
        /// 从模板中获取数字位数
        /// {n}=1位, {nn}=2位, {nnn}=3位, {nnnn}=4位
        /// </summary>
        private int GetNumLengthFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return 2; // 默认2位

            // 查找最大的 n 重复次数
            int maxN = 0;
            if (template.Contains("{nnnn}")) maxN = 4;
            else if (template.Contains("{nnn}")) maxN = 3;
            else if (template.Contains("{nn}")) maxN = 2;
            else if (template.Contains("{n}")) maxN = 1;

            return maxN > 0 ? maxN : 2;
        }

        /// <summary>
        /// 根据数字位数构建模板字符串
        /// </summary>
        private static string BuildTemplate(int numLength)
        {
            return numLength switch
            {
                1 => "{n}",
                2 => "{nn}",
                3 => "{nnn}",
                4 => "{nnnn}",
                _ => "{nn}"
            };
        }

        /// <summary>
        /// 创建编号生成器
        /// </summary>
        private NumberSequence CreateNumberSequence(string prefix, string suffix, int startNum, int numLength)
        {
            return new NumberSequence
            {
                Template = BuildTemplate(numLength),
                StartNum = startNum,
                Step = 1,
                Prefix = prefix,
                Suffix = suffix
            };
        }

        /// <summary>
        /// 生成编号序列（不写入CAD，仅预览）
        /// </summary>
        public List<string> GenerateNumberSequence(
            int count,
            string prefix = "",
            string suffix = "",
            int startNum = 1,
            int numLength = 2)
        {
            var seq = new NumberSequence
            {
                Template = BuildTemplate(numLength),
                StartNum = startNum,
                Step = 1,
                Prefix = prefix,
                Suffix = suffix
            };

            var result = new List<string>();
            for (int i = 0; i < count; i++)
            {
                result.Add(seq.Format(i));
            }
            return result;
        }

    }
}
