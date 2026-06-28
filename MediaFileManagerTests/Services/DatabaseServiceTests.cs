using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaFileManager.Services;
using MediaFileManager.Models;
using System.Collections.Generic;
using System.IO;

namespace MediaFileManagerTests.Services
{
    /// <summary>
    /// DatabaseService 单元测试
    ///
    /// 测试SQLite数据库的CRUD操作
    /// 由于SQLite是嵌入式数据库，测试直接操作真实数据库文件
    /// 每个测试方法独立运行，TestInitialize确保数据库处于干净状态
    /// </summary>
    [TestClass]
    public class DatabaseServiceTests
    {
        private DatabaseService _service;

        [TestInitialize]
        public void Setup()
        {
            // 每次测试前重新初始化数据库
            _service = new DatabaseService();
            _service.InitializeDatabase();
        }

        /// <summary>
        /// 测试：初始化数据库不抛出异常
        /// 验证表创建语句正确执行
        /// </summary>
        [TestMethod]
        public void InitializeDatabase_DoesNotThrow()
        {
            // Act & Assert - 不应抛出异常
            _service.InitializeDatabase();
            // 如果到达这里，表示初始化成功
            Assert.IsTrue(true);
        }

        /// <summary>
        /// 测试：批量插入文件记录
        /// TDD验证：插入后能从数据库读取相同数量的记录
        /// </summary>
        [TestMethod]
        public void BatchInsertFiles_InsertsRecords()
        {
            // Arrange - 准备测试数据
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo
                {
                    FileName = "test.mp3",
                    FullPath = @"C:\Music\test.mp3",
                    FileSize = 1024000,
                    FileType = "Audio",
                    Extension = ".mp3",
                    LastModified = System.DateTime.Now
                },
                new MediaFileInfo
                {
                    FileName = "photo.jpg",
                    FullPath = @"C:\Photos\photo.jpg",
                    FileSize = 2048000,
                    FileType = "Image",
                    Extension = ".jpg",
                    LastModified = System.DateTime.Now
                }
            };

            // Act - 批量插入
            _service.BatchInsertFiles(files);

            // Assert - 验证插入结果
            var allFiles = _service.GetAllFiles();
            Assert.AreEqual(2, allFiles.Count,
                $"应插入2条记录，实际有{allFiles.Count}条");
        }

        /// <summary>
        /// 测试：INSERT OR REPLACE行为 - 重复路径应更新而非新增
        /// </summary>
        [TestMethod]
        public void BatchInsertFiles_DuplicatePath_ReplacesRecord()
        {
            // Arrange
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo
                {
                    FileName = "test.mp3",
                    FullPath = @"C:\Music\test.mp3",
                    FileSize = 1000,
                    FileType = "Audio",
                    Extension = ".mp3",
                    LastModified = System.DateTime.Now
                }
            };
            _service.BatchInsertFiles(files);

            // Act - 再次插入相同路径，但FileSize不同
            files[0].FileSize = 999999;
            _service.BatchInsertFiles(files);

            // Assert - 应只有1条记录，且FileSize为最新值
            var allFiles = _service.GetAllFiles();
            Assert.AreEqual(1, allFiles.Count);
            Assert.AreEqual(999999, allFiles[0].FileSize,
                "重复路径应更新为最新值");
        }

        /// <summary>
        /// 测试：按类型查询文件
        /// </summary>
        [TestMethod]
        public void GetFilesByType_ReturnsCorrectType()
        {
            // Arrange
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileType = "Audio", Extension = ".mp3", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "b.jpg", FullPath = @"C:\b.jpg", FileType = "Image", Extension = ".jpg", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "c.wav", FullPath = @"C:\c.wav", FileType = "Audio", Extension = ".wav", LastModified = System.DateTime.Now }
            };
            _service.BatchInsertFiles(files);

            // Act
            var audioFiles = _service.GetFilesByType("Audio");

            // Assert
            Assert.AreEqual(2, audioFiles.Count,
                $"应有2个Audio文件，实际有{audioFiles.Count}个");
            foreach (var f in audioFiles)
            {
                Assert.AreEqual("Audio", f.FileType,
                    $"所有结果都应为Audio类型，但发现{ f.FileType}");
            }
        }

        /// <summary>
        /// 测试：模糊搜索功能
        /// </summary>
        [TestMethod]
        public void SearchFiles_ReturnsMatchingFiles()
        {
            // Arrange
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo { FileName = "hello.txt", FullPath = @"C:\Docs\hello.txt", FileType = "Document", Extension = ".txt", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "world.txt", FullPath = @"C:\Docs\world.txt", FileType = "Document", Extension = ".txt", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "hello.mp3", FullPath = @"C:\Music\hello.mp3", FileType = "Audio", Extension = ".mp3", LastModified = System.DateTime.Now }
            };
            _service.BatchInsertFiles(files);

            // Act
            var results = _service.SearchFiles("hello");

            // Assert
            Assert.AreEqual(2, results.Count,
                $"搜索「hello」应返回2个结果，实际{results.Count}个");
        }

        /// <summary>
        /// 测试：搜索无匹配结果时返回空列表
        /// </summary>
        [TestMethod]
        public void SearchFiles_NoMatch_ReturnsEmptyList()
        {
            // Act
            var results = _service.SearchFiles("nonexistent");

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        /// <summary>
        /// 测试：获取文件类型统计数据
        /// </summary>
        [TestMethod]
        public void GetFileTypeStatistics_ReturnsCorrectGroups()
        {
            // Arrange
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSize = 1000, FileType = "Audio", Extension = ".mp3", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "b.jpg", FullPath = @"C:\b.jpg", FileSize = 2000, FileType = "Image", Extension = ".jpg", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "c.wav", FullPath = @"C:\c.wav", FileSize = 3000, FileType = "Audio", Extension = ".wav", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "d.png", FullPath = @"C:\d.png", FileSize = 4000, FileType = "Image", Extension = ".png", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "e.pdf", FullPath = @"C:\e.pdf", FileSize = 5000, FileType = "Document", Extension = ".pdf", LastModified = System.DateTime.Now }
            };
            _service.BatchInsertFiles(files);

            // Act
            var stats = _service.GetFileTypeStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(3, stats.Rows.Count,
                $"应有3个类型组，实际{stats.Rows.Count}组");
        }

        /// <summary>
        /// 测试：清空所有文件记录
        /// </summary>
        [TestMethod]
        public void ClearAllFiles_RemovesAllRecords()
        {
            // Arrange
            var files = new List<MediaFileInfo>
            {
                new MediaFileInfo { FileName = "test.txt", FullPath = @"C:\test.txt", FileType = "Document", Extension = ".txt", LastModified = System.DateTime.Now }
            };
            _service.BatchInsertFiles(files);

            // Act
            _service.ClearAllFiles();

            // Assert
            var allFiles = _service.GetAllFiles();
            Assert.AreEqual(0, allFiles.Count,
                "清空后应无记录");
        }
    }
}
