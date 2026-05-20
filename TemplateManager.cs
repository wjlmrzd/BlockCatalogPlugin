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
    /// 模板管理器
    /// 支持保存/加载/删除目录样式模板
    /// </summary>
    public class TemplateManager
    {
        private static readonly string TemplateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WjlToolsBox", "BlockCatalog", "templates"
        );

        private static TemplateManager _instance;
        public static TemplateManager Instance => _instance ??= new TemplateManager();

        private TemplateManager()
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
        /// 保存模板
        /// </summary>
        public bool Save(string name, CatalogStyle style)
        {
            if (string.IsNullOrEmpty(name) || style == null) return false;

            try
            {
                EnsureTemplateDirectoryExists();
                string safeName = GetSafeFileName(name);
                string filePath = Path.Combine(TemplateDirectory, safeName + ".json");

                var templateData = new TemplateData
                {
                    Name = name,
                    Description = $"创建于 {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Created = DateTime.Now,
                    Style = style
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
                System.Diagnostics.Debug.WriteLine($"保存模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载模板
        /// </summary>
        public CatalogStyle Load(string name)
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

                var templateData = JsonSerializer.Deserialize<TemplateData>(json, options);
                return templateData?.Style;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载模板失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除模板
        /// </summary>
        public bool Delete(string name)
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
                System.Diagnostics.Debug.WriteLine($"删除模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 返回所有模板名称列表
        /// </summary>
        public List<string> List()
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
        /// 导出模板到外部路径
        /// </summary>
        public bool Export(string path, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) return false;

            try
            {
                string safeName = GetSafeFileName(name);
                string sourcePath = Path.Combine(TemplateDirectory, safeName + ".json");

                if (!File.Exists(sourcePath)) return false;

                File.Copy(sourcePath, path, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导出模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从外部路径导入模板
        /// </summary>
        public bool Import(string path, string newName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };

                var templateData = JsonSerializer.Deserialize<TemplateData>(json, options);
                if (templateData == null) return false;

                string name = newName ?? templateData.Name;
                EnsureTemplateDirectoryExists();

                string safeName = GetSafeFileName(name);
                string destPath = Path.Combine(TemplateDirectory, safeName + ".json");

                // 更新名称和时间
                templateData.Name = name;
                templateData.Created = DateTime.Now;

                var saveOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string newJson = JsonSerializer.Serialize(templateData, saveOptions);
                File.WriteAllText(destPath, newJson);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导入模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取安全的文件名（替换特殊字符）
        /// </summary>
        private string GetSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new StringBuilder();

            foreach (var c in name)
            {
                if (invalidChars.Contains(c))
                {
                    safeName.Append('_');
                }
                else
                {
                    safeName.Append(c);
                }
            }

            return safeName.ToString();
        }

        /// <summary>
        /// 创建预置模板（首次使用时调用）
        /// </summary>
        public void CreatePresetTemplates()
        {
            var existing = List();
            if (existing.Count > 0) return;

            // 标准目录：序号+图号+图名，合并策略=按前缀
            var standardStyle = new CatalogStyle
            {
                RowHeight = 8.0,
                MergeStrategy = MergeStrategy.PrefixConsecutive,
                SequenceFormat = SequenceFormat.Numeric,
                HeaderVisible = true,
                Columns = new List<ColumnDef>
                {
                    new ColumnDef { Tag = "XH", Header = "序号", Width = 20, Visible = true, Order = 0 },
                    new ColumnDef { Tag = "TH", Header = "图号", Width = 40, Visible = true, Order = 1 },
                    new ColumnDef { Tag = "TM", Header = "图名", Width = 60, Visible = true, Order = 2 }
                }
            };
            Save("标准目录", standardStyle);

            // 简化递增：序号+图号，无合并
            var simpleStyle = new CatalogStyle
            {
                RowHeight = 8.0,
                MergeStrategy = MergeStrategy.None,
                SequenceFormat = SequenceFormat.Numeric,
                HeaderVisible = true,
                Columns = new List<ColumnDef>
                {
                    new ColumnDef { Tag = "XH", Header = "序号", Width = 20, Visible = true, Order = 0 },
                    new ColumnDef { Tag = "TH", Header = "图号", Width = 40, Visible = true, Order = 1 }
                }
            };
            Save("简化递增", simpleStyle);
        }

        /// <summary>
        /// 模板是否存在
        /// </summary>
        public bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string safeName = GetSafeFileName(name);
            string filePath = Path.Combine(TemplateDirectory, safeName + ".json");
            return File.Exists(filePath);
        }

        /// <summary>
        /// 获取模板数量
        /// </summary>
        public int Count()
        {
            return List().Count;
        }
    }

    /// <summary>
    /// 模板数据结构
    /// </summary>
    public class TemplateData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public CatalogStyle Style { get; set; }
    }
}