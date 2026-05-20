using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 图框智能选择器
    /// 支持按布局、图层、块名等多种方式选择图框属性块
    /// </summary>
    public class FrameSelector
    {
        private readonly Editor _editor;
        private readonly Document _doc;
        private readonly Database _db;

        public FrameSelector()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            _editor = _doc.Editor;
            _db = _doc.Database;
        }

        /// <summary>
        /// 按配置选择图框
        /// </summary>
        public ExtractionResult SelectFrames(FrameSelectionConfig config)
        {
            var result = new ExtractionResult();
            result.Blocks = new List<AttributeBlockData>();
            result.AllTags = new List<string>();

            switch (config.Mode)
            {
                case NumberingMode.Global:
                    // 框选或全部提取
                    result = SelectByBoxOrAll(config);
                    break;

                case NumberingMode.ByLayout:
                    // 按布局选择
                    result = SelectByLayouts(config);
                    break;

                case NumberingMode.ByType:
                    // 按图纸类型选择
                    result = SelectByType(config);
                    break;

                case NumberingMode.CustomPrefix:
                    // 自定义前缀模式，使用框选
                    result = SelectByBoxOrAll(config);
                    break;
            }

            return result;
        }

        /// <summary>
        /// 框选或全部提取
        /// </summary>
        private ExtractionResult SelectByBoxOrAll(FrameSelectionConfig config)
        {
            var result = new ExtractionResult();
            var blocks = new List<AttributeBlockData>();
            var allTags = new HashSet<string>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                // 获取块引用
                var blockRefs = GetBlockReferences(tr, config);

                foreach (var br in blockRefs)
                {
                    var blockData = ExtractBlockData(br, tr, blocks.Count);
                    if (blockData != null)
                    {
                        blocks.Add(blockData);
                        foreach (var tag in blockData.Attributes.Keys)
                        {
                            allTags.Add(tag);
                        }
                    }
                }

                tr.Commit();
            }

            result.Blocks = blocks;
            result.AllTags = allTags.ToList();
            return result;
        }

        /// <summary>
        /// 按布局选择（每个布局提取一个图框）
        /// </summary>
        private ExtractionResult SelectByLayouts(FrameSelectionConfig config)
        {
            var result = new ExtractionResult();
            var blocks = new List<AttributeBlockData>();
            var allTags = new HashSet<string>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                // 获取所有布局
                var layouts = GetLayouts(tr, config.SelectedLayouts);

                foreach (var layout in layouts)
                {
                    // 在每个布局中查找图框
                    var blockRef = FindFrameInLayout(tr, layout, config);
                    if (blockRef != null)
                    {
                        var blockData = ExtractBlockData(blockRef, tr, blocks.Count);
                        if (blockData != null)
                        {
                            // 记录布局名称
                            blockData.LayoutName = layout.LayoutName;
                            blocks.Add(blockData);
                            foreach (var tag in blockData.Attributes.Keys)
                            {
                                allTags.Add(tag);
                            }
                        }
                    }
                }

                tr.Commit();
            }

            result.Blocks = blocks;
            result.AllTags = allTags.ToList();
            return result;
        }

        /// <summary>
        /// 按图纸类型选择（根据图名属性分类）
        /// </summary>
        private ExtractionResult SelectByType(FrameSelectionConfig config)
        {
            var result = SelectByBoxOrAll(config);

            // 如果指定了类型过滤，则筛选
            if (!string.IsNullOrEmpty(config.FilterByType))
            {
                result.Blocks = result.Blocks
                    .Where(b => b.GetAttribute("图名")?.Contains(config.FilterByType) == true
                             || b.GetAttribute("NAME")?.Contains(config.FilterByType) == true)
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// 获取所有布局
        /// </summary>
        private List<Layout> GetLayouts(Transaction tr, List<string> selectedLayouts)
        {
            var layouts = new List<Layout>();

            var layoutDict = _db.LayoutDictionaryId;
            var dict = tr.GetObject(layoutDict, OpenMode.ForRead) as DBDictionary;

            if (dict == null) return layouts;

            foreach (var entry in dict)
            {
                // 排除模型空间
                if (entry.Key == "Model") continue;

                // 如果指定了布局列表，则只选择指定的
                if (selectedLayouts != null && selectedLayouts.Count > 0)
                {
                    if (!selectedLayouts.Contains(entry.Key)) continue;
                }

                var layout = tr.GetObject(entry.Value, OpenMode.ForRead) as Layout;
                if (layout != null)
                {
                    layouts.Add(layout);
                }
            }

            return layouts;
        }

        /// <summary>
        /// 在布局中查找图框块
        /// </summary>
        private BlockReference FindFrameInLayout(Transaction tr, Layout layout, FrameSelectionConfig config)
        {
            var bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
            if (bt == null) return null;

            var btrId = layout.BlockTableRecordId;
            var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return null;

            // 查找属性块
            foreach (var objId in btr)
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                var br = entity as BlockReference;
                if (br == null) continue;

                // 检查是否有属性
                if (br.AttributeCollection.Count == 0) continue;

                // 过滤图层
                if (!string.IsNullOrEmpty(config.FilterLayer))
                {
                    var layerTable = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    var layer = tr.GetObject(br.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    if (layer != null && layer.Name != config.FilterLayer) continue;
                }

                // 过滤块名
                if (!string.IsNullOrEmpty(config.FilterBlockName))
                {
                    var blockDef = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (blockDef != null && blockDef.Name != config.FilterBlockName) continue;
                }

                return br;
            }

            return null;
        }

        /// <summary>
        /// 获取块引用列表
        /// </summary>
        private List<BlockReference> GetBlockReferences(Transaction tr, FrameSelectionConfig config)
        {
            var blockRefs = new List<BlockReference>();

            // 获取当前空间的块表记录
            var btrId = _db.CurrentSpaceId;
            var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return blockRefs;

            foreach (var objId in btr)
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                var br = entity as BlockReference;
                if (br == null) continue;

                // 只选择属性块
                if (config.OnlyAttributeBlocks && br.AttributeCollection.Count == 0) continue;

                // 过滤图层
                if (!string.IsNullOrEmpty(config.FilterLayer))
                {
                    var layer = tr.GetObject(br.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    if (layer != null && !layer.Name.Contains(config.FilterLayer)) continue;
                }

                // 过滤块名
                if (!string.IsNullOrEmpty(config.FilterBlockName))
                {
                    var blockDef = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (blockDef != null && !blockDef.Name.Contains(config.FilterBlockName)) continue;
                }

                blockRefs.Add(br);
            }

            return blockRefs;
        }

        /// <summary>
        /// 提取块数据
        /// </summary>
        private AttributeBlockData ExtractBlockData(BlockReference br, Transaction tr, int selectionOrder)
        {
            var data = new AttributeBlockData();
            data.BlockId = br.ObjectId;
            data.BlockName = br.Name;
            data.Position = br.Position;
            data.SelectionOrder = selectionOrder;
            data.Attributes = new Dictionary<string, string>();

            // 获取块定义名称
            var btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr != null)
            {
                data.BlockName = btr.Name;
            }

            // 提取属性
            foreach (ObjectId arId in br.AttributeCollection)
            {
                var ar = tr.GetObject(arId, OpenMode.ForRead) as AttributeReference;
                if (ar == null) continue;

                string tag = ar.Tag.Trim().ToUpperInvariant();
                data.Attributes[tag] = ar.TextString;
            }

            return data.Attributes.Count > 0 ? data : null;
        }

        /// <summary>
        /// 获取所有可用图层名称
        /// </summary>
        public List<string> GetAllLayers()
        {
            var layers = new List<string>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var layerTable = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (layerTable != null)
                {
                    foreach (var entry in layerTable)
                    {
                        var layer = tr.GetObject(entry, OpenMode.ForRead) as LayerTableRecord;
                        if (layer != null)
                        {
                            layers.Add(layer.Name);
                        }
                    }
                }
                tr.Commit();
            }

            return layers;
        }

        /// <summary>
        /// 获取所有属性块名称
        /// </summary>
        public List<string> GetAllBlockNamesWithAttributes()
        {
            var blockNames = new List<string>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return blockNames;

                foreach (var entry in bt)
                {
                    var btr = tr.GetObject(entry, OpenMode.ForRead) as BlockTableRecord;
                    if (btr == null) continue;

                    // 检查是否有属性定义
                    bool hasAttributes = false;
                    foreach (var objId in btr)
                    {
                        var attrDef = tr.GetObject(objId, OpenMode.ForRead) as AttributeDefinition;
                        if (attrDef != null)
                        {
                            hasAttributes = true;
                            break;
                        }
                    }

                    if (hasAttributes)
                    {
                        blockNames.Add(btr.Name);
                    }
                }

                tr.Commit();
            }

            return blockNames;
        }

        /// <summary>
        /// 获取所有布局名称
        /// </summary>
        public List<string> GetAllLayoutNames()
        {
            var layoutNames = new List<string>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var layoutDict = _db.LayoutDictionaryId;
                var dict = tr.GetObject(layoutDict, OpenMode.ForRead) as DBDictionary;

                if (dict != null)
                {
                    foreach (var entry in dict)
                    {
                        if (entry.Key != "Model")
                        {
                            layoutNames.Add(entry.Key);
                        }
                    }
                }

                tr.Commit();
            }

            return layoutNames;
        }

        /// <summary>
        /// 获取图纸类型列表（从已有块的图名属性提取）
        /// </summary>
        public List<string> GetDrawingTypes(List<AttributeBlockData> blocks)
        {
            var types = new HashSet<string>();

            foreach (var block in blocks)
            {
                var typeName = block.GetAttribute("图名") ?? block.GetAttribute("NAME") ?? block.GetAttribute("DRAWINGNAME");
                if (!string.IsNullOrEmpty(typeName))
                {
                    // 提取类型关键词
                    if (typeName.Contains("平面")) types.Add("平面图");
                    if (typeName.Contains("立面")) types.Add("立面图");
                    if (typeName.Contains("剖面")) types.Add("剖面图");
                    if (typeName.Contains("详图")) types.Add("详图");
                    if (typeName.Contains("大样")) types.Add("大样图");
                    else types.Add(typeName);
                }
            }

            return types.ToList();
        }

        /// <summary>
        /// 加载图层和块名过滤选项到UI控件
        /// </summary>
        public void LoadFilterOptionsToControls(System.Windows.Forms.ComboBox layerCombo, System.Windows.Forms.ComboBox blockCombo)
        {
            var layers = GetAllLayers();
            layerCombo.Items.Clear();
            layerCombo.Items.Add("(全部图层)");
            foreach (var layer in layers)
            {
                layerCombo.Items.Add(layer);
            }
            layerCombo.SelectedIndex = 0;

            var blockNames = GetAllBlockNamesWithAttributes();
            blockCombo.Items.Clear();
            blockCombo.Items.Add("(全部块名)");
            foreach (var name in blockNames)
            {
                blockCombo.Items.Add(name);
            }
            blockCombo.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// 扩展 AttributeBlockData 添加布局名称
    /// </summary>
    public partial class AttributeBlockData
    {
        /// <summary>
        /// 布局名称（用于按布局选择时记录）
        /// </summary>
        public string LayoutName { get; set; } = "";
    }
}