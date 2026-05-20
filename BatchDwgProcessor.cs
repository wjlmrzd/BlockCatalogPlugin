using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 批量处理结果
    /// </summary>
    public class BatchResult
    {
        public string FilePath { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int BlocksGenerated { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    /// <summary>
    /// 批量DWG文件处理器
    /// 支持进度报告、取消操作
    /// </summary>
    public class BatchDwgProcessor
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCancelled;

        /// <summary>
        /// 状态变更事件 - 传入当前正在处理的文件名
        /// </summary>
        public event Action<string> StatusChanged;

        /// <summary>
        /// 进度变更事件 - 传入 (current, total) 已完成数/总数
        /// </summary>
        public event Action<int, int> ProgressChanged;

        /// <summary>
        /// 单文件完成事件 - 传入 (filename, errorMessage) errorMessage为null表示成功
        /// </summary>
        public event Action<string, string> FileCompleted;

        /// <summary>
        /// 处理文件夹中的所有DWG文件
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="style">目录样式配置</param>
        /// <param name="templateName">模板名称（可选）</param>
        /// <returns>每个文件的处理结果列表</returns>
        public List<BatchResult> ProcessFolder(string folderPath, CatalogStyle style, string templateName = null)
        {
            _isCancelled = false;
            _cancellationTokenSource = new CancellationTokenSource();
            var results = new List<BatchResult>();

            if (!Directory.Exists(folderPath))
            {
                return results;
            }

            // 递归扫描所有 .dwg 文件
            var dwgFiles = GetDwgFilesRecursive(folderPath);
            int total = dwgFiles.Count;

            // 加载模板（如果指定）
            var effectiveStyle = style;
            if (!string.IsNullOrEmpty(templateName))
            {
                var templateStyle = TemplateManager.Instance.Load(templateName);
                if (templateStyle != null)
                {
                    effectiveStyle = templateStyle;
                }
            }

            for (int i = 0; i < dwgFiles.Count; i++)
            {
                // 检查是否已取消
                if (_isCancelled)
                {
                    break;
                }

                var dwgFile = dwgFiles[i];
                var fileName = Path.GetFileName(dwgFile);

                // 报告进度
                OnStatusChanged($"正在处理: {fileName}");
                OnProgressChanged(i + 1, total);

                // 处理单个文件
                var result = ProcessSingleDwg(dwgFile, effectiveStyle);
                results.Add(result);

                // 报告文件完成
                OnFileCompleted(fileName, result.ErrorMessage);
            }

            return results;
        }

        /// <summary>
        /// 取消正在进行的批量处理
        /// </summary>
        public void Cancel()
        {
            _isCancelled = true;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
            OnStatusChanged("已取消批量处理");
        }

        /// <summary>
        /// 递归获取文件夹中所有DWG文件
        /// </summary>
        private List<string> GetDwgFilesRecursive(string folderPath)
        {
            var files = new List<string>();
            try
            {
                // 获取当前文件夹的dwg文件
                files.AddRange(Directory.GetFiles(folderPath, "*.dwg", SearchOption.TopDirectoryOnly));

                // 递归扫描子文件夹
                foreach (var subDir in Directory.GetDirectories(folderPath))
                {
                    try
                    {
                        files.AddRange(GetDwgFilesRecursive(subDir));
                    }
                    catch
                    {
                        // 忽略无法访问的子文件夹
                    }
                }
            }
            catch
            {
                // 忽略无法访问的文件夹
            }

            return files.OrderBy(f => f).ToList();
        }

        /// <summary>
        /// 处理单个DWG文件
        /// </summary>
        private BatchResult ProcessSingleDwg(string dwgPath, CatalogStyle style)
        {
            var result = new BatchResult
            {
                FilePath = dwgPath,
                ProcessedAt = DateTime.Now,
                Success = false
            };

            try
            {
                // 使用只读模式打开图纸
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndReadShare, true, "");

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        // 在模型空间中查找属性块
                        var blocks = ExtractBlocksFromDatabase(db, tr);

                        if (blocks.Count > 0)
                        {
                            result.BlocksGenerated = blocks.Count;
                            result.Success = true;
                        }
                        else
                        {
                            result.ErrorMessage = "未找到属性块";
                        }

                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// 从数据库提取属性块
        /// </summary>
        private List<BlockData> ExtractBlocksFromDatabase(Database db, Transaction tr)
        {
            var blocks = new List<BlockData>();

            // 获取当前空间（模型空间）
            var btrId = db.CurrentSpaceId;
            var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return blocks;

            // 遍历实体查找属性块
            foreach (var objId in btr)
            {
                if (_isCancelled) break;

                var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                var blockRef = entity as BlockReference;
                if (blockRef == null) continue;

                // 跳过没有属性的块
                if (blockRef.AttributeCollection.Count == 0) continue;

                // 提取属性数据
                var blockData = ExtractBlockData(blockRef, tr);
                if (blockData != null && blockData.Attributes.Count > 0)
                {
                    blocks.Add(blockData);
                }
            }

            return blocks;
        }

        /// <summary>
        /// 提取块属性数据
        /// </summary>
        private BlockData ExtractBlockData(BlockReference blockRef, Transaction tr)
        {
            var data = new BlockData();

            var btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return null;

            data.BlockName = btr.Name;
            data.ObjectId = blockRef.Id;

            // 提取属性
            foreach (ObjectId arId in blockRef.AttributeCollection)
            {
                var ar = tr.GetObject(arId, OpenMode.ForRead) as AttributeReference;
                if (ar != null && !string.IsNullOrEmpty(ar.Tag))
                {
                    data.Attributes.Add(new BlockAttribute
                    {
                        Tag = ar.Tag.Trim().ToUpperInvariant(),
                        Value = ar.TextString ?? ""
                    });
                }
            }

            return data;
        }

        /// <summary>
        /// 静态方法：获取文件夹中的DWG文件列表（不递归）
        /// </summary>
        public static List<string> GetDwgFilesInFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return new List<string>();
            return Directory.GetFiles(folderPath, "*.dwg", SearchOption.TopDirectoryOnly)
                           .OrderBy(f => f)
                           .ToList();
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(status);
        }

        private void OnProgressChanged(int current, int total)
        {
            ProgressChanged?.Invoke(current, total);
        }

        private void OnFileCompleted(string filename, string errorMessage)
        {
            FileCompleted?.Invoke(filename, errorMessage);
        }
    }
}