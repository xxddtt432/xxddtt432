using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using MediaFileManager.Models;

namespace MediaFileManager.Services
{
    /// <summary>
    /// 数据库服务类
    /// 使用SQLite嵌入式数据库存储文件元数据和同步历史记录
    /// 封装了初始化、CRUD操作、事务处理等数据库核心功能
    ///
    /// 技术要点：
    /// - SQLite：轻量级、零配置的嵌入式数据库，适合桌面应用
    /// - 参数化查询：使用SQLiteParameter防止SQL注入
    /// - ADO.NET：使用IDbConnection/IDbCommand等接口实现数据库操作
    /// - 连接管理：使用using语句确保资源正确释放
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;

        /// <summary>
        /// 构造函数：初始化数据库连接字符串
        /// 数据库文件存储在应用程序目录下的 MediaVault.db
        /// </summary>
        public DatabaseService()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaVault.db");
            _connectionString = $"Data Source={dbPath};Version=3;";
        }

        /// <summary>
        /// 初始化数据库：创建所需的表结构（如不存在）
        /// 使用DDL语句定义表结构，包含主键、索引等
        /// </summary>
        public void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                // 创建文件信息表
                // 包含索引以优化按路径和类型的查询性能
                string createFileInfoTable = @"
                    CREATE TABLE IF NOT EXISTS FileInfo (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FileName TEXT NOT NULL,
                        FullPath TEXT UNIQUE NOT NULL,
                        FileSize INTEGER NOT NULL DEFAULT 0,
                        FileType TEXT NOT NULL DEFAULT 'Other',
                        Extension TEXT,
                        CreationTime TEXT,
                        LastModified TEXT,
                        DateAdded TEXT DEFAULT (datetime('now','localtime'))
                    );
                    CREATE INDEX IF NOT EXISTS idx_fullpath ON FileInfo(FullPath);
                    CREATE INDEX IF NOT EXISTS idx_filetype ON FileInfo(FileType);
                ";

                // 创建同步历史表
                // 记录每次同步操作的详细信息
                string createSyncHistoryTable = @"
                    CREATE TABLE IF NOT EXISTS SyncHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SourcePath TEXT NOT NULL,
                        DestinationPath TEXT NOT NULL,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT,
                        Status TEXT NOT NULL DEFAULT 'Running',
                        TotalFiles INTEGER DEFAULT 0,
                        CopiedFiles INTEGER DEFAULT 0,
                        SkippedFiles INTEGER DEFAULT 0,
                        FailedFiles INTEGER DEFAULT 0,
                        TotalBytes INTEGER DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS idx_sync_time ON SyncHistory(StartTime);
                ";

                using (var cmd = new SQLiteCommand(createFileInfoTable, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SQLiteCommand(createSyncHistoryTable, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 批量插入文件信息（使用事务提高性能）
        /// 事务确保数据一致性：要么全部插入，要么全部回滚
        ///
        /// 技术要点：
        /// - 事务处理：BEGIN/COMMIT/ROLLBACK
        /// - 批量操作优化：减少数据库锁定次数
        /// - INSERT OR REPLACE：处理重复路径的文件记录
        /// </summary>
        public void BatchInsertFiles(List<MediaFileInfo> files)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string sql = @"INSERT OR REPLACE INTO FileInfo
                            (FileName, FullPath, FileSize, FileType, Extension, CreationTime, LastModified)
                            VALUES (@FileName, @FullPath, @FileSize, @FileType, @Extension, @CreationTime, @LastModified)";

                        foreach (var file in files)
                        {
                            using (var cmd = new SQLiteCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@FileName", file.FileName);
                                cmd.Parameters.AddWithValue("@FullPath", file.FullPath);
                                cmd.Parameters.AddWithValue("@FileSize", file.FileSize);
                                cmd.Parameters.AddWithValue("@FileType", file.FileType);
                                cmd.Parameters.AddWithValue("@Extension", file.Extension);
                                cmd.Parameters.AddWithValue("@CreationTime", file.CreationTime.ToString("o"));
                                cmd.Parameters.AddWithValue("@LastModified", file.LastModified.ToString("o"));
                                cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 查询所有文件信息
        /// 返回List集合，支持后续LINQ查询和绑定到UI控件
        /// </summary>
        public List<MediaFileInfo> GetAllFiles()
        {
            var files = new List<MediaFileInfo>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT * FROM FileInfo ORDER BY LastModified DESC";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        files.Add(MapReaderToFileInfo(reader));
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// 按文件类型查询文件列表
        /// 使用参数化查询防止SQL注入
        /// </summary>
        public List<MediaFileInfo> GetFilesByType(string fileType)
        {
            var files = new List<MediaFileInfo>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT * FROM FileInfo WHERE FileType = @FileType ORDER BY LastModified DESC";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@FileType", fileType);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            files.Add(MapReaderToFileInfo(reader));
                        }
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// 按文件名搜索（模糊匹配）
        /// 使用LIKE进行模糊查询，支持通配符
        /// </summary>
        public List<MediaFileInfo> SearchFiles(string keyword)
        {
            var files = new List<MediaFileInfo>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT * FROM FileInfo WHERE FileName LIKE @Keyword ORDER BY LastModified DESC";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@Keyword", $"%{keyword}%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            files.Add(MapReaderToFileInfo(reader));
                        }
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// 保存同步历史记录
        /// </summary>
        public int SaveSyncJob(SyncJob job)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = @"INSERT INTO SyncHistory
                    (SourcePath, DestinationPath, StartTime, EndTime, Status, TotalFiles, CopiedFiles, SkippedFiles, FailedFiles, TotalBytes)
                    VALUES (@SourcePath, @DestinationPath, @StartTime, @EndTime, @Status, @TotalFiles, @CopiedFiles, @SkippedFiles, @FailedFiles, @TotalBytes);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@SourcePath", job.SourcePath);
                    cmd.Parameters.AddWithValue("@DestinationPath", job.DestinationPath);
                    cmd.Parameters.AddWithValue("@StartTime", job.StartTime.ToString("o"));
                    cmd.Parameters.AddWithValue("@EndTime", job.EndTime?.ToString("o") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", job.Status);
                    cmd.Parameters.AddWithValue("@TotalFiles", job.TotalFiles);
                    cmd.Parameters.AddWithValue("@CopiedFiles", job.CopiedFiles);
                    cmd.Parameters.AddWithValue("@SkippedFiles", job.SkippedFiles);
                    cmd.Parameters.AddWithValue("@FailedFiles", job.FailedFiles);
                    cmd.Parameters.AddWithValue("@TotalBytes", job.TotalBytes);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// 获取同步历史记录
        /// </summary>
        public List<SyncJob> GetSyncHistory()
        {
            var jobs = new List<SyncJob>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT * FROM SyncHistory ORDER BY StartTime DESC LIMIT 20";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        jobs.Add(new SyncJob
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            SourcePath = reader["SourcePath"].ToString(),
                            DestinationPath = reader["DestinationPath"].ToString(),
                            StartTime = DateTime.Parse(reader["StartTime"].ToString()),
                            EndTime = reader["EndTime"] != DBNull.Value ? DateTime.Parse(reader["EndTime"].ToString()) : (DateTime?)null,
                            Status = reader["Status"].ToString(),
                            TotalFiles = Convert.ToInt32(reader["TotalFiles"]),
                            CopiedFiles = Convert.ToInt32(reader["CopiedFiles"]),
                            SkippedFiles = Convert.ToInt32(reader["SkippedFiles"]),
                            FailedFiles = Convert.ToInt32(reader["FailedFiles"]),
                            TotalBytes = Convert.ToInt64(reader["TotalBytes"])
                        });
                    }
                }
            }
            return jobs;
        }

        /// <summary>
        /// 获取各类型文件的统计数量
        /// 使用GROUP BY聚合查询，返回DataTable便于绑定到图表
        /// </summary>
        public DataTable GetFileTypeStatistics()
        {
            var dt = new DataTable();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = @"SELECT FileType, COUNT(*) as Count, SUM(FileSize) as TotalSize
                               FROM FileInfo GROUP BY FileType ORDER BY Count DESC";
                using (var adapter = new SQLiteDataAdapter(sql, connection))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        /// <summary>
        /// 清空文件信息表
        /// </summary>
        public void ClearAllFiles()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM FileInfo", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 从IDataReader映射到MediaFileInfo实体
        /// 私有辅助方法，封装数据行到对象的转换逻辑
        /// </summary>
        private MediaFileInfo MapReaderToFileInfo(IDataReader reader)
        {
            return new MediaFileInfo
            {
                Id = Convert.ToInt32(reader["Id"]),
                FileName = reader["FileName"].ToString(),
                FullPath = reader["FullPath"].ToString(),
                FileSize = Convert.ToInt64(reader["FileSize"]),
                FileType = reader["FileType"].ToString(),
                Extension = reader["Extension"].ToString(),
                CreationTime = SafeParseDateTime(reader["CreationTime"]),
                LastModified = SafeParseDateTime(reader["LastModified"])
            };
        }

        /// <summary>
        /// 安全解析日期时间，处理空值和格式异常
        /// 使用TryParse进行容错处理
        /// </summary>
        private DateTime SafeParseDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
                return DateTime.MinValue;
            if (DateTime.TryParse(value.ToString(), out DateTime result))
                return result;
            return DateTime.MinValue;
        }
    }
}
