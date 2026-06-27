using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaFileManager.Models;

namespace MediaFileManagerTests.Models
{
    /// <summary>
    /// MediaFileInfo 模型类的单元测试
    /// 遵循 TDD（测试驱动开发）的 Red-Green-Refactor 循环
    ///
    /// 测试覆盖：
    /// - 文件类型分类逻辑（ClassifyFileType）
    /// - 文件大小格式化（FileSizeFormatted）
    /// - Equals/GetHashCode 一致性
    /// </summary>
    [TestClass]
    public class MediaFileInfoTests
    {
        /// <summary>
        /// 测试：图片文件类型分类
        /// 验证常见图片扩展名被正确分类为"Image"
        /// </summary>
        [TestMethod]
        [DataRow(".jpg")]
        [DataRow(".jpeg")]
        [DataRow(".png")]
        [DataRow(".gif")]
        [DataRow(".bmp")]
        public void ClassifyFileType_ImageExtensions_ReturnsImage(string extension)
        {
            // Act - 执行分类
            string result = MediaFileInfo.ClassifyFileType(extension);

            // Assert - 断言结果
            Assert.AreEqual("Image", result,
                $"扩展名 {extension} 应被分类为 Image");
        }

        /// <summary>
        /// 测试：音频文件类型分类
        /// </summary>
        [TestMethod]
        [DataRow(".mp3")]
        [DataRow(".wav")]
        [DataRow(".flac")]
        [DataRow(".aac")]
        public void ClassifyFileType_AudioExtensions_ReturnsAudio(string extension)
        {
            string result = MediaFileInfo.ClassifyFileType(extension);
            Assert.AreEqual("Audio", result);
        }

        /// <summary>
        /// 测试：视频文件类型分类
        /// </summary>
        [TestMethod]
        [DataRow(".mp4")]
        [DataRow(".avi")]
        [DataRow(".mkv")]
        public void ClassifyFileType_VideoExtensions_ReturnsVideo(string extension)
        {
            string result = MediaFileInfo.ClassifyFileType(extension);
            Assert.AreEqual("Video", result);
        }

        /// <summary>
        /// 测试：文档文件类型分类
        /// </summary>
        [TestMethod]
        [DataRow(".pdf")]
        [DataRow(".docx")]
        [DataRow(".txt")]
        public void ClassifyFileType_DocumentExtensions_ReturnsDocument(string extension)
        {
            string result = MediaFileInfo.ClassifyFileType(extension);
            Assert.AreEqual("Document", result);
        }

        /// <summary>
        /// 测试：未知扩展名应返回"Other"
        /// </summary>
        [TestMethod]
        public void ClassifyFileType_UnknownExtension_ReturnsOther()
        {
            string result = MediaFileInfo.ClassifyFileType(".xyz123");
            Assert.AreEqual("Other", result);
        }

        /// <summary>
        /// 测试：空扩展名/null应返回"Other"
        /// 边界条件测试
        /// </summary>
        [TestMethod]
        public void ClassifyFileType_NullOrEmpty_ReturnsOther()
        {
            Assert.AreEqual("Other", MediaFileInfo.ClassifyFileType(null));
            Assert.AreEqual("Other", MediaFileInfo.ClassifyFileType(""));
            Assert.AreEqual("Other", MediaFileInfo.ClassifyFileType("   "));
        }

        /// <summary>
        /// 测试：文件大小格式化 - B（字节）单位
        /// </summary>
        [TestMethod]
        public void FileSizeFormatted_LessThan1KB_ReturnsBytesUnit()
        {
            var file = new MediaFileInfo { FileSize = 512 };
            string result = file.FileSizeFormatted;
            StringAssert.Contains(result, "B");
        }

        /// <summary>
        /// 测试：文件大小格式化 - MB单位
        /// </summary>
        [TestMethod]
        public void FileSizeFormatted_MBSize_ReturnsMBUnit()
        {
            var file = new MediaFileInfo { FileSize = 5 * 1024 * 1024 };
            string result = file.FileSizeFormatted;
            Assert.AreEqual("5.00 MB", result);
        }

        /// <summary>
        /// 测试：文件大小格式化 - GB单位
        /// </summary>
        [TestMethod]
        public void FileSizeFormatted_GBSize_ReturnsGBUnit()
        {
            var file = new MediaFileInfo { FileSize = 3L * 1024 * 1024 * 1024 };
            string result = file.FileSizeFormatted;
            Assert.AreEqual("3.00 GB", result);
        }

        /// <summary>
        /// 测试：Equals方法 - 相同路径应相等
        /// </summary>
        [TestMethod]
        public void Equals_SamePath_ReturnsTrue()
        {
            var file1 = new MediaFileInfo { FullPath = @"C:\Test\file.txt" };
            var file2 = new MediaFileInfo { FullPath = @"C:\Test\file.txt" };

            Assert.IsTrue(file1.Equals(file2));
            Assert.AreEqual(file1.GetHashCode(), file2.GetHashCode());
        }

        /// <summary>
        /// 测试：Equals方法 - 不同路径应不相等
        /// </summary>
        [TestMethod]
        public void Equals_DifferentPath_ReturnsFalse()
        {
            var file1 = new MediaFileInfo { FullPath = @"C:\Test\file1.txt" };
            var file2 = new MediaFileInfo { FullPath = @"C:\Test\file2.txt" };

            Assert.IsFalse(file1.Equals(file2));
        }
    }
}
