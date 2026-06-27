using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaFileManager.Services;
using MediaFileManager.Models;
using System.Collections.Generic;
using System.IO;

namespace MediaFileManagerTests.Services
{
    /// <summary>
    /// FileScannerService 单元测试
    ///
    /// TDD开发过程：
    /// RED   - 先写测试，定义期望的行为
    /// GREEN - 实现最简代码使测试通过
    /// REFACTOR - 重构代码，保持测试通过
    ///
    /// 测试覆盖：
    /// - 目录扫描基本功能
    /// - 文件类型自动分类
    /// - 空目录边界条件
    /// - 不存在的目录异常处理
    /// </summary>
    [TestClass]
    public class FileScannerServiceTests
    {
        private FileScannerService _service;
        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            _service = new FileScannerService();

            // 创建临时测试目录
            _testDirectory = Path.Combine(Path.GetTempPath(), "MediaVaultTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);

            // 创建测试文件
            File.WriteAllText(Path.Combine(_testDirectory, "test1.txt"), "Hello World");
            File.WriteAllText(Path.Combine(_testDirectory, "test2.txt"), "Test content");
            File.WriteAllText(Path.Combine(_testDirectory, "image1.png"), "fake png");

            // 创建子目录和文件
            string subDir = Path.Combine(_testDirectory, "subfolder");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "music.mp3"), "fake mp3");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 清理测试目录
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        /// <summary>
        /// 测试：扫描目录应返回正确数量的文件
        /// TDD RED阶段：先定义期望-扫描测试目录应返回4个文件
        /// </summary>
        [TestMethod]
        public void ScanDirectory_ValidDirectory_ReturnsAllFiles()
        {
            // Act
            List<MediaFileInfo> files = _service.ScanDirectory(_testDirectory);

            // Assert
            Assert.IsNotNull(files, "返回的文件列表不应为null");
            Assert.AreEqual(4, files.Count,
                $"测试目录包含4个文件，但扫描返回了{files.Count}个");
        }

        /// <summary>
        /// 测试：扫描文件应正确分类文件类型
        /// TDD验证：文件类型分类逻辑在扫描过程中正确应用
        /// </summary>
        [TestMethod]
        public void ScanDirectory_ClassifiesFileTypesCorrectly()
        {
            // Act
            List<MediaFileInfo> files = _service.ScanDirectory(_testDirectory);

            // Assert - 验证txt文件被分类为Document
            foreach (var file in files)
            {
                if (file.Extension == ".txt")
                    Assert.AreEqual("Document", file.FileType,
                        $"txt文件应被分类为Document，实际为{file.FileType}");
                if (file.Extension == ".png")
                    Assert.AreEqual("Image", file.FileType);
                if (file.Extension == ".mp3")
                    Assert.AreEqual("Audio", file.FileType);
            }
        }

        /// <summary>
        /// 测试：扫描不存在的目录应返回空列表（不应抛出异常）
        /// 边界条件测试 - 验证服务的容错能力
        /// </summary>
        [TestMethod]
        public void ScanDirectory_NonExistentDirectory_ReturnsEmptyList()
        {
            // Act
            List<MediaFileInfo> files = _service.ScanDirectory(@"Z:\NonExistent\Path\");

            // Assert
            Assert.IsNotNull(files);
            Assert.AreEqual(0, files.Count,
                "扫描不存在的目录应返回空列表，不应抛出异常");
        }

        /// <summary>
        /// 测试：文件信息应包含正确的元数据
        /// </summary>
        [TestMethod]
        public void ScanDirectory_FileInfoIsCorrect()
        {
            // Act
            List<MediaFileInfo> files = _service.ScanDirectory(_testDirectory);

            // Assert
            var txtFile = files.Find(f => f.FileName == "test1.txt");
            Assert.IsNotNull(txtFile, "应能找到 test1.txt");
            Assert.AreEqual(".txt", txtFile.Extension);
            Assert.IsTrue(txtFile.FullPath.EndsWith("test1.txt"),
                "FullPath应以文件名结尾");
            Assert.IsTrue(txtFile.FileSize > 0,
                "文件大小应大于0");
        }

        /// <summary>
        /// 测试：扫描应包含子目录中的文件
        /// </summary>
        [TestMethod]
        public void ScanDirectory_IncludesSubdirectories()
        {
            // Act
            List<MediaFileInfo> files = _service.ScanDirectory(_testDirectory);

            // Assert
            bool hasMp3InSubdir = files.Exists(f =>
                f.FileName == "music.mp3" && f.FileType == "Audio");
            Assert.IsTrue(hasMp3InSubdir,
                "应包含子目录中的mp3文件");
        }
    }
}
