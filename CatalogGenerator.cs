using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;

namespace BlockCatalogPlugin
{
    // Types moved to CatalogStyle.cs and BlockExtractor.cs:
    // - MergeStrategy, SequenceFormat, ColumnDef, CatalogStyle (Issue 2)
    // - MergeConfig, MergeCriterion (kept in this file for ApplyMerge compatibility)
    // - AttributeBlockData (in BlockExtractor.cs)

    public class MergeConfig
    {
        public bool EnableMerge { get; set; } = true;
        public MergeCriterion Criterion { get; set; } = MergeCriterion.Prefix;
        public string RangeSymbol { get; set; } = "~";
        public string GroupSymbol { get; set; } = "-";
        public bool MergeOnlyConsecutive { get; set; } = true;
        public int MinMergeCount { get; set; } = 2;
    }

    public enum MergeCriterion
    {
        Prefix, Name, PrefixAndName, Size, Scale, Custom
    }

    public class CatalogGenerator
    {
        // 序号列宽
        private const double SequenceColumnWidth = 20;

        // 序号格式配置
        private static readonly string[] CircleNumbers = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫", "⑬", "⑭", "⑮", "⑯", "⑰", "⑱", "⑲", "⑳" };
        private static readonly string[] ChineseOneDigits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };

        /// <summary>
        /// 生成目录结果（含警告信息）
        /// </summary>
        public class GenerateResult
        {
            public ObjectId TableId { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public bool HasWarnings => Warnings.Count > 0;
        }

