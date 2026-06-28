using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using MediaFileManager.Models;

namespace MediaFileManager.Services
{
    /// <summary>
    /// 数据存储服务类（内存版）
    /// 使用静态内存列表存储，应用关闭时自动序列化到JSON文本文件
    /// 启动时自动从文件加载，实现跨会话数据持久化
    ///
    /// 技术说明：原始版本使用SQLite，为便于演示和编译
    /// 这里改为内存存储 + JSON文件持久化，无需任何第三方NuGet包
    /// 所有公开方法签名保持不变，UI层代码完全不用修改
    /// </summary>
    public class DatabaseService
    {
        // ==================== 内存存储 ====================
        private static List<MediaFileInfo> _fileCache = new List<MediaFileInfo>();
        private static List<SyncJob> _syncHistory = new List<SyncJob>();
        private static int _nextFileId = 1;
        private static int _nextSyncId = 1;
        private static readonly object _lock = new object();

        private readonly string _dataFilePath;

        public DatabaseService()
        {
            _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaVault_Data.json");
            LoadFromDisk();
        }

        /// <summary>
        /// 初始化数据库（建表）—— 改为从磁盘加载数据
        /// </summary>
        public void InitializeDatabase()
        {
            // 数据在构造函数中自动加载，此处无操作
        }

        // ==================== 文件信息 CRUD ====================

