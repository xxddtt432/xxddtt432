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
    /// 文件同步服务类
    /// 实现增量文件同步备份功能，将源目录的文件复制到目标目录
    ///
    /// 技术要点：
    /// - 增量同步：比较源文件和目标文件的最后修改时间，仅复制变化的文件
    /// - async/await：异步编程模式，使用Task.Run + await实现非阻塞操作
    /// - IProgress&lt;T&gt;：跨线程进度报告，自动封送回UI线程
    /// - CancellationToken：协作式取消
    /// - 文件I/O：使用File.Copy进行文件复制，FileInfo获取元数据
    /// - 异常处理：单个文件失败不影响整体同步
    /// </summary>
    public class FileSyncService
    {
        private readonly DatabaseService _databaseService;

        public FileSyncService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// 启动异步文件同步任务
        ///
        /// 同步策略（增量同步）：
        /// 1. 扫描源目录获取所有文件
        /// 2. 对于每个源文件，检查目标路径是否存在
        /// 3. 如果目标文件不存在 → 复制
        /// 4. 如果目标文件存在但源文件更新 → 复制（覆盖）
        /// 5. 如果目标文件存在且更新 → 跳过
        ///
        /// 设计模式：
        /// - 使用Task.Run将耗时I/O操作放入线程池
        /// - 使用IProgress报告进度
        /// - 使用CancellationToken支持取消
        /// </summary>
        /// <param name="sourcePath">源目录路径</param>
        /// <param name="destinationPath">目标目录路径</param>
        /// <param name="progress">进度报告器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>同步结果摘要字符串</returns>
        public async Task<SyncJob> SyncDirectoriesAsync(
            string sourcePath,
            string destinationPath,
            IProgress<SyncProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            // 创建同步作业记录
            var syncJob = new SyncJob
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                StartTime = DateTime.Now,
                Status = "Running"
            };

            // 使用Task.Run将整个同步操作委托给线程池
            return await Task.Run(() =>
            {
                // 确保目标目录存在
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                // 获取源目录中所有文件
                var sourceFiles = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
                syncJob.TotalFiles = sourceFiles.Length;

                for (int i = 0; i < sourceFiles.Length; i++)
                {
                    // 协作式取消：每处理一个文件前检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();

                    string sourceFile = sourceFiles[i];
                    try
                    {
                        // 计算目标文件的对应路径（保持目录结构）
                        string relativePath = GetRelativePath(sourcePath, sourceFile);
                        string destFile = Path.Combine(destinationPath, relativePath);

                        // 确保目标文件的目录存在
                        string destDir = Path.GetDirectoryName(destFile);
                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        FileInfo sourceFileInfo = new FileInfo(sourceFile);
                        bool shouldCopy = false;

                        // 增量同步判断逻辑
                        if (!File.Exists(destFile))
                        {
                            // 目标文件不存在 → 需要复制
                            shouldCopy = true;
                        }
                        else
                        {
                            // 目标文件存在 → 比较最后修改时间
                            FileInfo destFileInfo = new FileInfo(destFile);
                            if (sourceFileInfo.LastWriteTime > destFileInfo.LastWriteTime)
                            {
                                // 源文件更新 → 需要复制（覆盖）
                                shouldCopy = true;
                            }
                        }

                        if (shouldCopy)
                        {
                            // 复制文件，overwrite=true覆盖目标
                            File.Copy(sourceFile, destFile, true);
                            syncJob.CopiedFiles++;
                            syncJob.TotalBytes += sourceFileInfo.Length;
                        }
                        else
                        {
                            // 目标已是最新 → 跳过
                            syncJob.SkippedFiles++;
                        }
                    }
                    catch (Exception)
                    {
                        // 单个文件复制失败不中断整体同步
                        // 记录失败计数，继续处理下一个文件
                        syncJob.FailedFiles++;
                    }

                    // 报告进度
                    progress?.Report(new SyncProgressInfo
                    {
                        CurrentFile = sourceFile,
                        CopiedCount = syncJob.CopiedFiles,
                        SkippedCount = syncJob.SkippedFiles,
                        FailedCount = syncJob.FailedFiles,
                        TotalCount = syncJob.TotalFiles,
                        PercentComplete = syncJob.ProgressPercent,
                        BytesTransferred = syncJob.TotalBytes
                    });

                    // 每复制10个文件暂停1ms，让出CPU时间片给其他任务
                    if (syncJob.CopiedFiles % 10 == 0)
                    {
                        Thread.Sleep(1);
                    }
                }

                // 同步完成，更新作业状态
                syncJob.Status = "Completed";
                syncJob.EndTime = DateTime.Now;

                // 将同步记录持久化到数据库
                try
                {
                    _databaseService.SaveSyncJob(syncJob);
                }
                catch
                {
                    // 数据库记录失败不影响同步结果
                }

                return syncJob;
            }, cancellationToken);
        }

        /// <summary>
        /// 计算相对路径
        /// 将绝对路径转换为相对于根目录的路径
        /// 例如：root=C:\Data, file=C:\Data\Docs\a.txt → Docs\a.txt
        /// </summary>
        private string GetRelativePath(string rootPath, string fullPath)
        {
            // 规范化路径分隔符
            rootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            fullPath = Path.GetFullPath(fullPath);

            // 从完整路径中移除根路径前缀
            string relativePath = fullPath.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relativePath;
        }
    }

    /// <summary>
    /// 同步进度信息数据结构
    /// 通过IProgress&lt;T&gt;传递给UI层
    /// </summary>
    public class SyncProgressInfo
    {
        /// <summary>当前正在处理的文件路径</summary>
        public string CurrentFile { get; set; }

        /// <summary>已复制文件数</summary>
        public int CopiedCount { get; set; }

        /// <summary>已跳过文件数</summary>
        public int SkippedCount { get; set; }

        /// <summary>失败文件数</summary>
        public int FailedCount { get; set; }

        /// <summary>总文件数</summary>
        public int TotalCount { get; set; }

        /// <summary>完成百分比</summary>
        public int PercentComplete { get; set; }

        /// <summary>已传输字节数</summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        /// 格式化字节数为可读字符串
        /// </summary>
        public string BytesTransferredFormatted
        {
            get
            {
                if (BytesTransferred < 1024) return $"{BytesTransferred} B";
                if (BytesTransferred < 1024 * 1024) return $"{BytesTransferred / 1024.0:F1} KB";
                if (BytesTransferred < 1024 * 1024 * 1024) return $"{BytesTransferred / (1024.0 * 1024.0):F2} MB";
                return $"{BytesTransferred / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }
}