        /// <summary>
        /// 生成目录（使用 DBText 多行文字）
        /// </summary>
        /// <param name="blocks">属性块数据列表</param>
        /// <param name="style">目录样式</param>
        /// <param name="mergeConfig">合并配置</param>
        /// <param name="insertPoint">插入位置（null则使用原点）</param>
        /// <param name="targetLayoutName">目标布局名称（null则使用模型空间）</param>
        public GenerateResult GenerateTableWithResult(List<AttributeBlockData> blocks, CatalogStyle style, MergeConfig mergeConfig = null,
            Point3d? insertPoint = null, string targetLayoutName = null)
        {
            var result = new GenerateResult();
            if (blocks == null || blocks.Count == 0)
                throw new ArgumentException("没有可生成目录的属性块");

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取CAD文档");

            // 从 UI 躐程调用时必须锁定文档，否则会抛出 eLockViolation
            using (var docLock = doc.LockDocument())
            {
                var displayBlocks = blocks;
                if (mergeConfig != null && mergeConfig.EnableMerge)
                    displayBlocks = ApplyMerge(blocks, mergeConfig);

                var visibleCols = style.Columns.Where(c => c.Visible).ToList();
                int colCount = visibleCols.Count + 1;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取目标块表记录（模型空间或布局）
                        ObjectId targetBtrId;
                        if (!string.IsNullOrEmpty(targetLayoutName) && targetLayoutName != "Model")
                        {
                            // 指定布局
                            var lm = LayoutManager.Current;
                            var layoutId = lm.GetLayoutId(targetLayoutName);
                            if (layoutId != ObjectId.Null)
                            {
                                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                                targetBtrId = layout.BlockTableRecordId;
                            }
                            else
                            {
                                // 布局不存在，使用模型空间
                                targetBtrId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                            }
                        }
                        else
                        {
                            // 模型空间
                            targetBtrId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                        }

                        var targetBtr = (BlockTableRecord)tr.GetObject(targetBtrId, OpenMode.ForWrite);

                        // 计算插入位置
                        double startX = insertPoint?.X ?? 0;
                        double startY = insertPoint?.Y ?? 0;

                        double[] colWidths = new double[colCount];
                        colWidths[0] = SequenceColumnWidth;
                        for (int c = 1; c < colCount; c++)
                            colWidths[c] = visibleCols[c - 1]?.Width ?? 40;

                        // 表头在上，内容在下（Y递减方向）
                        double y = startY;
                        double x = startX;

                        // 插入表头（在最上方）- 居中显示
                        string warn = InsertText(tr, targetBtr, "序号", new Point3d(x, y, 0), style.FontHeight, true, colWidths[0], true);
                        if (!string.IsNullOrEmpty(warn)) result.Warnings.Add(warn);
                        for (int c = 1; c < colCount; c++)
                        {
                            x += colWidths[c - 1];
                            warn = InsertText(tr, targetBtr, visibleCols[c - 1]?.Header ?? "", new Point3d(x, y, 0), style.FontHeight, true, colWidths[c], true);
                            if (!string.IsNullOrEmpty(warn)) result.Warnings.Add(warn);
                        }
                        y -= style.HeaderHeight;  // 内容行向下移动

                        // 插入数据行 - 居中显示，自动适应
                        for (int r = 0; r < displayBlocks.Count; r++)
                        {
                            var block = displayBlocks[r];
                            int seqNum = style.SeqFormat.StartNum + r * style.SeqFormat.Step;
                            string seqStr = FormatSeqNum(seqNum, style.SeqFormat);

                            x = startX;
                            warn = InsertText(tr, targetBtr, seqStr, new Point3d(x, y, 0), style.FontHeight, false, colWidths[0], true);
                            if (!string.IsNullOrEmpty(warn)) result.Warnings.Add($"行{r + 1}序号: {warn}");
                            for (int c = 1; c < colCount; c++)
                            {
                                x += colWidths[c - 1];
                                var tag = visibleCols[c - 1]?.Tag;
                                string value = tag != null ? GetAttrBlockAttributeMulti(block, tag) : "";
                                warn = InsertText(tr, targetBtr, value, new Point3d(x, y, 0), style.FontHeight, false, colWidths[c], true);
                                if (!string.IsNullOrEmpty(warn)) result.Warnings.Add($"行{r + 1}列{c}: {warn}");
                            }
                            y -= style.RowHeight;  // 每行向下移动
                        }

                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 生成目录（兼容旧接口）
        /// </summary>
        public ObjectId GenerateTable(List<AttributeBlockData> blocks, CatalogStyle style, MergeConfig mergeConfig = null,
            Point3d? insertPoint = null, string targetLayoutName = null)
        {
            var result = GenerateTableWithResult(blocks, style, mergeConfig, insertPoint, targetLayoutName);
            return result.TableId;
        }

        /// <summary>
        /// 插入文字（支持居中和自动缩小字体）
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="ms">块表记录</param>
        /// <param name="text">文字内容</param>
        /// <param name="pos">位置</param>
        /// <param name="height">原始字高</param>
        /// <param name="bold">是否加粗</param>
        /// <param name="colWidth">列宽（用于计算居中和自动缩小）</param>
        /// <param name="autoFit">是否自动适应列宽</param>
        /// <returns>警告信息（如果有）</returns>
        private string InsertText(Transaction tr, BlockTableRecord ms, string text, Point3d pos, double height, bool bold, double colWidth = 0, bool autoFit = true)
        {
            if (string.IsNullOrEmpty(text)) text = "";

            // 计算文字宽度（粗略估算：每个字符约 0.7 倍字高）
            double textWidth = text.Length * height * 0.7;
            string warning = null;

            // 如果启用了自动适应且列宽足够大，则检测是否需要缩小字体
            double actualHeight = height;
            if (autoFit && colWidth > 0 && textWidth > colWidth * 0.9)
            {
                // 缩小字体以适应列宽（留 10% 边距）
                actualHeight = height * (colWidth * 0.9) / textWidth;
                actualHeight = Math.Max(actualHeight, 1.0); // 最小字高 1.0

                if (actualHeight < height * 0.5)
                {
                    // 如果缩小超过 50%，发出警告
                    warning = $"文字可能过小: {text.Substring(0, Math.Min(10, text.Length))}...";
                }
            }

            // 计算居中位置
            double offsetX = 0;
            if (colWidth > 0)
            {
                offsetX = colWidth / 2.0;
            }

            var dbText = new DBText
            {
                TextString = text,
                Position = new Point3d(pos.X + offsetX, pos.Y, pos.Z),
                Height = actualHeight,
                HorizontalMode = colWidth > 0 ? TextHorizontalMode.TextCenter : TextHorizontalMode.TextLeft,
                VerticalMode = TextVerticalMode.TextBottom,
                AlignmentPoint = new Point3d(pos.X + offsetX, pos.Y, pos.Z)
            };

            ms.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);

            return warning;
        }

        private string FormatSeqNum(int num, SeqFormatConfig format)
        {
            if (format == null) return num.ToString();
            string result = (format.Template ?? "{n}")
                .Replace("{n}", num.ToString())
                .Replace("{nn}", num.ToString("D2"))
                .Replace("{nnn}", num.ToString("D3"))
                .Replace("{c}", IndexToCircle(num))
                .Replace("{c1}", IndexToChinese1(num))
                .Replace("{cc}", IndexToCircle(num));
            return (format.Prefix ?? "") + result + (format.Suffix ?? "");
        }

        private string IndexToCircle(int num)
        {
            return (num >= 1 && num <= 20) ? CircleNumbers[num - 1] : num.ToString();
        }

        private string IndexToChinese1(int num)
        {
            if (num <= 0) return ChineseOneDigits[0];
            if (num <= 10) return ChineseOneDigits[num];
            if (num < 20) return "十" + (num == 10 ? "" : ChineseOneDigits[num - 10]);
            if (num < 100) return ChineseOneDigits[num / 10] + "十" + (num % 10 == 0 ? "" : ChineseOneDigits[num % 10]);
            return num.ToString();
        }

        public List<AttributeBlockData> ApplyMerge(List<AttributeBlockData> blocks, MergeConfig config)
        {
            if (blocks == null || blocks.Count < 2 || !config.EnableMerge) return blocks;

            var result = new List<AttributeBlockData>();
            var merged = new HashSet<int>();

            for (int i = 0; i < blocks.Count; i++)
            {
                if (merged.Contains(i)) continue;

                var current = blocks[i];
                var group = new List<AttributeBlockData> { current };
                merged.Add(i);

                for (int j = i + 1; j < blocks.Count; j++)
                {
                    if (merged.Contains(j)) continue;
                    var target = blocks[j];
                    if (CanMerge(current, target, config))
                    {
                        group.Add(target);
                        merged.Add(j);
                    }
                }

                if (group.Count >= config.MinMergeCount)
                    result.Add(CreateMergedBlock(group, config));
                else
                    result.AddRange(group);
            }

            return result;
        }

        private bool CanMerge(AttributeBlockData a, AttributeBlockData b, MergeConfig config)
        {
            switch (config.Criterion)
            {
                case MergeCriterion.Prefix:
                    return HasSamePrefix(a, b, config) && IsConsecutive(a, b, config);
                case MergeCriterion.Name:
                    return HasSameName(a, b) && IsConsecutive(a, b, config);
                case MergeCriterion.PrefixAndName:
                    return HasSamePrefix(a, b, config) && HasSameName(a, b) && IsConsecutive(a, b, config);
                case MergeCriterion.Size:
                    return SameAttr(a, b, "FM") && IsConsecutive(a, b, config);
                case MergeCriterion.Scale:
                    return SameAttr(a, b, "BLM") && IsConsecutive(a, b, config);
                default:
                    return false;
            }
        }

        private bool HasSamePrefix(AttributeBlockData a, AttributeBlockData b, MergeConfig config)
        {
            string xhA = a.GetAttribute("XH") ?? a.GetAttribute("TH") ?? "";
            string xhB = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "";
            return GetPrefix(xhA, config.GroupSymbol) == GetPrefix(xhB, config.GroupSymbol);
        }

        private string GetPrefix(string value, string separator)
        {
            int idx = value.LastIndexOf(separator);
            return idx > 0 ? value.Substring(0, idx) : value;
        }

        private bool HasSameName(AttributeBlockData a, AttributeBlockData b)
        {
            string tmA = a.GetAttribute("TM") ?? a.GetAttribute("NAME") ?? "";
            string tmB = b.GetAttribute("TM") ?? b.GetAttribute("NAME") ?? "";
            return tmA == tmB;
        }

        private bool SameAttr(AttributeBlockData a, AttributeBlockData b, string tag)
            => a.GetAttribute(tag) == b.GetAttribute(tag);

        private bool IsConsecutive(AttributeBlockData a, AttributeBlockData b, MergeConfig config)
        {
            if (!config.MergeOnlyConsecutive) return true;
            string xhA = a.GetAttribute("XH") ?? a.GetAttribute("TH") ?? "";
            string xhB = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "";
            int numA = ExtractTrailingNumber(xhA);
            int numB = ExtractTrailingNumber(xhB);
            return Math.Abs(numB - numA) == 1;
        }

        private int ExtractTrailingNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            string num = "";
            for (int i = s.Length - 1; i >= 0 && char.IsDigit(s[i]); i--)
                num = s[i] + num;
            return int.TryParse(num, out int result) ? result : 0;
        }

