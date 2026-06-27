using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaFileManager.Services;
using MediaFileManager.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace MediaFileManagerTests.Services
{
    /// <summary>
    /// StatisticsService 单元测试
    ///
    /// 测试统计计算逻辑的正确性
    /// 使用模拟数据验证各种统计方法的输出
    /// </summary>
    [TestClass]
    public class StatisticsServiceTests
    {
        private StatisticsService _service;
        private List<MediaFileInfo> _testFiles;

        [TestInitialize]
        public void Setup()
        {
            _service = new StatisticsService();

            // 准备测试数据：模拟不同类型的文件
            _testFiles = new List<MediaFileInfo>
            {
                new MediaFileInfo { FileName = "img1.jpg", FullPath = @"C:\Photos\img1.jpg", FileSize = 1000000, FileType = "Image", Extension = ".jpg", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "img2.png", FullPath = @"C:\Photos\img2.png", FileSize = 2000000, FileType = "Image", Extension = ".png", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "song.mp3", FullPath = @"C:\Music\song.mp3", FileSize = 5000000, FileType = "Audio", Extension = ".mp3", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "doc.pdf", FullPath = @"C:\Docs\doc.pdf", FileSize = 500000, FileType = "Document", Extension = ".pdf", LastModified = System.DateTime.Now },
                new MediaFileInfo { FileName = "video.mp4", FullPath = @"C:\Videos\video.mp4", FileSize = 50000000, FileType = "Video", Extension = ".mp4", LastModified = System.DateTime.Now }
            };
        }

        /// <summary>
        /// 测试：文件类型分布统计
        /// </summary>
        [TestMethod]
        public void CalculateFileTypeDistribution_ReturnsCorrectCounts()
        {
            // Act
            DataTable result = _service.CalculateFileTypeDistribution(_testFiles);

            // Assert
            Assert.AreEqual(4, result.Rows.Count,
                $"测试数据有4种类型，统计应有4行，实际{result.Rows.Count}行");

            // 验证每种类型的计数
            var imageRow = result.Select("FileType = 'Image'");
            Assert.AreEqual(1, imageRow.Length);
            Assert.AreEqual(2, imageRow[0]["Count"]);
            Assert.AreEqual(3000000L, imageRow[0]["TotalSize"]);

            var audioRow = result.Select("FileType = 'Audio'");
            Assert.AreEqual(1, audioRow[0]["Count"]);
        }

        /// <summary>
        /// 测试：空列表统计返回空DataTable
        /// </summary>
        [TestMethod]
        public void CalculateFileTypeDistribution_EmptyList_ReturnsEmptyTable()
        {
            // Act
            DataTable result = _service.CalculateFileTypeDistribution(new List<MediaFileInfo>());

            // Assert
            Assert.AreEqual(0, result.Rows.Count);
        }

        /// <summary>
        /// 测试：null列表统计返回空DataTable（容错）
        /// </summary>
        [TestMethod]
        public void CalculateFileTypeDistribution_NullList_ReturnsEmptyTable()
        {
            // Act
            DataTable result = _service.CalculateFileTypeDistribution(null);

            // Assert
            Assert.AreEqual(0, result.Rows.Count,
                "null输入应返回空表，不应抛出异常");
        }

        /// <summary>
        /// 测试：统计摘要计算
        /// </summary>
        [TestMethod]
        public void CalculateSummary_ReturnsCorrectTotals()
        {
            // Act
            var summary = _service.CalculateSummary(_testFiles);

            // Assert
            Assert.AreEqual(5, summary.TotalFiles);
            Assert.AreEqual(58500000L, summary.TotalSize,
                $"总大小应为58500000字节，实际{summary.TotalSize}");
            Assert.AreEqual(2, summary.ImageCount);
            Assert.AreEqual(1, summary.AudioCount);
            Assert.AreEqual(1, summary.VideoCount);
            Assert.AreEqual(1, summary.DocumentCount);
        }

        /// <summary>
        /// 测试：最大/最新/最旧文件识别
        /// </summary>
        [TestMethod]
        public void CalculateSummary_IdentifiesExtremes()
        {
            // Act
            var summary = _service.CalculateSummary(_testFiles);

            // Assert
            Assert.IsNotNull(summary.LargestFile,
                "应有最大文件");
            Assert.AreEqual("video.mp4", summary.LargestFile.FileName,
                "video.mp4 (50MB) 应是最大文件");
            Assert.IsTrue(summary.AverageFileSize > 0,
                "平均大小应大于0");
        }

        /// <summary>
        /// 测试：百分比之和应接近100%
        /// </summary>
        [TestMethod]
        public void CalculateFileTypeDistribution_PercentagesSumTo100()
        {
            // Act
            DataTable result = _service.CalculateFileTypeDistribution(_testFiles);
            double totalPercentage = 0;
            foreach (DataRow row in result.Rows)
            {
                totalPercentage += (double)row["Percentage"];
            }

            // Assert - 允许0.5%的浮点误差
            Assert.AreEqual(100.0, totalPercentage, 0.5,
                $"百分比之和应为100%，实际{totalPercentage:F2}%");
        }
    }
}