        /// <summary>
        /// 批量插入文件信息
        /// 使用lock确保线程安全，新记录分配自增ID
        /// 重复路径（FullPath相同）时替换旧记录
        /// </summary>
        public void BatchInsertFiles(List<MediaFileInfo> files)
        {
            lock (_lock)
            {
                foreach (var file in files)
                {
                    // 检查是否已有相同路径的记录，有则替换
                    var existing = _fileCache.Find(f =>
                        string.Equals(f.FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        // 更新现有记录
                        existing.FileName = file.FileName;
                        existing.FileSize = file.FileSize;
                        existing.FileType = file.FileType;
                        existing.Extension = file.Extension;
                        existing.CreationTime = file.CreationTime;
                        existing.LastModified = file.LastModified;
                    }
                    else
                    {
                        file.Id = _nextFileId++;
                        _fileCache.Add(file);
                    }
                }
                SaveToDisk();
            }
        }

        /// <summary>
        /// 查询所有文件信息
        /// </summary>
        public List<MediaFileInfo> GetAllFiles()
        {
            lock (_lock)
            {
                return _fileCache.OrderByDescending(f => f.LastModified).ToList();
            }
        }

        /// <summary>
        /// 按文件类型查询
        /// </summary>
        public List<MediaFileInfo> GetFilesByType(string fileType)
        {
            lock (_lock)
            {
                return _fileCache
                    .Where(f => f.FileType == fileType)
                    .OrderByDescending(f => f.LastModified)
                    .ToList();
            }
        }

        /// <summary>
        /// 按文件名模糊搜索
        /// </summary>
        public List<MediaFileInfo> SearchFiles(string keyword)
        {
            lock (_lock)
            {
                return _fileCache
                    .Where(f => f.FileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(f => f.LastModified)
                    .ToList();
            }
        }

        /// <summary>
        /// 清空所有文件记录
        /// </summary>
        public void ClearAllFiles()
        {
            lock (_lock)
            {
                _fileCache.Clear();
                _nextFileId = 1;
                SaveToDisk();
            }
        }

        // ==================== 同步历史 ====================

        /// <summary>
        /// 保存同步作业记录
        /// </summary>
        public int SaveSyncJob(SyncJob job)
        {
            lock (_lock)
            {
                job.Id = _nextSyncId++;
                _syncHistory.Insert(0, job); // 最新在前
                // 最多保留50条历史
                if (_syncHistory.Count > 50)
                    _syncHistory = _syncHistory.Take(50).ToList();
                SaveToDisk();
                return job.Id;
            }
        }

        /// <summary>
        /// 获取同步历史记录（最近20条）
        /// </summary>
        public List<SyncJob> GetSyncHistory()
        {
            lock (_lock)
            {
                return _syncHistory.Take(20).ToList();
            }
        }

        // ==================== 统计查询 ====================

        /// <summary>
        /// 获取各类型文件的统计数量
        /// 返回DataTable用于绑定DataGridView和图表
        /// </summary>
        public DataTable GetFileTypeStatistics()
        {
            var dt = new DataTable();
            dt.Columns.Add("FileType", typeof(string));
            dt.Columns.Add("Count", typeof(int));
            dt.Columns.Add("TotalSize", typeof(long));

            List<MediaFileInfo> files;
            lock (_lock)
            {
                files = new List<MediaFileInfo>(_fileCache);
            }

            var grouped = files
                .GroupBy(f => f.FileType)
                .Select(g => new
                {
                    FileType = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(f => f.FileSize)
                })
                .OrderByDescending(g => g.Count);

            foreach (var g in grouped)
            {
                dt.Rows.Add(g.FileType, g.Count, g.TotalSize);
            }

            return dt;
        }

        // ==================== 磁盘持久化 ====================

        /// <summary>
        /// 将内存数据保存到JSON文本文件
        /// 手动序列化，不依赖第三方库
        /// </summary>
        private void SaveToDisk()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"nextFileId\": " + _nextFileId + ",");
                sb.AppendLine("  \"nextSyncId\": " + _nextSyncId + ",");
                sb.AppendLine("  \"files\": [");

                for (int i = 0; i < _fileCache.Count; i++)
                {
                    var f = _fileCache[i];
                    sb.Append("    {");
                    sb.Append($"\"Id\":{f.Id},");
                    sb.Append($"\"FileName\":\"{EscapeJson(f.FileName)}\",");
                    sb.Append($"\"FullPath\":\"{EscapeJson(f.FullPath)}\",");
                    sb.Append($"\"FileSize\":{f.FileSize},");
                    sb.Append($"\"FileType\":\"{f.FileType}\",");
                    sb.Append($"\"Extension\":\"{EscapeJson(f.Extension ?? "")}\",");
                    sb.Append($"\"CreationTime\":\"{f.CreationTime:o}\",");
                    sb.Append($"\"LastModified\":\"{f.LastModified:o}\"");
                    sb.Append("}");
                    if (i < _fileCache.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("  ],");
                sb.AppendLine("  \"syncHistory\": [");

                for (int i = 0; i < _syncHistory.Count; i++)
                {
                    var s = _syncHistory[i];
                    sb.Append("    {");
                    sb.Append($"\"Id\":{s.Id},");
                    sb.Append($"\"SourcePath\":\"{EscapeJson(s.SourcePath)}\",");
                    sb.Append($"\"DestinationPath\":\"{EscapeJson(s.DestinationPath)}\",");
                    sb.Append($"\"StartTime\":\"{s.StartTime:o}\",");
                    sb.Append($"\"EndTime\":\"{(s.EndTime.HasValue ? s.EndTime.Value.ToString("o") : "null")}\",");
                    sb.Append($"\"Status\":\"{s.Status}\",");
                    sb.Append($"\"TotalFiles\":{s.TotalFiles},");
                    sb.Append($"\"CopiedFiles\":{s.CopiedFiles},");
                    sb.Append($"\"SkippedFiles\":{s.SkippedFiles},");
                    sb.Append($"\"FailedFiles\":{s.FailedFiles},");
                    sb.Append($"\"TotalBytes\":{s.TotalBytes}");
                    sb.Append("}");
                    if (i < _syncHistory.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(_dataFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // 保存失败不影响程序运行
            }
        }

        /// <summary>
        /// 从JSON文本文件加载数据到内存
        /// 简易解析器，不依赖第三方库
        /// </summary>
        private void LoadFromDisk()
        {
            if (!File.Exists(_dataFilePath))
                return;

            try
            {
                string json = File.ReadAllText(_dataFilePath, Encoding.UTF8);

                // 解析 nextFileId
                _nextFileId = ExtractInt(json, "\"nextFileId\":", 1);
                _nextSyncId = ExtractInt(json, "\"nextSyncId\":", 1);

                // 解析 files 数组
                int filesStart = json.IndexOf("\"files\":[");
                int filesEnd = json.IndexOf("]", filesStart);
                if (filesStart >= 0 && filesEnd > filesStart)
                {
                    string filesSection = json.Substring(filesStart + 9, filesEnd - filesStart - 9); // skip "files":[
                    _fileCache = ParseFileArray(filesSection);
                }

                // 解析 syncHistory 数组
                int histStart = json.IndexOf("\"syncHistory\":[");
                int histEnd = json.LastIndexOf("]}");
                if (histStart >= 0 && histEnd > histStart)
                {
                    string histSection = json.Substring(histStart + 15, histEnd - histStart - 15); // skip "syncHistory":[
                    _syncHistory = ParseSyncArray(histSection);
                }
            }
            catch
            {
                // 加载失败使用空数据
                _fileCache = new List<MediaFileInfo>();
                _syncHistory = new List<SyncJob>();
            }
        }

        /// <summary>
        /// 从JSON字符串中提取整数值
        /// </summary>
        private static int ExtractInt(string json, string key, int defaultValue)
        {
            int idx = json.IndexOf(key);
            if (idx < 0) return defaultValue;
            idx += key.Length;
            int end = json.IndexOfAny(new[] { ',', '\n', '\r', '}' }, idx);
            if (end < 0) end = json.Length;
            string val = json.Substring(idx, end - idx).Trim();
            return int.TryParse(val, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 简易文件数组JSON解析器
        /// 按 "},{" 分割每个对象，然后提取字段值
        /// </summary>
        private static List<MediaFileInfo> ParseFileArray(string section)
        {
            var result = new List<MediaFileInfo>();
            // 按 "},{" 分割对象（对象之间）
            string[] objects = section.Split(new[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string obj in objects)
            {
                string clean = obj.Trim().TrimStart('{').TrimEnd('}');
                var file = new MediaFileInfo
                {
                    Id = ExtractIntFromObject(clean, "Id"),
                    FileName = ExtractStringFromObject(clean, "FileName"),
                    FullPath = ExtractStringFromObject(clean, "FullPath"),
                    FileSize = ExtractLongFromObject(clean, "FileSize"),
                    FileType = ExtractStringFromObject(clean, "FileType"),
                    Extension = ExtractStringFromObject(clean, "Extension"),
                    CreationTime = ExtractDateTimeFromObject(clean, "CreationTime"),
                    LastModified = ExtractDateTimeFromObject(clean, "LastModified")
                };
                if (!string.IsNullOrEmpty(file.FullPath))
                    result.Add(file);
            }
            return result;
        }

        /// <summary>
        /// 简易同步历史数组JSON解析器
        /// </summary>
        private static List<SyncJob> ParseSyncArray(string section)
        {
            var result = new List<SyncJob>();
            string[] objects = section.Split(new[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string obj in objects)
            {
                string clean = obj.Trim().TrimStart('{').TrimEnd('}');
                var job = new SyncJob
                {
                    Id = ExtractIntFromObject(clean, "Id"),
                    SourcePath = ExtractStringFromObject(clean, "SourcePath"),
                    DestinationPath = ExtractStringFromObject(clean, "DestinationPath"),
                    StartTime = ExtractDateTimeFromObject(clean, "StartTime"),
                    Status = ExtractStringFromObject(clean, "Status"),
                    TotalFiles = ExtractIntFromObject(clean, "TotalFiles"),
                    CopiedFiles = ExtractIntFromObject(clean, "CopiedFiles"),
                    SkippedFiles = ExtractIntFromObject(clean, "SkippedFiles"),
                    FailedFiles = ExtractIntFromObject(clean, "FailedFiles"),
                    TotalBytes = ExtractLongFromObject(clean, "TotalBytes")
                };
                string endTimeStr = ExtractStringFromObject(clean, "EndTime");
                if (!string.IsNullOrEmpty(endTimeStr) && endTimeStr != "null")
                {
                    if (DateTime.TryParse(endTimeStr, out DateTime et))
                        job.EndTime = et;
                }
                if (!string.IsNullOrEmpty(job.SourcePath))
                    result.Add(job);
            }
            return result;
        }

        // ==================== JSON字段提取辅助方法 ====================

        private static string ExtractStringFromObject(string obj, string key)
        {
            string search = $"\"{key}\":\"";
            int idx = obj.IndexOf(search);
            if (idx < 0) return "";
            idx += search.Length;
            int end = idx;
            while (end < obj.Length)
            {
                if (obj[end] == '"' && (end == 0 || obj[end - 1] != '\\'))
                    break;
                end++;
            }
            if (end > idx)
                return obj.Substring(idx, end - idx).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return "";
        }

        private static int ExtractIntFromObject(string obj, string key)
        {
            string search = $"\"{key}\":";
            int idx = obj.IndexOf(search);
            if (idx < 0) return 0;
            idx += search.Length;
            int end = obj.IndexOfAny(new[] { ',', '}', '\n', '\r' }, idx);
            if (end < 0) end = obj.Length;
            string val = obj.Substring(idx, end - idx).Trim();
            return int.TryParse(val, out int result) ? result : 0;
        }

        private static long ExtractLongFromObject(string obj, string key)
        {
            string search = $"\"{key}\":";
            int idx = obj.IndexOf(search);
            if (idx < 0) return 0;
            idx += search.Length;
            int end = obj.IndexOfAny(new[] { ',', '}', '\n', '\r' }, idx);
            if (end < 0) end = obj.Length;
            string val = obj.Substring(idx, end - idx).Trim();
            return long.TryParse(val, out long result) ? result : 0;
        }

        private static DateTime ExtractDateTimeFromObject(string obj, string key)
        {
            string val = ExtractStringFromObject(obj, key);
            if (string.IsNullOrEmpty(val)) return DateTime.MinValue;
            return DateTime.TryParse(val, out DateTime dt) ? dt : DateTime.MinValue;
        }

        /// <summary>
        /// JSON字符串转义
        /// </summary>
        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
