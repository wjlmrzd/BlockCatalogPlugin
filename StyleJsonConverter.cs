using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// CatalogStyle 专用 JSON 序列化转换器
    /// 提供强类型枚举转换支持
    /// </summary>
    public static class StyleJsonConverter
    {
        /// <summary>
        /// 创建序列化选项（写入时使用，含缩进）
        /// </summary>
        public static JsonSerializerOptions SerializerOptions => new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 创建反序列化选项（读取时使用）
        /// </summary>
        public static JsonSerializerOptions DeserializerOptions => new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 序列化 CatalogStyle 为 JSON 字符串
        /// </summary>
        public static string Serialize(CatalogStyle style)
        {
            if (style == null) return null;
            return JsonSerializer.Serialize(style, SerializerOptions);
        }

        /// <summary>
        /// 反序列化 JSON 字符串为 CatalogStyle
        /// </summary>
        public static CatalogStyle Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<CatalogStyle>(json, DeserializerOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CatalogStyle 反序列化失败: {ex.Message}");
                return null;
            }
        }
    }
}