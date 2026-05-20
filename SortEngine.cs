using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 排序引擎，支持多种排序规则及几何容差模糊排序
    /// </summary>
    public class SortEngine
    {
        public enum SortType
        {
            SelectionOrder,           // 选择顺序
            NumericOrder,            // 按属性值自然数排序
            LeftRight_TopBottom,    // 左右上下（Z形）
            TopBottom_LeftRight,      // 上下左右（S形）
        }

        /// <summary>
        /// 对属性块列表排序
        /// </summary>
        /// <param name="blocks">待排序的属性块列表</param>
        /// <param name="type">排序类型</param>
        /// <param name="tolerance">几何容差阈值（用于模糊判断同一排），默认500</param>
        /// <param name="isReverse">是否反序输出</param>
        /// <returns>排序后的列表</returns>
        public List<AttributeBlockData> Sort(List<AttributeBlockData> blocks, SortType type, double tolerance = 500.0, bool isReverse = false)
        {
            if (blocks == null || blocks.Count <= 1) return blocks ?? new List<AttributeBlockData>();

            List<AttributeBlockData> sorted;

            switch (type)
            {
                case SortType.SelectionOrder:
                    sorted = blocks.OrderBy(b => b.SelectionOrder).ToList();
                    break;
                case SortType.NumericOrder:
                    sorted = blocks.OrderBy(b => ExtractSortKey(b)).ToList();
                    break;
                case SortType.TopBottom_LeftRight:
                    // 上下左右：先按Y分组（容差），组内按X排序
                    sorted = blocks.OrderBy(b => GetRowGroup(b.Position.Y, tolerance))
                                   .ThenBy(b => b.Position.X)
                                   .ToList();
                    break;
                case SortType.LeftRight_TopBottom:
                    // 左右上下：先按X分组（容差），组内按Y排序
                    sorted = blocks.OrderBy(b => GetColGroup(b.Position.X, tolerance))
                                   .ThenByDescending(b => b.Position.Y)
                                   .ToList();
                    break;
                default:
                    sorted = new List<AttributeBlockData>(blocks);
                    break;
            }

            // 如果需要反序（反向排列，但组内顺序保持）
            if (isReverse)
            {
                sorted.Reverse();
            }

            // 重新设置选择顺序
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].SelectionOrder = i;
            }

            return sorted;
        }

        /// <summary>
        /// 获取行分组编号（用于上下左右排序）
        /// Y坐标从大到小排列，所以用负数使Y大的组号小（排前面）
        /// </summary>
        private double GetRowGroup(double y, double tolerance)
        {
            // 容差分组：Y坐标除以tolerance，向下取整
            // 负号使Y大的（在上方）组号小，排序时排前面
            return -Math.Floor(y / tolerance);
        }

        /// <summary>
        /// 获取列分组编号（用于左右上下排序）
        /// X坐标从小到大排列，所以组号就是X/tolerance
        /// </summary>
        private double GetColGroup(double x, double tolerance)
        {
            return Math.Floor(x / tolerance);
        }

        /// <summary>
        /// 提取用于数值排序的键（处理 "建施-01" → 1 这样的格式）
        /// </summary>
        private int ExtractSortKey(AttributeBlockData block)
        {
            string xh = block.GetAttribute("XH") ?? block.GetAttribute("TH") ?? "";
            var nums = ExtractNumbers(xh);
            return nums.Count > 0 ? nums[0] : int.MaxValue;
        }

        /// <summary>
        /// 按图号数字递增排序（兼容旧名称）
        /// </summary>
        public List<AttributeBlockData> SortByXH(List<AttributeBlockData> blocks)
        {
            return Sort(blocks, SortType.NumericOrder);
        }

        /// <summary>
        /// 从字符串中提取所有数字
        /// </summary>
        private List<int> ExtractNumbers(string s)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(s)) return result;

            string numStr = "";
            foreach (char c in s)
            {
                if (char.IsDigit(c))
                    numStr += c;
                else if (numStr.Length > 0)
                {
                    if (int.TryParse(numStr, out int num))
                        result.Add(num);
                    numStr = "";
                }
            }

            if (numStr.Length > 0 && int.TryParse(numStr, out int lastNum))
                result.Add(lastNum);

            return result;
        }
    }
}