        private AttributeBlockData CreateMergedBlock(List<AttributeBlockData> group, MergeConfig config)
        {
            var first = group[0];
            var merged = new AttributeBlockData
            {
                BlockId = first.BlockId,
                BlockName = first.BlockName,
                Position = first.Position,
                SelectionOrder = first.SelectionOrder,
                Attributes = new Dictionary<string, string>(first.Attributes)
            };

            string xhA = first.GetAttribute("XH") ?? first.GetAttribute("TH") ?? "";
            string prefix = GetPrefix(xhA, config.GroupSymbol);
            int startNum = ExtractTrailingNumber(xhA);
            int endNum = ExtractTrailingNumber(group[group.Count - 1].GetAttribute("XH") ?? group[group.Count - 1].GetAttribute("TH") ?? "");
            merged.SetAttribute("XH", $"{prefix}{config.GroupSymbol}{startNum}{config.RangeSymbol}{endNum}");

            string tm = first.GetAttribute("TM") ?? first.GetAttribute("NAME") ?? "";
            if (!string.IsNullOrEmpty(tm))
                merged.SetAttribute("TM", $"{tm}（{startNum}{config.RangeSymbol}{endNum}）");

            return merged;
        }

        /// <summary>
        /// 生成目录实体列表（Polyline边框 + DBText文字）
        /// 用于预览和精确插入
        /// </summary>
        /// <param name="blocks">属性块数据列表</param>
        /// <param name="style">目录样式</param>
        /// <param name="insertPoint">插入位置</param>
        /// <param name="targetLayoutName">目标布局名称（null则使用模型空间）</param>
        /// <returns>CAD Entity列表</returns>
        public List<Entity> Generate(BlockDataResult blocks, CatalogStyle style, Point3d insertPoint, string targetLayoutName = null)
        {
            if (blocks == null || blocks.Blocks == null || blocks.Blocks.Count == 0)
                throw new ArgumentException("没有可生成目录的属性块");

            var entities = new List<Entity>();

            // 将 BlockData 转换为 AttributeBlockData 以便 ApplyMerge 处理
            var attrBlocks = blocks.Blocks.Select(b => new AttributeBlockData
            {
                BlockId = b.ObjectId,
                BlockName = b.BlockName,
                Attributes = b.Attributes?.ToDictionary(a => a.Tag, a => a.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Position = Autodesk.AutoCAD.Geometry.Point3d.Origin,
                SelectionOrder = 0
            }).ToList();

            // 应用合并策略
            var displayBlocks = attrBlocks;
            if (style.MergeStrategy == MergeStrategy.PrefixConsecutive)
            {
                displayBlocks = ApplyMerge(attrBlocks, new MergeConfig
                {
                    EnableMerge = true,
                    Criterion = MergeCriterion.Prefix,
                    GroupSymbol = "-",
                    RangeSymbol = "~"
                });
            }

            // 获取可见列
            var visibleCols = style.Columns.Where(c => c.Visible).ToList();
            int colCount = visibleCols.Count + 1; // +1 for sequence column

            // 计算列宽
            double[] colWidths = new double[colCount];
            colWidths[0] = SequenceColumnWidth; // 序号列宽
            for (int c = 1; c < colCount; c++)
                colWidths[c] = visibleCols[c - 1]?.Width ?? 40;

            // 计算总宽度和高度
            double totalWidth = colWidths.Sum();
            double headerHeight = style.HeaderHeight > 0 ? style.HeaderHeight : style.RowHeight * 1.2;
            int rowCount = displayBlocks.Count;
            double totalHeight = headerHeight + rowCount * style.RowHeight;

            double startX = insertPoint.X;
            double startY = insertPoint.Y;

            // 绘制外边框 (closed Polyline)
            var outerBorder = CreateRectangle(startX, startY, totalWidth, totalHeight);
            entities.Add(outerBorder);

            // 绘制内部网格线
            // 垂直分割线
            double x = startX;
            for (int c = 0; c <= colCount; c++)
            {
                if (c > 0) x += colWidths[c - 1];
                if (c > 0 && c < colCount) // 不画最左和最右（外边框已画）
                {
                    var vLine = CreateLine(x, startY, x, startY - totalHeight);
                    entities.Add(vLine);
                }
            }

            // 水平分割线（表头与内容之间）
            double y = startY - headerHeight;
            entities.Add(CreateLine(startX, y, startX + totalWidth, y));

            // 行分隔线
            double rowY = startY - headerHeight;
            for (int r = 0; r < rowCount; r++)
            {
                rowY -= style.RowHeight;
                if (r < rowCount - 1) // 最后一行不画分隔线（外边框）
                {
                    entities.Add(CreateLine(startX, rowY, startX + totalWidth, rowY));
                }
            }

            // 绘制表头文字
            double textHeight = style.RowHeight * 0.8;
            y = startY - textHeight * 0.5;

            // 序号列标题 - 居中显示，自动适应列宽
            entities.Add(CreateDBText("序号", new Point3d(startX + colWidths[0] / 2, y, 0), textHeight, true, colWidths[0]));

            x = startX;
            for (int c = 1; c < colCount; c++)
            {
                x += colWidths[c - 1];
                string header = visibleCols[c - 1]?.Header ?? visibleCols[c - 1]?.Tag ?? "";
                entities.Add(CreateDBText(header, new Point3d(x + colWidths[c] / 2, y, 0), textHeight, true, colWidths[c]));
            }

            // 绘制数据行 - 居中显示，自动适应
            double rowTextY = startY - headerHeight - textHeight * 0.5;
            int seqNum = 1;

            foreach (var block in displayBlocks)
            {
                x = startX;

                // 序号
                string seqStr = FormatSeqNum(seqNum++, style.SeqFormat);
                entities.Add(CreateDBText(seqStr, new Point3d(x + colWidths[0] / 2, rowTextY, 0), textHeight, false, colWidths[0]));

                // 属性列
                for (int c = 1; c < colCount; c++)
                {
                    x += colWidths[c - 1];
                    var tag = visibleCols[c - 1]?.Tag;
                    string value = tag != null ? GetAttrBlockAttributeMulti(block, tag) : "";
                    entities.Add(CreateDBText(value, new Point3d(x + colWidths[c] / 2, rowTextY, 0), textHeight, false, colWidths[c]));
                }

                rowTextY -= style.RowHeight;
            }

            return entities;
        }

        /// <summary>
        /// 创建居中的单行文字（自动适应列宽）
        /// </summary>
        private DBText CreateDBText(string text, Point3d position, double height, bool bold, double colWidth = 0)
        {
            if (string.IsNullOrEmpty(text)) text = "";

            double actualHeight = height;

            // 如果有列宽限制，自动调整字体大小
            if (colWidth > 0)
            {
                double textWidth = text.Length * height * 0.7;
                if (textWidth > colWidth * 0.9)
                {
                    // 缩小字体以适应列宽
                    actualHeight = height * (colWidth * 0.9) / textWidth;
                    actualHeight = Math.Max(actualHeight, 1.0); // 最小字高 1.0
                }
            }

            var dbText = new DBText
            {
                TextString = text,
                Position = position,
                Height = actualHeight,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextBottom,
                AlignmentPoint = position
            };
            if (bold)
            {
                dbText.WidthFactor = 1.2;
            }
            return dbText;
        }

        /// <summary>
        /// 创建矩形边框（闭合Polyline）
        /// </summary>
        private Polyline CreateRectangle(double x, double y, double width, double height)
        {
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(x, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x + width, y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x + width, y - height), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x, y - height), 0, 0, 0);
            pl.Closed = true;
            return pl;
        }

