using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 块属性
    /// </summary>
    public class BlockAttribute
    {
        public string Tag { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// 块数据
    /// </summary>
    public class BlockData
    {
        public string BlockName { get; set; }
        public ObjectId ObjectId { get; set; }
        public List<BlockAttribute> Attributes { get; set; } = new List<BlockAttribute>();

        public string GetAttribute(string tag)
        {
            var attr = Attributes?.FirstOrDefault(a => a.Tag.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
            return attr?.Value;
        }
    }

    /// <summary>
    /// 块数据提取结果
    /// </summary>
    public class BlockDataResult
    {
        public List<BlockData> Blocks { get; set; } = new List<BlockData>();
        public List<string> AllTags { get; set; } = new List<string>();
        public List<string> BlockNames { get; set; } = new List<string>();
        public List<string> LayerNames { get; set; } = new List<string>();
    }
}