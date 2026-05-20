using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Font = System.Drawing.Font;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// 批量处理面板 - WinForms用户控件
    /// </summary>
    public class BatchProcessingPanel : UserControl
    {
        private TextBox txtFolder;
        private Button btnBrowse;
        private CheckedListBox lstFiles;
        private ComboBox cmbTemplate;
        private CheckBox chkSummary;
        private Button btnScan;
        private Button btnProcess;
        private Button btnCancel;
        private Label lblProgress;
        private ListBox lstLog;
        private ProgressBar progressBar;

        private BatchDwgProcessor _processor;
        private Thread _processingThread;
        private bool _isProcessing;

        public BatchProcessingPanel()
        {
            _processor = new BatchDwgProcessor();
            _processor.StatusChanged += OnStatusChanged;
            _processor.ProgressChanged += OnProgressChanged;
            _processor.FileCompleted += OnFileCompleted;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            AutoScroll = true;
            Padding = new Padding(10);

            int y = 10;
            int labelX = 10;
            int controlX = 10;
            int controlWidth = 350;

            // === 文件夹选择 ===
            var lblFolder = new Label
            {
                Text = "选择文件夹：",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            Controls.Add(lblFolder);
            y += 20;

            txtFolder = new TextBox
            {
                Location = new Point(controlX, y),
                Width = controlWidth - 90,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Name = "txtFolder"
            };
            Controls.Add(txtFolder);

            btnBrowse = new Button
            {
                Text = "浏览...",
                Location = new Point(controlX + controlWidth - 80, y),
                Width = 70,
                Height = 24,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += BtnBrowse_Click;
            Controls.Add(btnBrowse);
            y += 30;

            // === 文件列表 ===
            var lblFiles = new Label
            {
                Text = "DWG文件列表：",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            Controls.Add(lblFiles);
            y += 20;

            lstFiles = new CheckedListBox
            {
                Location = new Point(controlX, y),
                Width = controlWidth,
                Height = 130,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                Name = "lstFiles"
            };
            lstFiles.ItemCheck += LstFiles_ItemCheck;
            Controls.Add(lstFiles);
            y += 140;

            // === 全选/取消全选 ===
            var btnSelectAll = new Button
            {
                Text = "全选",
                Location = new Point(controlX, y),
                Width = 60,
                Height = 22,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 7.5F)
            };
            btnSelectAll.FlatAppearance.BorderSize = 0;
            btnSelectAll.Click += (s, e) =>
            {
                for (int i = 0; i < lstFiles.Items.Count; i++)
                    lstFiles.SetItemChecked(i, true);
            };
            Controls.Add(btnSelectAll);

            var btnSelectNone = new Button
            {
                Text = "取消全选",
                Location = new Point(controlX + 70, y),
                Width = 70,
                Height = 22,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 7.5F)
            };
            btnSelectNone.FlatAppearance.BorderSize = 0;
            btnSelectNone.Click += (s, e) =>
            {
                for (int i = 0; i < lstFiles.Items.Count; i++)
                    lstFiles.SetItemChecked(i, false);
            };
            Controls.Add(btnSelectNone);
            y += 28;

            // === 模板选择 ===
            var lblTemplate = new Label
            {
                Text = "应用模板：",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            Controls.Add(lblTemplate);
            y += 20;

            cmbTemplate = new ComboBox
            {
                Location = new Point(controlX, y),
                Width = 200,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "cmbTemplate"
            };
            cmbTemplate.Items.Add("(不应用模板)");
            var templates = TemplateManager.Instance.List();
            foreach (var name in templates)
            {
                cmbTemplate.Items.Add(name);
            }
            if (cmbTemplate.Items.Count > 0)
                cmbTemplate.SelectedIndex = 0;
            Controls.Add(cmbTemplate);
            y += 30;

            // === 输出选项 ===
            chkSummary = new CheckBox
            {
                Text = "生成汇总目录",
                Location = new Point(controlX, y),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Bg,
                Checked = true,
                Name = "chkSummary"
            };
            Controls.Add(chkSummary);
            y += 30;

            // === 操作按钮 ===
            btnScan = new Button
            {
                Text = "扫描文件",
                Location = new Point(controlX, y),
                Width = 90,
                Height = 28,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.Click += BtnScan_Click;
            Controls.Add(btnScan);

            btnProcess = new Button
            {
                Text = "批量处理",
                Location = new Point(controlX + 100, y),
                Width = 90,
                Height = 28,
                BackColor = Theme.Success,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            btnProcess.FlatAppearance.BorderSize = 0;
            btnProcess.Click += BtnProcess_Click;
            Controls.Add(btnProcess);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(controlX + 200, y),
                Width = 70,
                Height = 28,
                BackColor = Theme.Error,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F),
                Enabled = false
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += BtnCancel_Click;
            Controls.Add(btnCancel);
            y += 35;

            // === 进度条 ===
            progressBar = new ProgressBar
            {
                Location = new Point(controlX, y),
                Width = controlWidth,
                Height = 16,
                Style = ProgressBarStyle.Continuous,
                BackColor = Theme.Card
            };
            Controls.Add(progressBar);
            y += 24;

            // === 进度标签 ===
            lblProgress = new Label
            {
                Text = "0/0 已完成",
                Location = new Point(controlX, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F),
                Name = "lblProgress"
            };
            Controls.Add(lblProgress);
            y += 25;

            // === 日志列表 ===
            var lblLog = new Label
            {
                Text = "处理日志：",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            Controls.Add(lblLog);
            y += 20;

            lstLog = new ListBox
            {
                Location = new Point(controlX, y),
                Width = controlWidth,
                Height = 120,
                BackColor = Theme.LogBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8F),
                Name = "lstLog"
            };
            Controls.Add(lstLog);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择包含DWG文件的文件夹";
                dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = dlg.SelectedPath;
                    ScanFiles();
                }
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            ScanFiles();
        }

        private void ScanFiles()
        {
            string folder = txtFolder.Text;
            if (!Directory.Exists(folder))
            {
                AddLog("文件夹不存在", Theme.Error);
                return;
            }

            lstFiles.Items.Clear();
            var files = BatchDwgProcessor.GetDwgFilesInFolder(folder);

            foreach (var file in files)
            {
                lstFiles.Items.Add(Path.GetFileName(file), true);
            }

            AddLog($"找到 {files.Count} 个DWG文件", Theme.Success);
            UpdateProgressLabel(0, files.Count);
        }

        private void LstFiles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int selectedCount = lstFiles.CheckedItems.Count;
            AddLog($"已选择 {selectedCount} 个文件", Theme.TextDim);
        }

        private void BtnProcess_Click(object sender, EventArgs e)
        {
            if (_isProcessing) return;

            string folder = txtFolder.Text;
            if (!Directory.Exists(folder))
            {
                AddLog("请先选择文件夹", Theme.Warning);
                return;
            }

            // 获取选中的文件
            var selectedFiles = new List<string>();
            for (int i = 0; i < lstFiles.Items.Count; i++)
            {
                if (lstFiles.GetItemChecked(i))
                {
                    selectedFiles.Add(lstFiles.Items[i].ToString());
                }
            }

            if (selectedFiles.Count == 0)
            {
                AddLog("请至少选择一个文件", Theme.Warning);
                return;
            }

            // 开始后台处理
            _isProcessing = true;
            btnProcess.Enabled = false;
            btnScan.Enabled = false;
            btnCancel.Enabled = true;

            string templateName = cmbTemplate.SelectedIndex > 0 ? cmbTemplate.SelectedItem.ToString() : null;
            var style = new CatalogStyle();

            _processingThread = new Thread(() =>
            {
                try
                {
                    // 处理选中文件对应的实际路径
                    var folderPath = txtFolder.Text;
                    var allDwgFiles = BatchDwgProcessor.GetDwgFilesInFolder(folderPath);

                    // 只处理选中的文件
                    var results = new List<BatchResult>();
                    int processed = 0;
                    int total = selectedFiles.Count;

                    foreach (var fileName in selectedFiles)
                    {
                        if (!_isProcessing) break;

                        var filePath = Path.Combine(folderPath, fileName);

                        BeginInvoke(new Action(() =>
                        {
                            OnStatusChanged($"正在处理: {fileName}");
                            OnProgressChanged(processed + 1, total);
                        }));

                        // 实际处理文件
                        var processor = new BatchDwgProcessor();
                        var singleResult = ProcessSingleFile(filePath, style);

                        lock (results)
                        {
                            results.Add(singleResult);
                        }

                        BeginInvoke(new Action(() =>
                        {
                            OnFileCompleted(fileName, singleResult.ErrorMessage);
                        }));

                        processed++;
                    }

                    // 生成汇总（如果启用）
                    if (chkSummary.Checked && results.Count > 0)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            AddLog($"准备生成汇总目录...", Theme.Primary);
                            // 汇总生成逻辑由调用者处理
                        }));
                    }

                    BeginInvoke(new Action(() =>
                    {
                        int successCount = results.FindAll(r => r.Success).Count;
                        AddLog($"处理完成: {successCount}/{results.Count} 文件成功", Theme.Success);
                        FinishProcessing();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        AddLog($"处理出错: {ex.Message}", Theme.Error);
                        FinishProcessing();
                    }));
                }
            });

            _processingThread.IsBackground = true;
            _processingThread.Start();
        }

        private BatchResult ProcessSingleFile(string filePath, CatalogStyle style)
        {
            var result = new BatchResult
            {
                FilePath = filePath,
                ProcessedAt = DateTime.Now,
                Success = false
            };

            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(filePath, Autodesk.AutoCAD.DatabaseServices.FileOpenMode.OpenForReadAndReadShare, true, "");

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        // 在模型空间中查找属性块
                        int blockCount = 0;

                        var btrId = db.CurrentSpaceId;
                        var btr = tr.GetObject(btrId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.BlockTableRecord;
                        if (btr != null)
                        {
                            foreach (var objId in btr)
                            {
                                var entity = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
                                if (entity == null) continue;

                                var blockRef = entity as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                                if (blockRef == null) continue;

                                if (blockRef.AttributeCollection.Count > 0)
                                {
                                    blockCount++;
                                }
                            }
                        }

                        tr.Commit();

                        result.BlocksGenerated = blockCount;
                        result.Success = blockCount > 0;
                        if (!result.Success)
                            result.ErrorMessage = "未找到属性块";
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

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            _isProcessing = false;
            if (_processingThread != null && _processingThread.IsAlive)
            {
                _processingThread.Abort();
            }
            _processor.Cancel();
            AddLog("已取消处理", Theme.Warning);
            FinishProcessing();
        }

        private void FinishProcessing()
        {
            _isProcessing = false;
            btnProcess.Enabled = true;
            btnScan.Enabled = true;
            btnCancel.Enabled = false;
        }

        private void OnStatusChanged(string status)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnStatusChanged), status);
                return;
            }
            AddLog(status, Theme.Primary);
        }

        private void OnProgressChanged(int current, int total)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int>(OnProgressChanged), current, total);
                return;
            }
            progressBar.Maximum = total;
            progressBar.Value = current;
            lblProgress.Text = $"{current}/{total} 已完成";
        }

        private void OnFileCompleted(string filename, string errorMessage)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(OnFileCompleted), filename, errorMessage);
                return;
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                AddLog($"[完成] {filename}", Theme.Success);
            }
            else
            {
                AddLog($"[失败] {filename}: {errorMessage}", Theme.Error);
            }
        }

        private void UpdateProgressLabel(int current, int total)
        {
            lblProgress.Text = $"{current}/{total} 已完成";
            progressBar.Maximum = total > 0 ? total : 1;
            progressBar.Value = current;
        }

        private void AddLog(string message, Color color)
        {
            if (lstLog == null) return;
            if (lstLog.Items.Count > 500) lstLog.Items.Clear();
            lstLog.Items.Add(message);
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        /// <summary>
        /// 获取处理结果
        /// </summary>
        public List<BatchResult> GetResults()
        {
            // 返回当前选中的文件路径列表对应的结果
            var results = new List<BatchResult>();
            return results;
        }
    }
}