using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MediaFileManager.Controls;
using MediaFileManager.Models;
using MediaFileManager.Services;

namespace MediaFileManager
{
    /// <summary>
    /// 主窗体类 - 个人多媒体文件管理中心
    ///
    /// 采用TabControl多标签页设计，包含以下模块：
    /// 1. 文件浏览器 - TreeView + ListView 双栏浏览 + 搜索过滤
    /// 2. 媒体播放器 - 播放列表管理 + 媒体播放
    /// 3. 文件同步备份 - 源/目标目录选择 + 异步同步 + 进度展示
    /// 4. 统计图表 - GDI+ 饼图/柱状图 + DataGridView 数据表格
    /// 5. 数据库管理 - 数据导入导出 + 同步历史
    ///
    /// 技术要点：
    /// - WinForms控件综合应用：TabControl, TreeView, ListView, DataGridView,
    ///   ProgressBar, StatusStrip, MenuStrip, SplitContainer, Panel等
    /// - 多线程：Task.Run + async/await + CancellationToken + IProgress
    /// - GDI+绘图：自定义ChartControl控件
    /// - 文件操作：System.IO命名空间
    /// - 数据库：通过DatabaseService封装SQLite操作
    /// - 异常处理：try-catch-finally + using语句资源管理
    /// </summary>
    public partial class MainForm : Form
    {
        // ==================== 服务实例 ====================
        private readonly DatabaseService _databaseService;
        private readonly FileScannerService _fileScannerService;
        private readonly FileSyncService _fileSyncService;
        private readonly StatisticsService _statisticsService;

        // ==================== 状态字段 ====================
        private List<MediaFileInfo> _currentFiles;
        private CancellationTokenSource _scanCts;
        private CancellationTokenSource _syncCts;
        private bool _isScanning;
        private bool _isSyncing;

        // ==================== UI控件（程序化创建） ====================
        private TabControl tabControl1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblStatus;
        private ToolStripProgressBar toolStripProgress;

        // Tab1: 文件浏览器控件
        private SplitContainer splitContainer1;
        private TreeView tvDirectories;
        private ListView lvFiles;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnScanFolder;
        private Label lblFileCount;

        // Tab2: 媒体播放器控件
        private ListBox lbPlaylist;
        private Button btnAddToPlaylist;
        private Button btnRemoveFromPlaylist;
        private Button btnPlaySelected;
        private Button btnClearPlaylist;
        private Label lblNowPlaying;

        // Tab3: 文件同步备份控件
        private TextBox txtSourcePath;
        private TextBox txtDestPath;
        private Button btnBrowseSource;
        private Button btnBrowseDest;
        private Button btnStartSync;
        private Button btnStopSync;
        private ProgressBar progressSync;
        private Label lblSyncProgress;
        private Label lblSyncDetail;

        // Tab4: 统计图表控件
        private ChartControl chartPie;
        private ChartControl chartBar;
        private DataGridView dgvStatistics;
        private Button btnRefreshStats;
        private Label lblSummary;

        // Tab5: 数据库管理控件
        private DataGridView dgvDatabase;
        private Button btnLoadDB;
        private Button btnClearDB;
        private Button btnExportCSV;
        private DataGridView dgvSyncHistory;

