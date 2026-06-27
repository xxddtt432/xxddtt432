using System;

namespace MediaFileManager.Models
{
    /// <summary>
    /// 媒体文件信息实体类
    /// 封装文件的元数据信息，包括名称、路径、大小、类型、修改日期等属性
    /// 使用属性(get/set)实现封装，提供ToString()方法用于显示
    /// </summary>
    public class MediaFileInfo
    {
        /// <summary>文件唯一标识（数据库自增ID）</summary>
        public int Id { get; set; }

        /// <summary>文件名称（含扩展名）</summary>
        public string FileName { get; set; }

        /// <summary>文件完整路径</summary>
        public string FullPath { get; set; }

        /// <summary>文件大小（字节）</summary>
        public long FileSize { get; set; }

        /// <summary>文件类型分类：Image/Audio/Video/Document/Other</summary>
        public string FileType { get; set; }

        /// <summary>文件扩展名（如 .mp3, .jpg）</summary>
        public string Extension { get; set; }

        /// <summary>文件创建时间</summary>
        public DateTime CreationTime { get; set; }

        /// <summary>文件最后修改时间</summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// 获取格式化的文件大小字符串（如 "1.5 MB"）
        /// 使用多级条件判断递归转换单位
        /// </summary>
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024)
                    return $"{FileSize} B";
                else if (FileSize < 1024 * 1024)
                    return $"{FileSize / 1024.0:F1} KB";
                else if (FileSize < 1024 * 1024 * 1024)
                    return $"{FileSize / (1024.0 * 1024.0):F2} MB";
                else
                    return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        /// <summary>
        /// 根据扩展名自动判断文件类型分类
        /// 使用switch表达式进行多分支匹配（C# 7.0+模式匹配）
        /// </summary>
        public static string ClassifyFileType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "Other";

            string ext = extension.ToLower();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".ico":
                case ".webp":
                    return "Image";
                case ".mp3":
                case ".wav":
                case ".wma":
                case ".flac":
                case ".aac":
                case ".ogg":
                case ".m4a":
                    return "Audio";
                case ".mp4":
                case ".avi":
                case ".wmv":
                case ".mkv":
                case ".mov":
                case ".flv":
                case ".webm":
                    return "Video";
                case ".doc":
                case ".docx":
                case ".pdf":
                case ".txt":
                case ".xls":
                case ".xlsx":
                case ".ppt":
                case ".pptx":
                case ".csv":
                case ".md":
                    return "Document";
                default:
                    return "Other";
            }
        }

        /// <summary>
        /// 重写ToString方法，用于在ListView等控件中显示
        /// </summary>
        public override string ToString()
        {
            return $"{FileName} | {FileSizeFormatted} | {FileType} | {LastModified:yyyy-MM-dd HH:mm}";
        }

        /// <summary>
        /// 重写Equals方法，基于完整路径判断文件是否相同
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is MediaFileInfo other)
                return string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        /// <summary>
        /// 重写GetHashCode方法，与Equals保持一致
        /// </summary>
        public override int GetHashCode()
        {
            return FullPath?.ToLower().GetHashCode() ?? 0;
        }
    }
}
