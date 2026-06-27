# MediaVault - 个人多媒体文件管理中心

## 项目概述
MediaVault 是一个基于 C# WinForms 的个人多媒体文件管理工具，提供文件浏览、媒体播放、文件同步备份、元数据管理、统计图表展示等一站式文件管理功能。

## 项目背景
在日常工作和学习中，用户面临多媒体文件（图片、音频、视频、文档）分散存储、难以统一管理的问题。MediaVault 旨在提供一个轻量级、高效的本地文件管理解决方案，集成媒体播放和文件同步备份功能。

## 核心功能

### 1. 文件浏览器
- 树形目录导航（TreeView）
- 文件列表展示（ListView，支持详细信息视图）
- 文件搜索与过滤（按文件名、类型、大小）
- 右键上下文菜单（打开、删除、复制路径）

### 2. 媒体播放器
- 集成 Windows Media Player 控件
- 支持常见音频格式：MP3, WAV, WMA
- 支持常见视频格式：MP4, AVI, WMV
- 播放列表管理（添加、删除、清空）

### 3. 文件同步备份
- 源目录 → 目标目录同步
- 增量同步（仅复制新增/修改的文件）
- 实时进度显示（ProgressBar + 百分比）
- 多线程异步执行，不阻塞 UI
- 支持取消操作

### 4. 数据库管理
- SQLite 本地数据库存储文件元数据
- 文件信息表：名称、路径、大小、类型、修改日期
- 同步历史记录表
- 播放列表持久化

### 5. 统计图表
- 文件类型分布饼图（GDI+ 绘制）
- 存储空间使用柱状图
- 最近同步活动时间线

### 6. 数据导入导出
- 导出文件列表到 CSV/Excel 格式
- 从 CSV 导入文件列表
- DataGridView 展示表格数据

## 技术选型

| 技术 | 选型 | 理由 |
|------|------|------|
| 开发框架 | .NET Framework 4.7.2 WinForms | 课程教学框架，成熟稳定 |
| 数据库 | SQLite (System.Data.SQLite) | 轻量级嵌入式数据库，零配置 |
| Excel处理 | 原生文件IO + DataTable | 不依赖第三方库 |
| 媒体播放 | Windows Media Player COM | 系统自带，支持广泛格式 |
| 多线程 | Task + async/await | 现代C#异步编程模式 |
| 图形绘制 | GDI+ (System.Drawing) | .NET原生图形库 |

## 系统架构

```
MediaVault/
├── Forms/
│   └── MainForm.cs            # 主窗体（TabControl多标签页）
├── Models/
│   ├── MediaFileInfo.cs       # 文件信息实体类
│   └── SyncJob.cs             # 同步作业实体类
├── Services/
│   ├── DatabaseService.cs     # 数据库服务（CRUD操作）
│   ├── FileScannerService.cs  # 文件扫描服务（多线程）
│   ├── FileSyncService.cs     # 文件同步服务（异步）
│   └── StatisticsService.cs   # 统计服务
├── Controls/
│   └── ChartControl.cs        # 自定义图表控件（GDI+）
├── Program.cs                 # 应用入口
└── App.config                 # 应用配置
```

## 开发模式

本项目采用 **RDD（README驱动开发）** 和 **TDD（测试驱动开发）** 相结合的模式：
- **RDD**: 先编写本文档明确需求和设计，再开始编码
- **TDD**: 为核心服务层编写单元测试，遵循 Red-Green-Refactor 循环

## 环境要求
- Windows 10/11
- .NET Framework 4.7.2+
- Visual Studio 2019+ 或 VS Code
- Windows Media Player（系统自带）

## 快速开始
1. 克隆项目到本地
2. 使用 Visual Studio 打开 MediaFileManager.sln
3. 还原 NuGet 包：System.Data.SQLite
4. 编译并运行项目

## 项目地址
https://github.com/xxddtt432/xxddtt432.git/
