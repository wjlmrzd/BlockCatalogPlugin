using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 表格样式模板管理器
    /// 支持保存/加载/删除样式配置（行高、列宽、字体等）
    /// </summary>
    public class StyleTemplateManager
    {
        private static readonly string TemplateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WjlToolsBox", "BlockCatalog", "styles"
        );

        private static StyleTemplateManager _instance;
        public static StyleTemplateManager Instance => _instance ??= new StyleTemplateManager();

        private StyleTemplateManager()
        {
            EnsureTemplateDirectoryExists();
        }

        private void EnsureTemplateDirectoryExists()
        {
            if (!Directory.Exists(TemplateDirectory))
            {
                Directory.CreateDirectory(TemplateDirectory);
            }
        }

        /// <summary>
        /// 获取所有样式模板名称
        /// </summary>
        public List<string> GetTemplateNames()
        {
            var names = new List<string>();
            if (!Directory.Exists(TemplateDirectory)) return names;

            foreach (var file in Directory.GetFiles(TemplateDirectory, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                names.Add(name);
            }
            return names.OrderBy(n => n).ToList();
        }

        /// <summary>
        /// 保存样式模板
        /// </summary>
        public bool SaveTemplate(string name, CatalogStyle style, List<ColumnWidthConfig> columnWidths = null)
        {
            if (string.IsNullOrEmpty(name) || style == null) return false;

            try
            {
                EnsureTemplateDirectoryExists();
                string safeName = GetSafeFileName(name);
                string filePath = Path.Combine(TemplateDirectory, safeName + ".json");

                var templateData = new StyleTemplateData
                {
                    Name = name,
                    Description = $"创建于 {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Created = DateTime.Now,
                    FontName = style.FontName,
                    FontHeight = style.FontHeight,
                    RowHeight = style.RowHeight,
                    HeaderHeight = style.HeaderHeight,
                    DrawBorder = style.DrawBorder,
                    Columns = style.Columns,
                    CustomColumnWidths = columnWidths ?? new List<ColumnWidthConfig>()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string json = JsonSerializer.Serialize(templateData, options);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存样式模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载样式模板
        /// </summary>
        public StyleTemplateData LoadTemplate(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                string safeName = GetSafeFileName(name);
                string filePath = Path.Combine(TemplateDirectory, safeName + ".json");

                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };

                return JsonSerializer.Deserialize<StyleTemplateData>(json, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载样式模板失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除样式模板
        /// </summary>
        public bool DeleteTemplate(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            try
            {
                string safeName = GetSafeFileName(name);
                string filePath = Path.Combine(TemplateDirectory, safeName + ".json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除样式模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 模板是否存在
        /// </summary>
        public bool TemplateExists(string name)
        {
            string safeName = GetSafeFileName(name);
            string filePath = Path.Combine(TemplateDirectory, safeName + ".json");
            return File.Exists(filePath);
        }

        /// <summary>
        /// 获取安全的文件名
        /// </summary>
        private string GetSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new StringBuilder();
            foreach (var c in name)
            {
                if (invalidChars.Contains(c))
                    safeName.Append('_');
                else
                    safeName.Append(c);
            }
            return safeName.ToString();
        }

        /// <summary>
        /// 创建预置模板
        /// </summary>
        public void CreatePresetTemplates()
        {
            if (GetTemplateNames().Count > 0) return;

            // 标准样式
            SaveTemplate("标准样式", new CatalogStyle
            {
                FontName = "宋体",
                FontHeight = 3.5,
                RowHeight = 8,
                HeaderHeight = 10,
                DrawBorder = true
            });

            // 大字体样式
            SaveTemplate("大字体样式", new CatalogStyle
            {
                FontName = "宋体",
                FontHeight = 5,
                RowHeight = 12,
                HeaderHeight = 15,
                DrawBorder = true
            });

            // 紧凑样式
            SaveTemplate("紧凑样式", new CatalogStyle
            {
                FontName = "宋体",
                FontHeight = 2.5,
                RowHeight = 6,
                HeaderHeight = 8,
                DrawBorder = true
            });

            // A3图纸样式
            SaveTemplate("A3图纸样式", new CatalogStyle
            {
                FontName = "宋体",
                FontHeight = 3.5,
                RowHeight = 8,
                HeaderHeight = 10,
                DrawBorder = true,
                Columns = new List<ColumnDef>
                {
                    new ColumnDef { Tag = "XH", Header = "图号", Width = 50, Visible = true, Order = 0 },
                    new ColumnDef { Tag = "TM", Header = "图名", Width = 80, Visible = true, Order = 1 },
                    new ColumnDef { Tag = "FM", Header = "幅面", Width = 30, Visible = true, Order = 2 },
                    new ColumnDef { Tag = "BLM", Header = "比例", Width = 30, Visible = true, Order = 3 }
                }
            });
        }
    }

    /// <summary>
    /// 样式模板数据结构
    /// </summary>
    public class StyleTemplateData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }

        // 字体设置
        public string FontName { get; set; } = "宋体";
        public double FontHeight { get; set; } = 3.5;

        // 行高设置
        public double RowHeight { get; set; } = 8;
        public double HeaderHeight { get; set; } = 10;

        // 边框
        public bool DrawBorder { get; set; } = true;

        // 列配置
        public List<ColumnDef> Columns { get; set; } = new List<ColumnDef>();

        // 自定义列宽（用户拖拽调整后的）
        public List<ColumnWidthConfig> CustomColumnWidths { get; set; } = new List<ColumnWidthConfig>();
    }

}