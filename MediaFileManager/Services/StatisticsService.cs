using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using MediaFileManager.Models;

namespace MediaFileManager.Services
{
    /// <summary>
    /// 统计服务类
    /// 提供文件统计数据的计算和分析功能
    /// 包括文件类型分布、存储空间占比、文件数量趋势等
    ///
    /// 技术要点：
    /// - LINQ：使用GroupBy、Sum、OrderBy等扩展方法进行数据聚合
    /// - DataTable：作为数据中间层，支持绑定到DataGridView和图表
    /// - 文件I/O：获取磁盘驱动器信息
    /// </summary>
    public class StatisticsService
    {
        /// <summary>
        /// 计算文件类型分布统计
        /// 使用LINQ的GroupBy进行分组聚合，Select投影创建匿名类型
        /// 然后转换为DataTable以便绑定到UI控件
        /// </summary>
        /// <param name="files">文件信息列表</param>
        /// <returns>包含类型统计的DataTable（列：FileType, Count, TotalSize, Percentage）</returns>
        public DataTable CalculateFileTypeDistribution(List<MediaFileInfo> files)
        {
            var dt = new DataTable();
            dt.Columns.Add("FileType", typeof(string));
            dt.Columns.Add("Count", typeof(int));
            dt.Columns.Add("TotalSize", typeof(long));
            dt.Columns.Add("TotalSizeFormatted", typeof(string));
            dt.Columns.Add("Percentage", typeof(double));

            if (files == null || files.Count == 0)
                return dt;

            int totalCount = files.Count;

            // LINQ分组聚合：按FileType分组，计算每组数量和大小总和
            var grouped = files
                .GroupBy(f => f.FileType)
                .Select(g => new
                {
                    FileType = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(f => f.FileSize),
                    Percentage = (double)g.Count() / totalCount * 100.0
                })
                .OrderByDescending(g => g.Count);

            // 填充DataTable
            foreach (var group in grouped)
            {
                dt.Rows.Add(
                    group.FileType,
                    group.Count,
                    group.TotalSize,
                    FormatFileSize(group.TotalSize),
                    Math.Round(group.Percentage, 1)
                );
            }

            return dt;
        }

        /// <summary>
        /// 获取文件数量前N的目录（热度分析）
        /// 使用LINQ分组统计每个目录中的文件数量
        /// </summary>
        public DataTable GetTopDirectories(List<MediaFileInfo> files, int topN = 10)
        {
            var dt = new DataTable();
            dt.Columns.Add("Directory", typeof(string));
            dt.Columns.Add("FileCount", typeof(int));
            dt.Columns.Add("TotalSize", typeof(long));
            dt.Columns.Add("TotalSizeFormatted", typeof(string));

            if (files == null || files.Count == 0)
                return dt;

            var grouped = files
                .GroupBy(f => Path.GetDirectoryName(f.FullPath) ?? "Unknown")
                .Select(g => new
                {
                    Directory = g.Key,
                    FileCount = g.Count(),
                    TotalSize = g.Sum(f => f.FileSize)
                })
                .OrderByDescending(g => g.FileCount)
                .Take(topN);

            foreach (var group in grouped)
            {
                dt.Rows.Add(
                    group.Directory,
                    group.FileCount,
                    group.TotalSize,
                    FormatFileSize(group.TotalSize)
                );
            }

            return dt;
        }

        /// <summary>
        /// 计算总体统计摘要
        /// </summary>
        public FileStatisticsSummary CalculateSummary(List<MediaFileInfo> files)
        {
            if (files == null || files.Count == 0)
                return new FileStatisticsSummary();

            return new FileStatisticsSummary
            {
                TotalFiles = files.Count,
                TotalSize = files.Sum(f => f.FileSize),
                ImageCount = files.Count(f => f.FileType == "Image"),
                AudioCount = files.Count(f => f.FileType == "Audio"),
                VideoCount = files.Count(f => f.FileType == "Video"),
                DocumentCount = files.Count(f => f.FileType == "Document"),
                OtherCount = files.Count(f => f.FileType == "Other"),
                AverageFileSize = (long)files.Average(f => f.FileSize),
                LargestFile = files.OrderByDescending(f => f.FileSize).FirstOrDefault(),
                NewestFile = files.OrderByDescending(f => f.LastModified).FirstOrDefault(),
                OldestFile = files.OrderBy(f => f.LastModified).FirstOrDefault()
            };
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// 文件统计摘要数据结构
    /// 汇聚所有统计指标
    /// </summary>
    public class FileStatisticsSummary
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int ImageCount { get; set; }
        public int AudioCount { get; set; }
        public int VideoCount { get; set; }
        public int DocumentCount { get; set; }
        public int OtherCount { get; set; }
        public long AverageFileSize { get; set; }
        public MediaFileInfo LargestFile { get; set; }
        public MediaFileInfo NewestFile { get; set; }
        public MediaFileInfo OldestFile { get; set; }

        public string TotalSizeFormatted
        {
            get
            {
                if (TotalSize < 1024) return $"{TotalSize} B";
                if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024.0:F1} KB";
                if (TotalSize < 1024 * 1024 * 1024) return $"{TotalSize / (1024.0 * 1024.0):F2} MB";
                return $"{TotalSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }
}
