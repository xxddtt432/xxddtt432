using System;
using System.Windows.Forms;

namespace MediaFileManager
{
    /// <summary>
    /// 应用程序入口类
    /// 负责初始化应用程序、配置异常处理和启动主窗体
    /// </summary>
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 启用视觉样式，使应用程序具有Windows原生外观
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 注册全局异常处理，确保未捕获的异常有友好的提示
            Application.ThreadException += (sender, e) =>
            {
                MessageBox.Show(
                    $"发生未处理的异常：\n{e.Exception.Message}\n\n堆栈跟踪：\n{e.Exception.StackTrace}",
                    "程序错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            // 处理UI线程之外的未捕获异常
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"发生严重错误：\n{ex?.Message}",
                    "严重错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            // 启动主窗体
            Application.Run(new MainForm());
        }
    }
}