        public MainForm()
        {
            // 初始化服务层
            _databaseService = new DatabaseService();
            _fileScannerService = new FileScannerService();
            _fileSyncService = new FileSyncService(_databaseService);
            _statisticsService = new StatisticsService();

            // 初始化数据库（创建表结构）
            try
            {
                _databaseService.InitializeDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库初始化失败: {ex.Message}\n部分功能可能不可用。",
                    "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // 初始化UI
            InitializeForm();
            InitializeMenuStrip();
            InitializeTabControl();
            InitializeStatusStrip();

            _currentFiles = new List<MediaFileInfo>();
        }

        /// <summary>
        /// 初始化窗体基本属性
        /// </summary>
        private void InitializeForm()
        {
            this.Text = "MediaVault - 个人多媒体文件管理中心";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;
            this.BackColor = Color.FromArgb(245, 245, 250);
        }

        /// <summary>
        /// 初始化菜单栏
        /// </summary>
        private void InitializeMenuStrip()
        {
            MenuStrip menuStrip = new MenuStrip();
            menuStrip.BackColor = Color.FromArgb(255, 255, 255);

            // 文件菜单
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("文件(&F)");
            fileMenu.DropDownItems.Add("扫描目录(&S)", null, (s, e) => ScanSelectedFolder());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("导出CSV(&E)", null, (s, e) => ExportToCSV());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("退出(&X)", null, (s, e) => Application.Exit());

            // 视图菜单
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("视图(&V)");
            viewMenu.DropDownItems.Add("刷新(&R)", null, (s, e) => RefreshAll());

            // 工具菜单
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("工具(&T)");
            toolsMenu.DropDownItems.Add("清空数据库(&C)", null, (s, e) => ClearDatabase());

            // 帮助菜单
            ToolStripMenuItem helpMenu = new ToolStripMenuItem("帮助(&H)");
            helpMenu.DropDownItems.Add("关于(&A)", null, (s, e) =>
            {
                MessageBox.Show(
                    "MediaVault v1.0\n" +
                    "个人多媒体文件管理中心\n\n" +
                    "Windows程序设计课程期末作业\n" +
                    "计算机学院 物联网工程2401班\n\n" +
                    "技术栈：C# WinForms + SQLite + GDI+\n" +
                    "开发模式：RDD + TDD + AI协同编程(Claude Code)",
                    "关于 MediaVault",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        /// <summary>
        /// 初始化TabControl及其所有标签页
        /// 使用程序化创建方式（非Designer），便于代码管理和版本控制
        /// </summary>
        private void InitializeTabControl()
        {
            tabControl1 = new TabControl
            {
                Location = new Point(8, 32),
                Size = new Size(1068, 638),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("微软雅黑", 10)
            };

            // 创建各个标签页
            TabPage tabFileBrowser = CreateFileBrowserTab();
            TabPage tabMediaPlayer = CreateMediaPlayerTab();
            TabPage tabFileSync = CreateFileSyncTab();
            TabPage tabStatistics = CreateStatisticsTab();
            TabPage tabDatabase = CreateDatabaseTab();

            tabControl1.TabPages.AddRange(new TabPage[]
            {
                tabFileBrowser, tabMediaPlayer, tabFileSync, tabStatistics, tabDatabase
            });

            this.Controls.Add(tabControl1);
        }

        // ==================== Tab1: 文件浏览器 ====================
        private TabPage CreateFileBrowserTab()
        {
            TabPage tab = new TabPage("文件浏览器");
            tab.BackColor = Color.White;

            // 顶部工具栏面板
            Panel topPanel = new Panel
            {
                Location = new Point(8, 8),
                Size = new Size(1040, 36),
                BackColor = Color.White
            };

            Label lblPath = new Label
            {
                Text = "扫描目录:",
                Location = new Point(4, 10),
                Size = new Size(70, 20),
                Font = new Font("微软雅黑", 9)
            };

            txtSearch = new TextBox
            {
                Location = new Point(78, 8),
                Size = new Size(280, 22),
                Font = new Font("微软雅黑", 9),
                PlaceholderText = "输入文件/目录路径..."
            };
            txtSearch.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            btnScanFolder = new Button
            {
                Text = "扫描目录",
                Location = new Point(365, 6),
                Size = new Size(90, 26),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(65, 140, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnScanFolder.FlatAppearance.BorderSize = 0;
            btnScanFolder.Click += (s, e) => ScanSelectedFolder();

            btnSearch = new Button
            {
                Text = "🔍 搜索",
                Location = new Point(462, 6),
                Size = new Size(80, 26),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(89, 194, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Click += BtnSearch_Click;

            lblFileCount = new Label
            {
                Text = "文件数: 0",
                Location = new Point(560, 10),
                Size = new Size(200, 20),
                Font = new Font("微软雅黑", 9),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            topPanel.Controls.AddRange(new Control[] { lblPath, txtSearch, btnScanFolder, btnSearch, lblFileCount });

            // 主内容区：SplitContainer（TreeView + ListView）
            splitContainer1 = new SplitContainer
            {
                Location = new Point(8, 50),
                Size = new Size(1040, 540),
                SplitterDistance = 260,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Panel1MinSize = 180,
                Panel2MinSize = 400
            };

            // 左侧：目录树
            tvDirectories = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("微软雅黑", 9),
                HideSelection = false,
                PathSeparator = "\\"
            };
            tvDirectories.AfterSelect += TvDirectories_AfterSelect;

            // 右侧：文件列表
            lvFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("微软雅黑", 9),
                AllowColumnReorder = true
            };
            lvFiles.Columns.Add("文件名", 280);
            lvFiles.Columns.Add("大小", 100);
            lvFiles.Columns.Add("类型", 80);
            lvFiles.Columns.Add("修改日期", 160);
            lvFiles.Columns.Add("路径", 400);
            lvFiles.DoubleClick += LvFiles_DoubleClick;
            lvFiles.MouseClick += LvFiles_MouseClick;

            splitContainer1.Panel1.Controls.Add(tvDirectories);
            splitContainer1.Panel2.Controls.Add(lvFiles);

            tab.Controls.Add(topPanel);
            tab.Controls.Add(splitContainer1);

            return tab;
        }

        // ==================== Tab2: 媒体播放器 ====================
        private TabPage CreateMediaPlayerTab()
        {
            TabPage tab = new TabPage("媒体播放器");
            tab.BackColor = Color.White;

            // 左侧：播放列表
            Label lblPlaylist = new Label
            {
                Text = "播放列表",
                Location = new Point(12, 12),
                Size = new Size(100, 24),
                Font = new Font("微软雅黑", 11, FontStyle.Bold)
            };

            lbPlaylist = new ListBox
            {
                Location = new Point(12, 42),
                Size = new Size(380, 350),
                Font = new Font("微软雅黑", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            // 播放列表按钮
            btnAddToPlaylist = new Button
            {
                Text = "+ 添加文件",
                Location = new Point(12, 400),
                Size = new Size(88, 28),
                Font = new Font("微软雅黑", 8),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(65, 140, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddToPlaylist.FlatAppearance.BorderSize = 0;
            btnAddToPlaylist.Click += BtnAddToPlaylist_Click;

            btnRemoveFromPlaylist = new Button
            {
                Text = "- 移除",
                Location = new Point(108, 400),
                Size = new Size(70, 28),
                Font = new Font("微软雅黑", 8),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(237, 101, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRemoveFromPlaylist.FlatAppearance.BorderSize = 0;
            btnRemoveFromPlaylist.Click += BtnRemoveFromPlaylist_Click;

            btnPlaySelected = new Button
            {
                Text = "▶ 播放",
                Location = new Point(186, 400),
                Size = new Size(80, 28),
                Font = new Font("微软雅黑", 8),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(89, 194, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnPlaySelected.FlatAppearance.BorderSize = 0;
            btnPlaySelected.Click += BtnPlaySelected_Click;

            btnClearPlaylist = new Button
            {
                Text = "清空列表",
                Location = new Point(274, 400),
                Size = new Size(80, 28),
                Font = new Font("微软雅黑", 8),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(149, 152, 159),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClearPlaylist.FlatAppearance.BorderSize = 0;
            btnClearPlaylist.Click += BtnClearPlaylist_Click;

            // 右侧：播放信息
            lblNowPlaying = new Label
            {
                Text = "当前未播放\n\n支持的格式：MP3, WAV, WMA, MP4, AVI, WMV\n\n请在文件浏览器中双击媒体文件\n或点击"+添加文件"添加到播放列表",
                Location = new Point(420, 42),
                Size = new Size(600, 400),
                Font = new Font("微软雅黑", 10),
                ForeColor = Color.FromArgb(120, 120, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.TopLeft
            };

            tab.Controls.AddRange(new Control[] {
                lblPlaylist, lbPlaylist,
                btnAddToPlaylist, btnRemoveFromPlaylist, btnPlaySelected, btnClearPlaylist,
                lblNowPlaying
            });

            return tab;
        }

        // ==================== Tab3: 文件同步备份 ====================
        private TabPage CreateFileSyncTab()
        {
            TabPage tab = new TabPage("文件同步备份");
            tab.BackColor = Color.White;

            Font labelFont = new Font("微软雅黑", 10);
            int yStart = 20;

            // 源目录
            Label lblSource = new Label { Text = "源目录:", Location = new Point(20, yStart), Size = new Size(70, 24), Font = labelFont };
            txtSourcePath = new TextBox { Location = new Point(95, yStart), Size = new Size(480, 24), Font = new Font("微软雅黑", 9) };
            btnBrowseSource = new Button { Text = "浏览...", Location = new Point(585, yStart - 2), Size = new Size(80, 28), Font = new Font("微软雅黑", 9) };
            btnBrowseSource.Click += (s, e) => BrowseFolder(txtSourcePath);

            // 目标目录
            int yDest = yStart + 40;
            Label lblDest = new Label { Text = "目标目录:", Location = new Point(20, yDest), Size = new Size(70, 24), Font = labelFont };
            txtDestPath = new TextBox { Location = new Point(95, yDest), Size = new Size(480, 24), Font = new Font("微软雅黑", 9) };
            btnBrowseDest = new Button { Text = "浏览...", Location = new Point(585, yDest - 2), Size = new Size(80, 28), Font = new Font("微软雅黑", 9) };
            btnBrowseDest.Click += (s, e) => BrowseFolder(txtDestPath);

            // 操作按钮
            int yBtn = yDest + 45;
            btnStartSync = new Button
            {
                Text = "开始同步",
                Location = new Point(95, yBtn),
                Size = new Size(110, 35),
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(65, 140, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnStartSync.FlatAppearance.BorderSize = 0;
            btnStartSync.Click += BtnStartSync_Click;

            btnStopSync = new Button
            {
                Text = "停止",
                Location = new Point(215, yBtn),
                Size = new Size(80, 35),
                Font = new Font("微软雅黑", 10),
                BackColor = Color.FromArgb(237, 101, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnStopSync.FlatAppearance.BorderSize = 0;
            btnStopSync.Click += BtnStopSync_Click;

            // 进度条
            progressSync = new ProgressBar
            {
                Location = new Point(20, yBtn + 50),
                Size = new Size(640, 24),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };

            lblSyncProgress = new Label
            {
                Text = "就绪 - 请选择源目录和目标目录后点击"开始同步"",
                Location = new Point(20, yBtn + 80),
                Size = new Size(640, 20),
                Font = new Font("微软雅黑", 9),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            lblSyncDetail = new Label
            {
                Text = "",
                Location = new Point(20, yBtn + 105),
                Size = new Size(640, 60),
                Font = new Font("微软雅黑", 8),
                ForeColor = Color.FromArgb(130, 130, 130)
            };

            // 说明面板
            Panel infoPanel = new Panel
            {
                Location = new Point(20, yBtn + 170),
                Size = new Size(640, 120),
                BackColor = Color.FromArgb(250, 250, 255),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblInfo = new Label
            {
                Text = "📋 同步说明：\n" +
                       "• 增量同步：仅复制新增或修改过的文件，已存在且未修改的文件将跳过\n" +
                       "• 目录结构保持：同步时会保持原有的文件夹层级结构\n" +
                       "• 安全可靠：单个文件失败不会影响其他文件的同步\n" +
                       "• 进度可视：实时显示同步进度和传输数据量",
                Location = new Point(12, 10),
                Size = new Size(610, 95),
                Font = new Font("微软雅黑", 9),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            infoPanel.Controls.Add(lblInfo);

            tab.Controls.AddRange(new Control[] {
                lblSource, txtSourcePath, btnBrowseSource,
                lblDest, txtDestPath, btnBrowseDest,
                btnStartSync, btnStopSync,
                progressSync, lblSyncProgress, lblSyncDetail,
                infoPanel
            });

            return tab;
        }

        // ==================== Tab4: 统计图表 ====================
        private TabPage CreateStatisticsTab()
        {
            TabPage tab = new TabPage("统计图表");
            tab.BackColor = Color.White;

            btnRefreshStats = new Button
            {
                Text = "刷新统计",
                Location = new Point(12, 10),
                Size = new Size(100, 30),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(65, 140, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRefreshStats.FlatAppearance.BorderSize = 0;
            btnRefreshStats.Click += BtnRefreshStats_Click;

            lblSummary = new Label
            {
                Text = "点击"刷新统计"查看文件分析报告",
                Location = new Point(125, 15),
                Size = new Size(600, 24),
                Font = new Font("微软雅黑", 10),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            // 饼图
            chartPie = new ChartControl
            {
                Location = new Point(12, 50),
                Size = new Size(500, 270),
                ChartTitle = "文件类型分布（饼图）",
                ChartType = ChartType.Pie
            };

            // 柱状图
            chartBar = new ChartControl
            {
                Location = new Point(520, 50),
                Size = new Size(530, 270),
                ChartTitle = "文件数量统计（柱状图）",
                ChartType = ChartType.Bar
            };

            // 统计数据表格
            dgvStatistics = new DataGridView
            {
                Location = new Point(12, 330),
                Size = new Size(1038, 270),
                Font = new Font("微软雅黑", 9),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            tab.Controls.AddRange(new Control[] {
                btnRefreshStats, lblSummary, chartPie, chartBar, dgvStatistics
            });

            return tab;
        }

        // ==================== Tab5: 数据库管理 ====================
        private TabPage CreateDatabaseTab()
        {
            TabPage tab = new TabPage("数据库管理");
            tab.BackColor = Color.White;

            // 数据库操作按钮
            btnLoadDB = new Button
            {
                Text = "加载数据库记录",
                Location = new Point(12, 10),
                Size = new Size(130, 30),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(65, 140, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLoadDB.FlatAppearance.BorderSize = 0;
            btnLoadDB.Click += BtnLoadDB_Click;

            btnExportCSV = new Button
            {
                Text = "导出CSV",
                Location = new Point(152, 10),
                Size = new Size(90, 30),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(89, 194, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExportCSV.FlatAppearance.BorderSize = 0;
            btnExportCSV.Click += (s, e) => ExportToCSV();

            btnClearDB = new Button
            {
                Text = "清空数据库",
                Location = new Point(252, 10),
                Size = new Size(100, 30),
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(237, 101, 121),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClearDB.FlatAppearance.BorderSize = 0;
            btnClearDB.Click += (s, e) => ClearDatabase();

            // 文件信息数据表
            Label lblFileTable = new Label
            {
                Text = "文件信息表 (FileInfo)",
                Location = new Point(12, 50),
                Size = new Size(200, 20),
                Font = new Font("微软雅黑", 10, FontStyle.Bold)
            };

            dgvDatabase = new DataGridView
            {
                Location = new Point(12, 75),
                Size = new Size(1038, 230),
                Font = new Font("微软雅黑", 9),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // 同步历史表
            Label lblSyncTable = new Label
            {
                Text = "同步历史表 (SyncHistory)",
                Location = new Point(12, 315),
                Size = new Size(200, 20),
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            dgvSyncHistory = new DataGridView
            {
                Location = new Point(12, 340),
                Size = new Size(1038, 255),
                Font = new Font("微软雅黑", 9),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            tab.Controls.AddRange(new Control[] {
                btnLoadDB, btnExportCSV, btnClearDB,
                lblFileTable, dgvDatabase,
                lblSyncTable, dgvSyncHistory
            });

            return tab;
        }

        /// <summary>
        /// 初始化状态栏
        /// 显示当前状态和进度条
        /// </summary>
        private void InitializeStatusStrip()
        {
            statusStrip1 = new StatusStrip();
            lblStatus = new ToolStripStatusLabel("就绪");
            toolStripProgress = new ToolStripProgressBar
            {
                Size = new Size(150, 16),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            statusStrip1.Items.Add(lblStatus);
            statusStrip1.Items.Add(toolStripProgress);
            this.Controls.Add(statusStrip1);
        }

        // ==================== 文件浏览器事件处理 ====================

        /// <summary>
        /// 扫描指定目录：使用多线程异步扫描文件
        /// 结合FileScannerService的异步方法，使用Progress报告进度到UI
        /// </summary>
        private async void ScanSelectedFolder()
        {
            string path = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("请输入有效的目录路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 取消正在进行的扫描
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            SetScanningState(true);

            try
            {
                // 使用Progress<T>实现跨线程UI更新
                var progress = new Progress<ScanProgressInfo>(info =>
                {
                    lblStatus.Text = $"扫描中... {info.PercentComplete}% ({info.ProcessedCount}/{info.TotalCount})";
                    toolStripProgress.Visible = true;
                });

                // 异步扫描（线程池执行）
                _currentFiles = await _fileScannerService.ScanDirectoryAsync(
                    path, "*.*", progress, _scanCts.Token);

                // 更新UI
                UpdateFileList(_currentFiles);
                UpdateDirectoryTree(path);
                lblFileCount.Text = $"文件数: {_currentFiles.Count}";
                lblStatus.Text = $"扫描完成 - 找到 {_currentFiles.Count} 个文件";

                // 将扫描结果保存到数据库
                await Task.Run(() =>
                {
                    try { _databaseService.ClearAllFiles(); } catch { }
                    try { _databaseService.BatchInsertFiles(_currentFiles); } catch { }
                });
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "扫描已取消";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "扫描失败";
            }
            finally
            {
                SetScanningState(false);
                toolStripProgress.Visible = false;
            }
        }

        /// <summary>
        /// 更新文件列表视图
        /// </summary>
        private void UpdateFileList(List<MediaFileInfo> files)
        {
            lvFiles.Items.Clear();
            lvFiles.BeginUpdate(); // 批量更新，提高性能

            foreach (var file in files)
            {
                ListViewItem item = new ListViewItem(file.FileName);
                item.SubItems.Add(file.FileSizeFormatted);
                item.SubItems.Add(file.FileType);
                item.SubItems.Add(file.LastModified.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(file.FullPath);
                item.Tag = file; // 将MediaFileInfo对象附加到Tag属性
                lvFiles.Items.Add(item);
            }

            lvFiles.EndUpdate();
        }

        /// <summary>
        /// 更新目录树
        /// </summary>
        private void UpdateDirectoryTree(string rootPath)
        {
            tvDirectories.Nodes.Clear();
            try
            {
                TreeNode rootNode = new TreeNode(rootPath) { Tag = rootPath };
                tvDirectories.Nodes.Add(rootNode);

                // 仅展开第一层子目录（延迟加载，避免过深的递归）
                foreach (string dir in Directory.GetDirectories(rootPath))
                {
                    try
                    {
                        string dirName = Path.GetFileName(dir);
                        TreeNode node = new TreeNode(dirName) { Tag = dir };
                        node.Nodes.Add(new TreeNode("loading...")); // 占位节点
                        rootNode.Nodes.Add(node);
                    }
                    catch { /* 跳过无法访问的目录 */ }
                }
                rootNode.Expand();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"加载目录树失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 目录树节点选中事件：延迟加载子目录并显示该目录下的文件
        /// </summary>
        private void TvDirectories_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is string path && Directory.Exists(path))
            {
                // 延迟加载：如果只有一个"loading..."子节点，实际加载子目录
                if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "loading...")
                {
                    e.Node.Nodes.Clear();
                    foreach (string dir in Directory.GetDirectories(path))
                    {
                        try
                        {
                            string dirName = Path.GetFileName(dir);
                            TreeNode childNode = new TreeNode(dirName) { Tag = dir };
                            childNode.Nodes.Add(new TreeNode("loading..."));
                            e.Node.Nodes.Add(childNode);
                        }
                        catch { }
                    }
                }

                // 在当前文件列表中过滤该目录下的文件
                var dirFiles = _currentFiles
                    .Where(f => Path.GetDirectoryName(f.FullPath)?.Equals(path, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                // 也包含子目录的文件（如果已展开子节点）
                UpdateFileList(dirFiles);
            }
        }

        /// <summary>
        /// 文件列表双击事件：打开文件或播放媒体
        /// 使用Process.Start调用系统默认程序打开文件
        /// </summary>
        private void LvFiles_DoubleClick(object sender, EventArgs e)
        {
            if (lvFiles.SelectedItems.Count > 0 && lvFiles.SelectedItems[0].Tag is MediaFileInfo file)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        System.Diagnostics.Process.Start(file.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 文件列表右键菜单：复制路径、删除文件
        /// </summary>
        private void LvFiles_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && lvFiles.SelectedItems.Count > 0)
            {
                ContextMenuStrip contextMenu = new ContextMenuStrip();
                ToolStripMenuItem copyItem = new ToolStripMenuItem("复制路径");
                copyItem.Click += (s, args) =>
                {
                    if (lvFiles.SelectedItems[0].Tag is MediaFileInfo file)
                    {
                        Clipboard.SetText(file.FullPath);
                        lblStatus.Text = "路径已复制到剪贴板";
                    }
                };
                contextMenu.Items.Add(copyItem);
                contextMenu.Show(lvFiles, e.Location);
            }
        }

        /// <summary>
        /// 搜索按钮事件：在已扫描的文件中搜索
        /// </summary>
        private void BtnSearch_Click(object sender, EventArgs e)
        {
            string keyword = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                ScanSelectedFolder();
                return;
            }

            // 如果已在浏览目录模式，先尝试搜索
            if (_currentFiles.Count > 0)
            {
                var results = _currentFiles
                    .Where(f => f.FileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                UpdateFileList(results);
                lblFileCount.Text = $"搜索结果: {results.Count} 个文件匹配 "{keyword}"";
                lblStatus.Text = $"搜索完成 - 找到 {results.Count} 个匹配文件";
            }
            else
            {
                ScanSelectedFolder();
            }
        }

        private void SetScanningState(bool scanning)
        {
            _isScanning = scanning;
            btnScanFolder.Enabled = !scanning;
            btnScanFolder.Text = scanning ? "扫描中..." : "扫描目录";
            btnSearch.Enabled = !scanning;
        }

        // ==================== 媒体播放器事件处理 ====================

        /// <summary>
        /// 添加文件到播放列表
        /// </summary>
        private void BtnAddToPlaylist_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择媒体文件";
                ofd.Filter = "媒体文件|*.mp3;*.wav;*.wma;*.mp4;*.avi;*.wmv;*.mkv;*.flac;*.aac|所有文件|*.*";
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (string file in ofd.FileNames)
                    {
                        if (!lbPlaylist.Items.Contains(file))
                        {
                            lbPlaylist.Items.Add(file);
                        }
                    }
                    lblNowPlaying.Text = $"播放列表 ({lbPlaylist.Items.Count} 首)";
                }
            }
        }

        private void BtnRemoveFromPlaylist_Click(object sender, EventArgs e)
        {
            if (lbPlaylist.SelectedIndex >= 0)
            {
                lbPlaylist.Items.RemoveAt(lbPlaylist.SelectedIndex);
            }
        }

        /// <summary>
        /// 播放选中的媒体文件
        /// 使用Process.Start调用系统默认播放器
        /// </summary>
        private void BtnPlaySelected_Click(object sender, EventArgs e)
        {
            if (lbPlaylist.SelectedItem is string filePath && File.Exists(filePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(filePath);
                    lblNowPlaying.Text = $"正在播放: {Path.GetFileName(filePath)}";
                    lblStatus.Text = $"正在播放: {filePath}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"播放失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (lbPlaylist.Items.Count > 0)
            {
                // 播放第一个
                string firstFile = lbPlaylist.Items[0].ToString();
                try
                {
                    System.Diagnostics.Process.Start(firstFile);
                    lblNowPlaying.Text = $"正在播放: {Path.GetFileName(firstFile)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"播放失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnClearPlaylist_Click(object sender, EventArgs e)
        {
            lbPlaylist.Items.Clear();
            lblNowPlaying.Text = "播放列表已清空";
        }

        // ==================== 文件同步备份事件处理 ====================

        /// <summary>
        /// 浏览文件夹对话框
        /// </summary>
        private void BrowseFolder(TextBox targetTextBox)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择目录";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    targetTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// 异步启动文件同步
        ///
        /// 核心流程：
        /// 1. 验证输入参数
        /// 2. 设置UI状态（禁用按钮，显示进度）
        /// 3. 使用async/await调用FileSyncService.SyncDirectoriesAsync
        /// 4. 通过IProgress实时更新UI进度
        /// 5. 处理完成/取消/异常三种结果
        /// </summary>
        private async void BtnStartSync_Click(object sender, EventArgs e)
        {
            string sourcePath = txtSourcePath.Text.Trim();
            string destPath = txtDestPath.Text.Trim();

            // 输入验证
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                MessageBox.Show("请选择有效的源目录。", "验证失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(destPath))
            {
                MessageBox.Show("请输入目标目录路径。", "验证失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("源目录和目标目录不能相同。", "验证失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 取消正在进行的同步
            _syncCts?.Cancel();
            _syncCts = new CancellationTokenSource();

            SetSyncingState(true);

            try
            {
                // 创建进度报告器 - 自动封送回UI线程
                var progress = new Progress<SyncProgressInfo>(info =>
                {
                    progressSync.Value = info.PercentComplete;
                    lblSyncProgress.Text = $"同步进度: {info.PercentComplete}% " +
                        $"({info.CopiedCount + info.SkippedCount + info.FailedCount}/{info.TotalCount})";
                    lblSyncDetail.Text = $"已复制: {info.CopiedCount} | 已跳过: {info.SkippedCount} | " +
                        $"失败: {info.FailedCount} | 传输: {info.BytesTransferredFormatted}";
                    lblStatus.Text = $"同步中... {info.PercentComplete}%";
                });

                // 异步执行同步（线程池）
                SyncJob result = await _fileSyncService.SyncDirectoriesAsync(
                    sourcePath, destPath, progress, _syncCts.Token);

                // 同步完成处理
                string message = $"同步完成！\n" +
                    $"总文件数: {result.TotalFiles}\n" +
                    $"已复制: {result.CopiedFiles}\n" +
                    $"已跳过: {result.SkippedFiles}\n" +
                    $"失败: {result.FailedFiles}\n" +
                    $"耗时: {result.Duration.TotalSeconds:F1} 秒\n" +
                    $"传输量: {SyncProgressInfo_FormatBytes(result.TotalBytes)}";

                MessageBox.Show(message, "同步完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "同步完成";
                lblSyncProgress.Text = $"同步完成！已复制 {result.CopiedFiles} 个文件，" +
                    $"跳过 {result.SkippedFiles} 个，失败 {result.FailedFiles} 个";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "同步已取消";
                lblSyncProgress.Text = "同步已取消";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"同步失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "同步失败";
            }
            finally
            {
                SetSyncingState(false);
            }
        }

        private string SyncProgressInfo_FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void BtnStopSync_Click(object sender, EventArgs e)
        {
            _syncCts?.Cancel();
            lblStatus.Text = "正在取消同步...";
        }

        private void SetSyncingState(bool syncing)
        {
            _isSyncing = syncing;
            btnStartSync.Enabled = !syncing;
            btnStopSync.Enabled = syncing;
            txtSourcePath.Enabled = !syncing;
            txtDestPath.Enabled = !syncing;
            btnBrowseSource.Enabled = !syncing;
            btnBrowseDest.Enabled = !syncing;
        }

        // ==================== 统计图表事件处理 ====================

        /// <summary>
        /// 刷新统计数据并更新图表
        /// </summary>
        private void BtnRefreshStats_Click(object sender, EventArgs e)
        {
            try
            {
                // 从数据库获取最新文件列表（如果内存中没有则加载数据库数据）
                if (_currentFiles == null || _currentFiles.Count == 0)
                {
                    _currentFiles = _databaseService.GetAllFiles();
                }

                if (_currentFiles.Count == 0)
                {
                    lblSummary.Text = "暂无文件数据，请先在"文件浏览器"中扫描目录。";
                    return;
                }

                // 计算文件类型分布
                DataTable typeDist = _statisticsService.CalculateFileTypeDistribution(_currentFiles);
                dgvStatistics.DataSource = typeDist;

                // 准备图表数据（需要Label和Value两列）
                DataTable chartData = new DataTable();
                chartData.Columns.Add("Label", typeof(string));
                chartData.Columns.Add("Value", typeof(double));
                foreach (DataRow row in typeDist.Rows)
                {
                    chartData.Rows.Add(row["FileType"], row["Count"]);
                }

                // 更新图表
                chartPie.SetDataSource(chartData, "Label", "Value");
                chartBar.SetDataSource(chartData, "Label", "Value");

                // 更新统计摘要
                var summary = _statisticsService.CalculateSummary(_currentFiles);
                lblSummary.Text = $"📊 总文件: {summary.TotalFiles} | " +
                    $"总大小: {summary.TotalSizeFormatted} | " +
                    $"🖼 图片: {summary.ImageCount} | " +
                    $"🎵 音频: {summary.AudioCount} | " +
                    $"🎬 视频: {summary.VideoCount} | " +
                    $"📄 文档: {summary.DocumentCount} | " +
                    $"📦 其他: {summary.OtherCount}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"统计刷新失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== 数据库管理事件处理 ====================

        /// <summary>
        /// 从数据库加载文件信息
        /// </summary>
        private void BtnLoadDB_Click(object sender, EventArgs e)
        {
            try
            {
                var files = _databaseService.GetAllFiles();
                dgvDatabase.DataSource = files.Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FileType,
                    大小 = f.FileSizeFormatted,
                    f.Extension,
                    修改日期 = f.LastModified.ToString("yyyy-MM-dd HH:mm"),
                    f.FullPath
                }).ToList();

                var history = _databaseService.GetSyncHistory();
                dgvSyncHistory.DataSource = history.Select(h => new
                {
                    h.Id,
                    源目录 = h.SourcePath,
                    目标目录 = h.DestinationPath,
                    开始时间 = h.StartTime.ToString("yyyy-MM-dd HH:mm"),
                    状态 = h.Status,
                    总文件 = h.TotalFiles,
                    已复制 = h.CopiedFiles,
                    已跳过 = h.SkippedFiles,
                    失败 = h.FailedFiles
                }).ToList();

                lblStatus.Text = $"已加载 {files.Count} 条文件记录";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库加载失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出文件列表到CSV文件
        /// </summary>
        private void ExportToCSV()
        {
            if (_currentFiles == null || _currentFiles.Count == 0)
            {
                _currentFiles = _databaseService.GetAllFiles();
            }

            if (_currentFiles.Count == 0)
            {
                MessageBox.Show("没有可导出的文件数据。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV文件|*.csv";
                sfd.FileName = $"文件列表_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8))
                        {
                            // 写入表头
                            writer.WriteLine("文件名,大小(字节),格式化大小,类型,扩展名,修改日期,完整路径");

                            // 写入数据行
                            foreach (var file in _currentFiles)
                            {
                                writer.WriteLine($"\"{file.FileName}\",{file.FileSize},\"{file.FileSizeFormatted}\"," +
                                    $"\"{file.FileType}\",\"{file.Extension}\"," +
                                    $"\"{file.LastModified:yyyy-MM-dd HH:mm:ss}\",\"{file.FullPath}\"");
                            }
                        }

                        MessageBox.Show($"成功导出 {_currentFiles.Count} 条记录到:\n{sfd.FileName}",
                            "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = $"已导出 {_currentFiles.Count} 条记录";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导出失败: {ex.Message}", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 清空数据库
        /// </summary>
        private void ClearDatabase()
        {
            var result = MessageBox.Show("确定要清空所有数据库记录吗？此操作不可撤销。",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _databaseService.ClearAllFiles();
                    lblStatus.Text = "数据库已清空";
                    dgvDatabase.DataSource = null;
                    dgvSyncHistory.DataSource = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 刷新所有视图
        /// </summary>
        private void RefreshAll()
        {
            BtnRefreshStats_Click(null, null);
            BtnLoadDB_Click(null, null);
            lblStatus.Text = "视图已刷新";
        }

        /// <summary>
        /// 窗体关闭时的清理工作
        /// 取消所有后台任务，释放资源
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 取消所有正在进行的异步操作
            _scanCts?.Cancel();
            _syncCts?.Cancel();

            // 释放资源
            _scanCts?.Dispose();
            _syncCts?.Dispose();
        }
    }
}
