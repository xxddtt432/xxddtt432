using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaFileManager.Models;

namespace MediaFileManager.Services
{
    /// <summary>
    /// 文件扫描服务类
    /// 使用多线程技术（Task Parallel Library）实现快速的文件系统扫描
    /// 支持递归遍历目录、文件过滤、进度报告和取消操作
    ///
    /// 技术要点：
    /// - Task.Run()：将耗时操作委托给线程池（ThreadPool），避免阻塞UI线程
    /// - IProgress&lt;T&gt;：推荐的跨线程UI更新模式，Progress&lt;T&gt;自动封送回调到UI线程
    /// - CancellationToken：协作式取消模式，支持优雅的中断操作
    /// - Directory.EnumerateFiles：延迟枚举，内存效率优于GetFiles
    /// - LINQ：用于过滤和排序文件列表
    /// </summary>
    public class FileScannerService
    {
        /// <summary>
        /// 异步扫描指定目录下的所有文件
        ///
        /// 设计思路：
        /// 1. 使用Task.Run将扫描工作放到线程池线程，不阻塞UI
        /// 2. 使用IProgress报告扫描进度到UI线程
        /// 3. 使用CancellationToken支持取消
        /// 4. 递归遍历子目录（使用SearchOption.AllDirectories）
        /// </summary>
        /// <param name="rootPath">要扫描的根目录路径</param>
        /// <param name="searchPattern">文件搜索模式，如 "*.*" 或 "*.mp3"</param>
        /// <param name="progress">进度报告器，自动封送回UI线程</param>
        /// <param name="cancellationToken">取消令牌，用于协作式取消</param>
        /// <returns>扫描到的文件信息列表</returns>
        public Task<List<MediaFileInfo>> ScanDirectoryAsync(
            string rootPath,
            string searchPattern,
            IProgress<ScanProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var files = new List<MediaFileInfo>();

                // 验证目录是否存在
                if (!Directory.Exists(rootPath))
                    throw new DirectoryNotFoundException($"目录不存在: {rootPath}");

                // 获取所有匹配的文件路径（使用延迟枚举减少内存占用）
                var filePaths = Directory.EnumerateFiles(
                    rootPath,
                    string.IsNullOrEmpty(searchPattern) ? "*.*" : searchPattern,
                    SearchOption.AllDirectories).ToList();

                int totalFiles = filePaths.Count;
                int processedCount = 0;

                // 遍历每个文件，提取元数据
                foreach (string filePath in filePaths)
                {
                    // 协作式取消检查：每次迭代检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        FileInfo fileInfo = new FileInfo(filePath);
                        MediaFileInfo mediaFile = new MediaFileInfo
                        {
                            FileName = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            FileSize = fileInfo.Length,
                            Extension = fileInfo.Extension,
                            FileType = MediaFileInfo.ClassifyFileType(fileInfo.Extension),
                            CreationTime = fileInfo.CreationTime,
                            LastModified = fileInfo.LastWriteTime
                        };
                        files.Add(mediaFile);
                    }
                    catch (Exception)
                    {
                        // 跳过无法访问的文件（权限问题、被占用等）
                        // 使用空catch处理特定文件的访问异常，不中断整体扫描
                    }

                    processedCount++;

                    // 每处理10个文件报告一次进度（减少UI更新频率）
                    if (processedCount % 10 == 0 || processedCount == totalFiles)
                    {
                        progress?.Report(new ScanProgressInfo
                        {
                            CurrentFile = filePath,
                            ProcessedCount = processedCount,
                            TotalCount = totalFiles,
                            PercentComplete = (int)(processedCount * 100.0 / totalFiles)
                        });
                    }
                }

                return files;
            }, cancellationToken);
        }

        /// <summary>
        /// 快速扫描（同步版本，用于简单场景）
        /// 直接在当前线程执行，适合小目录或后台已在线程池中的情况
        /// </summary>
        public List<MediaFileInfo> ScanDirectory(string rootPath)
        {
            var files = new List<MediaFileInfo>();
            if (!Directory.Exists(rootPath))
                return files;

            foreach (string filePath in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    FileInfo fi = new FileInfo(filePath);
                    files.Add(new MediaFileInfo
                    {
                        FileName = fi.Name,
                        FullPath = fi.FullName,
                        FileSize = fi.Length,
                        Extension = fi.Extension,
                        FileType = MediaFileInfo.ClassifyFileType(fi.Extension),
                        CreationTime = fi.CreationTime,
                        LastModified = fi.LastWriteTime
                    });
                }
                catch
                {
                    // 跳过无法访问的文件
                }
            }
            return files;
        }
    }

    /// <summary>
    /// 扫描进度信息数据结构
    /// 通过IProgress&lt;T&gt;传递给UI层
    /// </summary>
    public class ScanProgressInfo
    {
        /// <summary>当前正在处理的文件路径</summary>
        public string CurrentFile { get; set; }

        /// <summary>已处理的文件数</summary>
        public int ProcessedCount { get; set; }

        /// <summary>文件总数</summary>
        public int TotalCount { get; set; }

        /// <summary>完成百分比</summary>
        public int PercentComplete { get; set; }
    }
}
