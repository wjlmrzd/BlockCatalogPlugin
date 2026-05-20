using System;
using System.Collections.Generic;
using System.Text;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 序号生成器，支持多种格式模板
    /// </summary>
    public class NumberSequence
    {
        private static readonly string[] ChineseLower = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        private static readonly string[] ChineseCircle = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫", "⑬", "⑭", "⑮", "⑯", "⑰", "⑱", "⑲", "⑳" };
        private static readonly string[] ChineseCircleAlt = { "㊀", "㊁", "㊂", "㊃", "㊄", "㊅", "㊆", "㊇", "㊈", "㊉", "㊊", "㊋", "㊌", "㊍", "㊎", "㊏", "㊐", "㊑", "㊒", "㊓" };
        private static readonly string[] LetterLowerArr = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" };
        private static readonly string[] LetterUpperArr = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

        public string Template { get; set; } = "{n}";
        public int StartNum { get; set; } = 1;
        public int Step { get; set; } = 1;
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";

        /// <summary>
        /// 当前索引（用于分组递增时跟踪）
        /// </summary>
        public int CurrentIndex { get; private set; } = 0;

        /// <summary>
        /// 重置索引
        /// </summary>
        public void Reset()
        {
            CurrentIndex = 0;
        }

        /// <summary>
        /// 前进到下一个索引
        /// </summary>
        public void Next()
        {
            CurrentIndex++;
        }

        /// <summary>
        /// 根据索引生成序号字符串
        /// </summary>
        /// <param name="index">从0开始的索引</param>
        /// <returns>格式化后的序号字符串</returns>
        public string Format(int index)
        {
            int num = StartNum + index * Step;
            string result = Template;

            // {n} - 阿拉伯数字
            result = result.Replace("{n}", num.ToString());
            // {nn} - 补零两位
            result = result.Replace("{nn}", num.ToString("D2"));
            // {nnn} - 补零三位
            result = result.Replace("{nnn}", num.ToString("D3"));
            // {nnnn} - 补零四位
            result = result.Replace("{nnnn}", num.ToString("D4"));

            // {c} - 中文数字（圆圈风格：①）
            result = result.Replace("{c}", IndexToCircle(num));
            // {c1} - 中文小写（一）
            result = result.Replace("{c1}", IndexToChinese1(num));
            // {c2} - 中文大写（壹）
            result = result.Replace("{c2}", IndexToChinese2(num));
            // {cc} - 圆圈数字
            result = result.Replace("{cc}", IndexToCircle(num));
            // {cc2} - 圆圈中文
            result = result.Replace("{cc2}", IndexToCircleAlt(num));

            // {a} - 字母小写
            result = result.Replace("{a}", IndexToLetter(index, true));
            // {A} - 字母大写
            result = result.Replace("{A}", IndexToLetter(index, false));

            // 应用前缀后缀
            return Prefix + result + Suffix;
        }

        /// <summary>
        /// 预置格式工厂方法
        /// </summary>
        public static NumberSequence Create(string template, int startNum = 1, int step = 1)
        {
            return new NumberSequence
            {
                Template = template,
                StartNum = startNum,
                Step = step
            };
        }

        /// <summary>
        /// 常用预置格式
        /// </summary>
        public static NumberSequence Preset(string type)
        {
            return type switch
            {
                "1,2,3" => Create("{n}"),
                "01,02,03" => Create("{nn}"),
                "001,002,003" => Create("{nnn}"),
                "①,②,③" => Create("{cc}"),
                "一,二,三" => Create("{c1}"),
                "A,B,C" => Create("{A}"),
                "a,b,c" => Create("{a}"),
                "图{n}" => Create("图{n}"),
                "DL-{nn}" => Create("DL-{nn}"),
                "第{c1}" => Create("第{c1}"),
                "平面图（{c1}）" => Create("平面图（{c1}）"),
                _ => Create("{n}")
            };
        }

        private string IndexToCircle(int num)
        {
            if (num >= 1 && num <= 20) return ChineseCircle[num - 1];
            if (num <= 0) return ChineseCircle[0];
            return num.ToString();
        }

        private string IndexToCircleAlt(int num)
        {
            if (num >= 1 && num <= 20) return ChineseCircleAlt[num - 1];
            if (num <= 0) return ChineseCircleAlt[0];
            return num.ToString();
        }

        private string IndexToChinese1(int num)
        {
            if (num <= 0) return ChineseLower[0];
            if (num <= 10) return ChineseLower[num];
            if (num < 20) return "十" + (num == 10 ? "" : ChineseLower[num - 10]);
            if (num == 10) return "十";
            if (num < 100) return ChineseLower[num / 10] + "十" + (num % 10 == 0 ? "" : ChineseLower[num % 10]);
            return num.ToString();
        }

        private string IndexToChinese2(int num)
        {
            string[] cap = { "", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖", "拾" };
            if (num <= 0) return "零";
            if (num <= 10) return cap[num];
            if (num < 100) return cap[num / 10] + "拾" + (num % 10 == 0 ? "" : cap[num % 10]);
            return num.ToString();
        }

        private string IndexToLetter(int index, bool lower)
        {
            var arr = lower ? LetterLowerArr : LetterUpperArr;
            int len = arr.Length;
            if (index < len) return arr[index];
            // 对于超过26的，返回 AA, AB...
            int first = index / len - 1;
            int second = index % len;
            return (lower ? LetterLowerArr[first] : LetterUpperArr[first]) + arr[second];
        }
    }
}
