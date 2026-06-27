using System;

namespace MediaFileManager.Models
{
    /// <summary>
    /// 文件同步作业实体类
    /// 记录同步操作的详细信息，包括源/目标路径、状态、进度、时间等
    /// 用于数据库持久化和UI进度展示
    /// </summary>
    public class SyncJob
    {
        /// <summary>同步作业唯一标识（数据库自增ID）</summary>
        public int Id { get; set; }

        /// <summary>源目录路径</summary>
        public string SourcePath { get; set; }

        /// <summary>目标目录路径</summary>
        public string DestinationPath { get; set; }

        /// <summary>同步开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>同步结束时间（完成或取消）</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>同步状态：Running/Completed/Cancelled/Failed</summary>
        public string Status { get; set; }

        /// <summary>计划同步的文件总数</summary>
        public int TotalFiles { get; set; }

        /// <summary>已同步完成的文件数</summary>
        public int CopiedFiles { get; set; }

        /// <summary>跳过的文件数（目标已存在且未修改）</summary>
        public int SkippedFiles { get; set; }

        /// <summary>同步失败的文件数</summary>
        public int FailedFiles { get; set; }

        /// <summary>同步的总字节数</summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 计算同步进度百分比（0-100）
        /// 使用三元运算符防止除零异常
        /// </summary>
        public int ProgressPercent =>
            TotalFiles > 0 ? (int)((CopiedFiles + SkippedFiles + FailedFiles) * 100.0 / TotalFiles) : 0;

        /// <summary>
        /// 获取同步耗时（已完成时）或已用时（运行中时）
        /// </summary>
        public TimeSpan Duration =>
            (EndTime ?? DateTime.Now) - StartTime;

        /// <summary>
        /// 格式化同步状态为可读字符串
        /// </summary>
        public override string ToString()
        {
            return $"[{Status}] {SourcePath} → {DestinationPath} | " +
                   $"进度: {ProgressPercent}% | " +
                   $"文件: {CopiedFiles}已复制/{SkippedFiles}已跳过/{FailedFiles}失败 | " +
                   $"耗时: {Duration.TotalSeconds:F1}s";
        }
    }
}
