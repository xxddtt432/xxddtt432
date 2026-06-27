using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MediaFileManager.Controls
{
    /// <summary>
    /// 自定义统计图表控件
    /// 使用GDI+（System.Drawing）绘制饼图、柱状图等统计图表
    ///
    /// 技术要点：
    /// - GDI+绘图：Graphics对象、Pen（画笔）、Brush（画刷）
    /// - GraphicsPath：创建复杂的图形路径
    /// - LinearGradientBrush：渐变填充效果
    /// - SmoothingMode：抗锯齿渲染
    /// - 自定义控件：继承UserControl，重写OnPaint方法
    /// - 数学计算：使用三角函数计算饼图扇形角度
    /// </summary>
    public class ChartControl : UserControl
    {
        private DataTable _dataSource;
        private ChartType _chartType = ChartType.Pie;
        private string _title = "统计图表";
        private readonly Color[] _chartColors = new Color[]
        {
            Color.FromArgb(65, 140, 240),   // 蓝色
            Color.FromArgb(252, 133, 96),   // 橙色
            Color.FromArgb(89, 194, 121),   // 绿色
            Color.FromArgb(244, 208, 64),   // 黄色
            Color.FromArgb(199, 106, 212),  // 紫色
            Color.FromArgb(72, 207, 208),   // 青色
            Color.FromArgb(237, 101, 121),  // 粉色
            Color.FromArgb(149, 152, 159),  // 灰色
        };

        public ChartControl()
        {
            // 设置控件样式：双缓冲减少闪烁
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.White;
            this.MinimumSize = new Size(300, 250);
        }

        /// <summary>
        /// 设置图表数据源
        /// 期望DataTable包含：Label列（分类标签）、Value列（数值）
        /// </summary>
        public void SetDataSource(DataTable dataSource, string labelColumn, string valueColumn)
        {
            _dataSource = dataSource;
            Invalidate(); // 触发重绘
        }

        /// <summary>
        /// 设置图表类型
        /// </summary>
        public ChartType ChartType
        {
            get => _chartType;
            set { _chartType = value; Invalidate(); }
        }

        /// <summary>
        /// 设置图表标题
        /// </summary>
        public string ChartTitle
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        /// <summary>
        /// 重写OnPaint方法：核心绘图逻辑
        /// 根据ChartType分发到不同的绘制方法
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            // 启用抗锯齿，使图形边缘平滑
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 清除背景
            g.Clear(BackColor);

            // 根据图表类型调用不同的绘制方法
            switch (_chartType)
            {
                case ChartType.Pie:
                    DrawPieChart(g);
                    break;
                case ChartType.Bar:
                    DrawBarChart(g);
                    break;
                case ChartType.Both:
                    DrawPieChart(g);
                    break;
            }
        }

        /// <summary>
        /// 绘制饼图（Pie Chart）
        ///
        /// 实现原理：
        /// 1. 计算每个扇形的起始角度和扫描角度
        /// 2. 使用Graphics.FillPie填充扇形区域
        /// 3. 使用Graphics.DrawPie绘制扇形边框
        /// 4. 绘制图例说明
        /// </summary>
        private void DrawPieChart(Graphics g)
        {
            if (_dataSource == null || _dataSource.Rows.Count == 0)
            {
                DrawPlaceholder(g, "暂无数据，请先扫描文件");
                return;
            }

            // 计算绘图区域
            int padding = 40;
            int legendWidth = 160;
            int chartSize = Math.Min(ClientSize.Width - legendWidth - padding * 2, ClientSize.Height - padding * 2);
            int chartX = padding;
            int chartY = (ClientSize.Height - chartSize) / 2 + 20;

            Rectangle chartRect = new Rectangle(chartX, chartY, chartSize, chartSize);

            // 计算总值
            double total = 0;
            foreach (DataRow row in _dataSource.Rows)
            {
                total += Convert.ToDouble(row[1]); // 假设第二列是数值
            }

            if (total <= 0) return;

            // 绘制饼图扇区
            float startAngle = 0f;
            int colorIndex = 0;

            // 存储扇区信息用于后续绘制图例
            var slices = new List<(string label, double value, float sweepAngle, Color color)>();

            foreach (DataRow row in _dataSource.Rows)
            {
                double value = Convert.ToDouble(row[1]);
                float sweepAngle = (float)(value / total * 360);

                Color color = _chartColors[colorIndex % _chartColors.Length];
                slices.Add((row[0].ToString(), value, sweepAngle, color));

                // 使用SolidBrush填充扇形区域
                using (Brush brush = new SolidBrush(color))
                {
                    g.FillPie(brush, chartRect, startAngle, sweepAngle);
                }

                // 使用白色画笔绘制扇形边框
                using (Pen pen = new Pen(Color.White, 2))
                {
                    g.DrawPie(pen, chartRect, startAngle, sweepAngle);
                }

                startAngle += sweepAngle;
                colorIndex++;
            }

            // 绘制百分比标签（在饼图中心）
            DrawPieLabels(g, chartRect, slices, total);

            // 绘制图例
            int legendX = chartRect.Right + 30;
            int legendY = chartY + 10;
            DrawLegend(g, slices, legendX, legendY);

            // 绘制标题
            using (Font titleFont = new Font("微软雅黑", 14, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                SizeF titleSize = g.MeasureString(_title, titleFont);
                g.DrawString(_title, titleFont, titleBrush,
                    (ClientSize.Width - titleSize.Width) / 2, 8);
            }
        }

        /// <summary>
        /// 绘制饼图百分比标签
        /// 在扇形中间显示百分比文字
        /// </summary>
        private void DrawPieLabels(Graphics g, Rectangle chartRect,
            List<(string label, double value, float sweepAngle, Color color)> slices, double total)
        {
            if (slices.Count > 8) return; // 扇区太多时不显示标签，避免重叠

            float startAngle = 0f;
            foreach (var slice in slices)
            {
                double percentage = slice.value / total * 100;
                if (percentage < 5) { startAngle += slice.sweepAngle; continue; } // 太小的扇区不显示

                // 计算扇形中心角度（弧度）
                float midAngle = (startAngle + slice.sweepAngle / 2f) * (float)Math.PI / 180f;

                // 计算标签位置（扇形中心向外偏移）
                float radius = chartRect.Width / 2f * 0.7f;
                float cx = chartRect.X + chartRect.Width / 2f;
                float cy = chartRect.Y + chartRect.Height / 2f;
                float labelX = cx + (float)Math.Cos(midAngle) * radius - 15;
                float labelY = cy + (float)Math.Sin(midAngle) * radius - 10;

                using (Font font = new Font("微软雅黑", 8, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.White))
                {
                    g.DrawString($"{percentage:F1}%", font, brush, labelX, labelY);
                }

                startAngle += slice.sweepAngle;
            }
        }

        /// <summary>
        /// 绘制柱状图（Bar Chart）
        ///
        /// 使用LinearGradientBrush实现渐变填充的柱状图
        /// 每个柱子使用渐变画刷从上到下渲染
        /// </summary>
        private void DrawBarChart(Graphics g)
        {
            if (_dataSource == null || _dataSource.Rows.Count == 0)
            {
                DrawPlaceholder(g, "暂无数据，请先扫描文件");
                return;
            }

            int padding = 50;
            int bottomMargin = 60;
            int chartLeft = padding + 30;
            int chartBottom = ClientSize.Height - bottomMargin;
            int chartTop = padding + 30;
            int chartWidth = ClientSize.Width - chartLeft - padding;
            int chartHeight = chartBottom - chartTop;

            // 找到最大值用于缩放
            double maxValue = 0;
            foreach (DataRow row in _dataSource.Rows)
            {
                double val = Convert.ToDouble(row[1]);
                if (val > maxValue) maxValue = val;
            }
            if (maxValue == 0) maxValue = 1;

            int barCount = _dataSource.Rows.Count;
            int barSpacing = 20;
            int barWidth = Math.Max(20, (chartWidth - barSpacing * (barCount + 1)) / barCount);

            // 绘制坐标轴
            using (Pen axisPen = new Pen(Color.FromArgb(180, 180, 180), 1))
            {
                // Y轴
                g.DrawLine(axisPen, chartLeft, chartTop, chartLeft, chartBottom);
                // X轴
                g.DrawLine(axisPen, chartLeft, chartBottom, chartLeft + chartWidth, chartBottom);
            }

            // 绘制Y轴刻度线和标签
            using (Font labelFont = new Font("微软雅黑", 7))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                int yTicks = 5;
                for (int i = 0; i <= yTicks; i++)
                {
                    int y = chartBottom - (int)((double)i / yTicks * chartHeight);
                    g.DrawLine(Pens.LightGray, chartLeft - 3, y, chartLeft, y);
                    double tickValue = maxValue * i / yTicks;
                    string label = tickValue >= 1000 ? $"{tickValue / 1000:F0}K" : $"{tickValue:F0}";
                    g.DrawString(label, labelFont, labelBrush, chartLeft - 35, y - 7);
                }
            }

            // 绘制柱子
            int colorIndex = 0;
            for (int i = 0; i < barCount; i++)
            {
                double value = Convert.ToDouble(_dataSource.Rows[i][1]);
                int barHeight = (int)(value / maxValue * chartHeight);
                int barX = chartLeft + barSpacing + i * (barWidth + barSpacing);
                int barY = chartBottom - barHeight;

                Color color = _chartColors[colorIndex % _chartColors.Length];

                // 使用LinearGradientBrush实现渐变填充
                // 从顶部较亮到底部较暗的垂直渐变
                Rectangle barRect = new Rectangle(barX, barY, barWidth, barHeight);
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    barRect,
                    ControlPaint.Light(color, 0.3f),  // 顶部：亮色
                    color,                              // 底部：原色
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, barRect);
                }

                // 绘制柱子边框
                using (Pen pen = new Pen(ControlPaint.Dark(color, 0.2f), 1))
                {
                    g.DrawRectangle(pen, barRect);
                }

                // 绘制数值标签
                using (Font valueFont = new Font("微软雅黑", 8, FontStyle.Bold))
                using (Brush valueBrush = new SolidBrush(Color.FromArgb(60, 60, 60)))
                {
                    string valueText = value >= 1000 ? $"{value / 1000:F1}K" : $"{value:F0}";
                    SizeF textSize = g.MeasureString(valueText, valueFont);
                    g.DrawString(valueText, valueFont, valueBrush,
                        barX + (barWidth - textSize.Width) / 2,
                        barY - textSize.Height - 4);
                }

                // 绘制分类标签
                using (Font catFont = new Font("微软雅黑", 8))
                using (Brush catBrush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                {
                    string catLabel = _dataSource.Rows[i][0].ToString();
                    SizeF labelSize = g.MeasureString(catLabel, catFont);
                    g.DrawString(catLabel, catFont, catBrush,
                        barX + (barWidth - labelSize.Width) / 2,
                        chartBottom + 8);
                }

                colorIndex++;
            }

            // 绘制标题
            using (Font titleFont = new Font("微软雅黑", 14, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                SizeF titleSize = g.MeasureString(_title, titleFont);
                g.DrawString(_title, titleFont, titleBrush,
                    (ClientSize.Width - titleSize.Width) / 2, 5);
            }
        }

        /// <summary>
        /// 绘制图例
        /// 使用彩色方块 + 文字标识每个类别
        /// </summary>
        private void DrawLegend(Graphics g,
            List<(string label, double value, float sweepAngle, Color color)> slices,
            int x, int y)
        {
            int itemHeight = 22;

            using (Font font = new Font("微软雅黑", 9))
            {
                for (int i = 0; i < slices.Count; i++)
                {
                    var slice = slices[i];
                    int itemY = y + i * itemHeight;

                    // 绘制颜色方块
                    using (Brush brush = new SolidBrush(slice.color))
                    {
                        g.FillRectangle(brush, x, itemY, 14, 14);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(200, 200, 200)))
                    {
                        g.DrawRectangle(pen, x, itemY, 14, 14);
                    }

                    // 绘制标签和百分比
                    double percentage = slice.value / slices.Sum(s => s.value) * 100;
                    string legendText = $"{slice.label} ({percentage:F1}%)";
                    using (Brush textBrush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    {
                        g.DrawString(legendText, font, textBrush, x + 20, itemY - 1);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制占位符（无数据时的提示）
        /// </summary>
        private void DrawPlaceholder(Graphics g, string message)
        {
            using (Font font = new Font("微软雅黑", 12))
            using (Brush brush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                SizeF textSize = g.MeasureString(message, font);
                g.DrawString(message, font, brush,
                    (ClientSize.Width - textSize.Width) / 2,
                    (ClientSize.Height - textSize.Height) / 2);
            }
        }
    }

    /// <summary>
    /// 图表类型枚举
    /// </summary>
    public enum ChartType
    {
        Pie,
        Bar,
        Both
    }
}
