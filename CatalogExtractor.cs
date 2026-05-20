using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 块目录提取器 - 核心提取逻辑
    /// </summary>
    public class CatalogExtractor
    {
        private readonly List<Regex> _includePatterns = new List<Regex>();
        private readonly List<string> _excludePatterns = new List<string>();
        private readonly List<string> _layerFilters = new List<string>();
        private string _filterBlockNames;

        /// <summary>
        /// 设置要匹配的块名（支持正则表达式，逗号分隔多个）
        /// </summary>
        public string FilterBlockNames
        {
            get => _filterBlockNames;
            set
            {
                _filterBlockNames = value;
                _includePatterns.Clear();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    foreach (var pattern in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = pattern.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            try
                            {
                                _includePatterns.Add(new Regex(trimmed, RegexOptions.IgnoreCase));
                            }
                            catch
                            {
                                // 如果不是有效正则，当作通配符处理
                                _includePatterns.Add(new Regex("^" + Regex.Escape(trimmed).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置要排除的块名列表（逗号分隔）
        /// </summary>
        public string ExcludeBlockNames { get; set; }

        /// <summary>
        /// 设置图层过滤器（逗号分隔）
        /// </summary>
        public string LayerFilters { get; set; }

        /// <summary>
        /// 从选择集提取块属性数据
        /// </summary>
        public BlockDataResult ExtractFromSelection(SelectionSet selectionSet)
        {
            var result = new BlockDataResult();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;

            ParseExcludePatterns();
            ParseLayerFilters();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in selectionSet)
                {
                    if (so == null) continue;

                    var blockRef = tr.GetObject(so.ObjectId, OpenMode.ForRead) as BlockReference;
                    if (blockRef == null) continue;

                    var blockData = ExtractBlockData(blockRef, tr);
                    if (blockData != null)
                    {
                        result.Blocks.Add(blockData);

                        // 收集所有标签
                        foreach (var attr in blockData.Attributes)
                        {
                            if (!result.AllTags.Contains(attr.Tag, StringComparer.OrdinalIgnoreCase))
                                result.AllTags.Add(attr.Tag);
                        }

                        // 收集块名
                        if (!result.BlockNames.Contains(blockData.BlockName, StringComparer.OrdinalIgnoreCase))
                            result.BlockNames.Add(blockData.BlockName);

                        // 收集图层名
                        var layerName = GetLayerName(blockRef, tr);
                        if (!string.IsNullOrEmpty(layerName) && !result.LayerNames.Contains(layerName, StringComparer.OrdinalIgnoreCase))
                            result.LayerNames.Add(layerName);
                    }
                }
                tr.Commit();
            }

            return result;
        }

        /// <summary>
        /// 分析指定ObjectId列表的块类型
        /// </summary>
        public Dictionary<string, List<string>> AnalyzeBlockTypes(List<ObjectId> objectIds)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;

            ParseExcludePatterns();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var objId in objectIds)
                {
                    var blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                    if (blockRef == null) continue;

                    var btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (btr == null) continue;

                    var blockName = btr.Name;

                    // 检查是否在排除列表中
                    if (IsExcluded(blockName)) continue;

                    // 检查是否匹配include模式
                    if (_includePatterns.Count > 0 && !IsIncluded(blockName)) continue;

                    // 收集属性标签
                    var tags = new List<string>();
                    CollectAttributeTags(blockRef, tr, tags);

                    if (tags.Count > 0)
                    {
                        if (!result.ContainsKey(blockName))
                            result[blockName] = tags;
                        else
                        {
                            // 合并标签
                            foreach (var tag in tags)
                            {
                                if (!result[blockName].Contains(tag, StringComparer.OrdinalIgnoreCase))
                                    result[blockName].Add(tag);
                            }
                        }
                    }
                }
                tr.Commit();
            }

            return result;
        }

        /// <summary>
        /// 使用Editor选择INSERT实体
        /// </summary>
        public SelectionSet SelectBlocks(Editor ed)
        {
            var filterValues = new List<TypedValue>
            {
                new TypedValue(0, "INSERT")
            };

            if (_layerFilters.Count > 0)
            {
                var layerValues = _layerFilters.Select(l => new TypedValue(8, l)).ToArray();
                filterValues.AddRange(layerValues);
            }

            var filter = new SelectionFilter(filterValues.ToArray());
            var pr = ed.GetSelection(filter);
            return pr.Status == PromptStatus.OK ? pr.Value : null;
        }

        private BlockData ExtractBlockData(BlockReference blockRef, Transaction tr)
        {
            var btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return null;

            var blockName = btr.Name;

            // 检查排除列表
            if (IsExcluded(blockName)) return null;

            // 检查include模式
            if (_includePatterns.Count > 0 && !IsIncluded(blockName)) return null;

            // 检查图层过滤
            if (_layerFilters.Count > 0)
            {
                var layerName = GetLayerName(blockRef, tr);
                if (string.IsNullOrEmpty(layerName) || !_layerFilters.Any(l => layerName.Equals(l, StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            var data = new BlockData
            {
                BlockName = blockName,
                ObjectId = blockRef.Id
            };

            // 提取属性
            CollectAttributeTags(blockRef, tr, data.Attributes, true);

            // 过滤无属性块
            return data.Attributes.Count > 0 ? data : null;
        }

        private void CollectAttributeTags(BlockReference blockRef, Transaction tr, List<string> tags, bool extractValues = false)
        {
            foreach (ObjectId arId in blockRef.AttributeCollection)
            {
                var ar = tr.GetObject(arId, OpenMode.ForRead) as AttributeReference;
                if (ar != null && !string.IsNullOrEmpty(ar.Tag))
                {
                    var tag = ar.Tag.Trim().ToUpperInvariant();
                    if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        tags.Add(tag);
                }
            }

            // 如果没有属性，尝试从块定义获取常量属性
            if (tags.Count == 0)
            {
                var btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId defId in btr)
                    {
                        var ad = tr.GetObject(defId, OpenMode.ForRead) as AttributeDefinition;
                        if (ad != null && ad.Constant)
                        {
                            var tag = ad.Tag.Trim().ToUpperInvariant();
                            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                                tags.Add(tag);
                        }
                    }
                }
            }
        }

        private void CollectAttributeTags(BlockReference blockRef, Transaction tr, List<BlockAttribute> attributes, bool extractValues)
        {
            foreach (ObjectId arId in blockRef.AttributeCollection)
            {
                var ar = tr.GetObject(arId, OpenMode.ForRead) as AttributeReference;
                if (ar != null && !string.IsNullOrEmpty(ar.Tag))
                {
                    attributes.Add(new BlockAttribute
                    {
                        Tag = ar.Tag.Trim().ToUpperInvariant(),
                        Value = ar.TextString ?? ""
                    });
                }
            }

            // 如果没有属性，尝试从块定义获取常量属性
            if (attributes.Count == 0)
            {
                var btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId defId in btr)
                    {
                        var ad = tr.GetObject(defId, OpenMode.ForRead) as AttributeDefinition;
                        if (ad != null && ad.Constant)
                        {
                            attributes.Add(new BlockAttribute
                            {
                                Tag = ad.Tag.Trim().ToUpperInvariant(),
                                Value = ad.TextString ?? ""
                            });
                        }
                    }
                }
            }
        }

        private string GetLayerName(BlockReference blockRef, Transaction tr)
        {
            var ent = tr.GetObject(blockRef.Id, OpenMode.ForRead) as Entity;
            return ent?.Layer ?? "";
        }

        private void ParseExcludePatterns()
        {
            _excludePatterns.Clear();
            if (!string.IsNullOrWhiteSpace(ExcludeBlockNames))
            {
                foreach (var pattern in ExcludeBlockNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = pattern.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _excludePatterns.Add(trimmed);
                }
            }
        }

        private void ParseLayerFilters()
        {
            _layerFilters.Clear();
            if (!string.IsNullOrWhiteSpace(LayerFilters))
            {
                foreach (var layer in LayerFilters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = layer.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _layerFilters.Add(trimmed);
                }
            }
        }

        private bool IsExcluded(string blockName)
        {
            return _excludePatterns.Any(p => blockName.Equals(p, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsIncluded(string blockName)
        {
            if (_includePatterns.Count == 0) return true;
            return _includePatterns.Any(r => r.IsMatch(blockName));
        }
    }
}