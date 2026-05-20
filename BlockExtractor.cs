using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 属性块数据
    /// </summary>
    public partial class AttributeBlockData
    {
        public ObjectId BlockId { get; set; }
        public string BlockName { get; set; } = "";
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Autodesk.AutoCAD.Geometry.Point3d Position { get; set; }
        public int SelectionOrder { get; set; }

        public string GetAttribute(string tag)
        {
            return Attributes.TryGetValue(tag, out string value) ? value : null;
        }

        public void SetAttribute(string tag, string value)
        {
            Attributes[tag] = value;
        }
    }

    /// <summary>
    /// 提取结果
    /// </summary>
    public class ExtractionResult
    {
        public List<AttributeBlockData> Blocks { get; set; } = new List<AttributeBlockData>();
        public List<string> AllTags { get; set; } = new List<string>(); // 所有发现的属性标签
    }

    /// <summary>
    /// 属性块提取器
    /// </summary>
    public class BlockExtractor
    {
        /// <summary>
        /// 从选择集提取属性块
        /// </summary>
        public ExtractionResult ExtractFromSelection(SelectionSet ss)
        {
            var result = new ExtractionResult();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;

            int order = 0;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in ss)
                {
                    if (so == null) continue;

                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as BlockReference;
                    if (ent == null) continue;

                    var data = ExtractBlockData(ent, tr, order++);
                    if (data != null)
                    {
                        result.Blocks.Add(data);
                        // 收集所有标签
                        foreach (var key in data.Attributes.Keys)
                        {
                            if (!result.AllTags.Contains(key, StringComparer.OrdinalIgnoreCase))
                                result.AllTags.Add(key);
                        }
                    }
                }
                tr.Commit();
            }

            return result;
        }

        /// <summary>
        /// 框选提取属性块
        /// </summary>
        public ExtractionResult ExtractInRange(Autodesk.AutoCAD.Geometry.Point3d p1, Autodesk.AutoCAD.Geometry.Point3d p2)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return new ExtractionResult();

            var ed = doc.Editor;
            var pr = ed.SelectWindow(p1, p2);
            if (pr.Status != PromptStatus.OK || pr.Value == null)
                return new ExtractionResult();

            return ExtractFromSelection(pr.Value);
        }

        /// <summary>
        /// 提示用户选择属性块
        /// </summary>
        public ExtractionResult PromptSelectBlocks(string prompt = "选择属性块")
        {
            var result = new ExtractionResult();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;

            var ed = doc.Editor;
            var opt = new TypedValue(0, "INSERT");
            var filter = new SelectionFilter(new TypedValue[] { opt });
            var pr = ed.GetSelection(filter);

            if (pr.Status != PromptStatus.OK || pr.Value == null)
                return new ExtractionResult();

            return ExtractFromSelection(pr.Value);
        }

        private AttributeBlockData ExtractBlockData(BlockReference br, Transaction tr, int order)
        {
            // 检查是否有属性
            var btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return null;

            var data = new AttributeBlockData
            {
                BlockId = br.Id,
                BlockName = btr.Name,
                Position = br.Position,
                SelectionOrder = order
            };

            // 提取属性
            foreach (ObjectId arId in br.AttributeCollection)
            {
                var ar = tr.GetObject(arId, OpenMode.ForRead) as AttributeReference;
                if (ar != null && !string.IsNullOrEmpty(ar.Tag))
                {
                    data.Attributes[ar.Tag.Trim().ToUpperInvariant()] = ar.TextString ?? "";
                }
            }

            // 如果没有属性，可能是嵌套块，尝试从块定义获取常量属性
            if (data.Attributes.Count == 0)
            {
                foreach (ObjectId defId in btr)
                {
                    var ad = tr.GetObject(defId, OpenMode.ForRead) as AttributeDefinition;
                    if (ad != null && ad.Constant)
                    {
                        data.Attributes[ad.Tag.Trim().ToUpperInvariant()] = ad.TextString ?? "";
                    }
                }
            }

            // 必须有属性才返回
            return data.Attributes.Count > 0 ? data : null;
        }
    }
}