        /// <summary>
        /// 创建直线
        /// </summary>
        private Line CreateLine(double x1, double y1, double x2, double y2)
        {
            return new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));
        }

        /// <summary>
        /// 多标签查找 - 尝试多个标签名，返回第一个找到的非空值
        /// 支持中英文标签别名映射
        /// </summary>
        private string GetBlockAttributeMulti(BlockData block, string primaryTag)
        {
            // 标签别名映射：列定义标签 -> 实际可能存在的标签列表
            var tagAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "XH", new[] { "XH", "TH", "BH", "图号", "编号", "DRAWING_NO", "图纸编号" } },
                { "TH", new[] { "TH", "XH", "BH", "图号", "编号", "DRAWING_NO", "图纸编号" } },
                { "TM", new[] { "TM", "图名", "NAME", "DRAWINGNAME", "TNAME", "图纸名称" } },
                { "BLM", new[] { "BLM", "BL", "比例", "SCALE", "缩放比例" } },
                { "FM", new[] { "FM", "幅面", "SIZE", "图纸幅面" } },
            };

            // 尝试主标签
            var val = block.GetAttribute(primaryTag);
            if (!string.IsNullOrEmpty(val)) return val;

            // 尝试别名映射
            if (tagAliases.TryGetValue(primaryTag, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    val = block.GetAttribute(alias);
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }

            // 尝试直接遍历所有属性（兜底）
            foreach (var attr in block.Attributes)
            {
                if (!string.IsNullOrEmpty(attr.Value)) return attr.Value;
            }

            return "";
        }

        /// <summary>
        /// 多标签查找 - 用于 AttributeBlockData 类型
        /// </summary>
        private string GetAttrBlockAttributeMulti(AttributeBlockData block, string primaryTag)
        {
            var tagAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "XH", new[] { "XH", "TH", "BH", "图号", "编号", "DRAWING_NO", "图纸编号" } },
                { "TH", new[] { "TH", "XH", "BH", "图号", "编号", "DRAWING_NO", "图纸编号" } },
                { "TM", new[] { "TM", "图名", "NAME", "DRAWINGNAME", "TNAME", "图纸名称" } },
                { "BLM", new[] { "BLM", "BL", "比例", "SCALE", "缩放比例" } },
                { "FM", new[] { "FM", "幅面", "SIZE", "图纸幅面" } },
            };

            // 尝试主标签
            var val = block.GetAttribute(primaryTag);
            if (!string.IsNullOrEmpty(val)) return val;

            // 尝试别名映射
            if (tagAliases.TryGetValue(primaryTag, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    val = block.GetAttribute(alias);
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }

            return "";
        }

    }
}
