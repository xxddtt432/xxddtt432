using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaFileManager.Services;
using MediaFileManager.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaFileManagerTests.Services
{
    /// <summary>
    /// FileSyncService 单元测试
    ///
    /// TDD开发过程验证：
    /// 1. 先写测试定义同步行为的期望
    /// 2. 实现最小可工作的同步逻辑
    /// 3. 重构优化（如增量同步判断）
    ///
    /// 测试使用临时目录进行真实文件操作
    /// </summary>
    [TestClass]
    public class FileSyncServiceTests
    {
        private FileSyncService _service;
        private DatabaseService _dbService;
        private string _sourceDir;
        private string _destDir;

        [TestInitialize]
        public void Setup()
        {
            _dbService = new DatabaseService();
            _service = new FileSyncService(_dbService);

            // 创建临时源和目标目录
            _sourceDir = Path.Combine(Path.GetTempPath(), "SyncTest_Source_" + Path.GetRandomFileName());
            _destDir = Path.Combine(Path.GetTempPath(), "SyncTest_Dest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_destDir);

            // 在源目录创建测试文件
            File.WriteAllText(Path.Combine(_sourceDir, "file1.txt"), "Content 1");
            File.WriteAllText(Path.Combine(_sourceDir, "file2.txt"), "Content 2");

            // 创建子目录和文件
            string subDir = Path.Combine(_sourceDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "file3.txt"), "Content 3");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 清理测试目录
            try { if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, true); } catch { }
            try { if (Directory.Exists(_destDir)) Directory.Delete(_destDir, true); } catch { }
        }

        /// <summary>
        /// 测试：同步应复制所有源文件到目标目录
        /// TDD RED阶段：定义期望 - 同步后目标目录应有所有文件
        /// </summary>
        [TestMethod]
        public async Task SyncDirectoriesAsync_CopiesAllFiles()
        {
            // Arrange
            var progress = new Progress<SyncProgressInfo>();
            var cts = new CancellationTokenSource();

            // Act
            SyncJob result = await _service.SyncDirectoriesAsync(
                _sourceDir, _destDir, progress, cts.Token);

            // Assert
            Assert.AreEqual("Completed", result.Status,
                $"同步状态应为Completed，实际为{result.Status}");
            Assert.AreEqual(3, result.CopiedFiles,
                $"应复制3个文件，实际复制{result.CopiedFiles}个");
            Assert.AreEqual(0, result.FailedFiles);

            // 验证目标文件确实存在
            Assert.IsTrue(File.Exists(Path.Combine(_destDir, "file1.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(_destDir, "file2.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(_destDir, "sub", "file3.txt")));
        }

        /// <summary>
        /// 测试：增量同步 - 已存在且未修改的文件应被跳过
        /// TDD验证增量同步逻辑
        /// </summary>
        [TestMethod]
        public async Task SyncDirectoriesAsync_SkipsUnchangedFiles()
        {
            // Arrange - 第一次同步
            var progress = new Progress<SyncProgressInfo>();
            var cts = new CancellationTokenSource();
            await _service.SyncDirectoriesAsync(_sourceDir, _destDir, progress, cts.Token);

            // Act - 第二次同步（文件未变化）
            cts = new CancellationTokenSource();
            SyncJob result2 = await _service.SyncDirectoriesAsync(
                _sourceDir, _destDir, progress, cts.Token);

            // Assert - 所有文件应被跳过
            Assert.AreEqual(0, result2.CopiedFiles,
                $"文件未变化时应复制0个，实际复制{result2.CopiedFiles}个");
            Assert.AreEqual(3, result2.SkippedFiles,
                $"应跳过3个文件，实际跳过{result2.SkippedFiles}个");
        }

        /// <summary>
        /// 测试：源文件更新后，同步应重新复制
        /// 验证增量同步的更新检测逻辑
        /// </summary>
        [TestMethod]
        public async Task SyncDirectoriesAsync_CopiesUpdatedFiles()
        {
            // Arrange - 第一次同步
            var progress = new Progress<SyncProgressInfo>();
            var cts = new CancellationTokenSource();
            await _service.SyncDirectoriesAsync(_sourceDir, _destDir, progress, cts.Token);

            // 修改源文件
            System.Threading.Thread.Sleep(100); // 确保修改时间不同
            File.WriteAllText(Path.Combine(_sourceDir, "file1.txt"), "Updated content");

            // Act - 第二次同步
            cts = new CancellationTokenSource();
            SyncJob result2 = await _service.SyncDirectoriesAsync(
                _sourceDir, _destDir, progress, cts.Token);

            // Assert - file1.txt应被重新复制
            Assert.AreEqual(1, result2.CopiedFiles,
                $"更新1个文件，应复制1个，实际复制{result2.CopiedFiles}个");
            Assert.AreEqual(2, result2.SkippedFiles,
                $"未变化的2个文件应跳过，实际跳过{result2.SkippedFiles}个");

            // 验证目标文件内容已更新
            string destContent = File.ReadAllText(Path.Combine(_destDir, "file1.txt"));
            Assert.AreEqual("Updated content", destContent,
                "目标文件内容应已更新");
        }

        /// <summary>
        /// 测试：取消同步应抛出OperationCanceledException
        /// </summary>
        [TestMethod]
        public async Task SyncDirectoriesAsync_Cancelled_ThrowsException()
        {
            // Arrange - 在同步开始前就取消
            var progress = new Progress<SyncProgressInfo>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await _service.SyncDirectoriesAsync(_sourceDir, _destDir, progress, cts.Token);
            });
        }

        /// <summary>
        /// 测试：同步保持目录结构
        /// </summary>
        [TestMethod]
        public async Task SyncDirectoriesAsync_PreservesDirectoryStructure()
        {
            // Arrange
            var progress = new Progress<SyncProgressInfo>();
            var cts = new CancellationTokenSource();

            // Act
            await _service.SyncDirectoriesAsync(_sourceDir, _destDir, progress, cts.Token);

            // Assert - 子目录应在目标中存在
            string destSubDir = Path.Combine(_destDir, "sub");
            Assert.IsTrue(Directory.Exists(destSubDir),
                "目标应保留子目录结构");
            Assert.IsTrue(File.Exists(Path.Combine(destSubDir, "file3.txt")),
                "子目录中的文件应被复制");
        }
    }
}
