using e_learning_app;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app.Views
{
    public partial class ReportsView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        // -- Models ---------------------------------------------------
        public class ClassReport
        {
            public string Subject { get; set; }
            public string ClassName { get; set; }
            public string AccentColor { get; set; }
            public int StudentCount { get; set; }
            public double AvgScore { get; set; }
            public double PassRate { get; set; }
            public double ExcelRate { get; set; }
        }

        public class TopStudent { public string Medal, Name, ClassName; public double Avg; }

        public class ScoreBand { public string Range, Color; public int Count; public double Pct; }

        // -- Data -----------------------------------------------------
        private List<ClassReport> _reports = new();
        private List<TopStudent> _top = new();
        private List<ScoreBand> _distribution = new();
        private string _semester = "Học kỳ 2 – 2024";

        // -- Constructor ----------------------------------------------
        public ReportsView(DatabaseManager dbManager) { InitializeComponent(); LoadData(); _dbManager = dbManager; }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => Refresh();

        // -- Data -----------------------------------------------------
        private void LoadData()
        {
            _reports = new()
            {
                new() { Subject="Toán Giải Tích",   ClassName="12A1", AccentColor="#3B82F6", StudentCount=38, AvgScore=7.9, PassRate=89, ExcelRate=24 },
                new() { Subject="Vật Lý Đại Cuong", ClassName="11B3", AccentColor="#8B5CF6", StudentCount=42, AvgScore=7.4, PassRate=83, ExcelRate=19 },
                new() { Subject="Hóa Hữu Co",       ClassName="12C2", AccentColor="#F59E0B", StudentCount=35, AvgScore=8.1, PassRate=91, ExcelRate=28 },
                new() { Subject="Sinh Học Phân Tử",  ClassName="11A2", AccentColor="#10B981", StudentCount=40, AvgScore=7.6, PassRate=85, ExcelRate=20 },
            };
            _top = new()
            {
                new() { Medal="🥇", Name="Nguyễn Minh Anh",  ClassName="12A1", Avg=9.7 },
                new() { Medal="🥈", Name="Trần Bảo Châu",    ClassName="12C2", Avg=9.4 },
                new() { Medal="🥉", Name="Lê Quốc Hùng",     ClassName="11B3", Avg=9.2 },
            };
            _distribution = new()
            {
                new() { Range="9–10",  Color="#22C55E", Count=55,  Pct=22 },
                new() { Range="7–8.9", Color="#3B82F6", Count=89,  Pct=36 },
                new() { Range="5–6.9", Color="#F59E0B", Count=69,  Pct=28 },
                new() { Range="3–4.9", Color="#FB923C", Count=22,  Pct=9  },
                new() { Range="0–2.9", Color="#EF4444", Count=12,  Pct=5  },
            };
        }

        // -- Refresh --------------------------------------------------
        private void Refresh()
        {
            int total = _reports.Sum(r => r.StudentCount);
            if (total == 0) return;

            double avg = _reports.Average(r => r.AvgScore);
            double pass = _reports.Average(r => r.PassRate);
            double excel = _reports.Average(r => r.ExcelRate);
            int passing = (int)(total * pass / 100);
            int weak = _distribution.Where(d => d.Range is "3–4.9" or "0–2.9").Sum(d => d.Count);

            // Summary cards
            TxtOverallAvg.Text = avg.ToString("0.00");
            TxtAvgNote.Text = avg >= 7.5 ? "Trên mức kỳ vọng (7.5) ✓" : "Dưới mức kỳ vọng (7.5)";
            TxtAvgNote.Foreground = avg >= 7.5
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

            TxtPassRate.Text = $"{pass:0.0}%";
            TxtPassNote.Text = $"{passing} / {total} học sinh đạt";

            TxtExcelRate.Text = $"{excel:0.0}%";
            TxtExcelNote.Text = $"{_distribution.FirstOrDefault(d => d.Range == "9–10")?.Count ?? 0} học sinh diểm = 9.0";

            double weakPct = _distribution.Where(d => d.Range is "3–4.9" or "0–2.9").Sum(d => d.Pct);
            TxtWeakRate.Text = $"{weakPct:0.0}%";
            TxtWeakNote.Text = $"{weak} học sinh cần hỗ trợ";

            // Table rows
            TableRowsPanel.Children.Clear();
            foreach (var r in _reports)
            {
                var sep = new Separator { Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)) };
                TableRowsPanel.Children.Add(sep);
                TableRowsPanel.Children.Add(BuildTableRow(r));
            }

            // Distribution bars
            DistributionPanel.Children.Clear();
            double maxPct = _distribution.Max(d => d.Pct);
            foreach (var b in _distribution)
                DistributionPanel.Children.Add(BuildDistRow(b, maxPct));

            // Top students
            TopStudentsPanel.Children.Clear();
            foreach (var s in _top)
                TopStudentsPanel.Children.Add(BuildTopStudentRow(s));
        }

        // -- Row builders ---------------------------------------------
        private UIElement BuildTableRow(ClassReport r)
        {
            var grid = new Grid { Height = 46 };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(i == 3 ? 80 : 70) });

            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = (Brush)new BrushConverter().ConvertFromString(r.AccentColor)!,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var nameWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            nameWrap.Children.Add(dot);
            nameWrap.Children.Add(new TextBlock { Text = $"{r.Subject} – {r.ClassName}", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) });
            Grid.SetColumn(nameWrap, 0);

            TextBlock Num(int col, string text, Brush fg)
            {
                var tb = new TextBlock { Text = text, FontSize = 13, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(tb, col); return tb;
            }
            var btnDetail = new Button
            {
                Content = "↗",
                Tag = r.ClassName,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14
            };
            btnDetail.Click += BtnViewDetail_Click;
            Grid.SetColumn(btnDetail, 4);

            grid.Children.Add(nameWrap);
            grid.Children.Add(Num(1, r.StudentCount.ToString(), new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))));
            grid.Children.Add(Num(2, r.AvgScore.ToString("0.0"), new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))));
            grid.Children.Add(Num(3, $"{r.PassRate:0}%", new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))));
            grid.Children.Add(btnDetail);
            return grid;
        }

        private UIElement BuildDistRow(ScoreBand b, double maxPct)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            Grid.SetColumn(new TextBlock { Text = b.Range, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), VerticalAlignment = VerticalAlignment.Center }, 0);
            var barBg = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), CornerRadius = new CornerRadius(3), Height = 22 };
            var barFg = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(b.Color)!,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = double.IsNaN(maxPct) || maxPct == 0 ? 0 : (b.Pct / maxPct) * 200
            };
            var barGrid = new Grid(); barGrid.Children.Add(barBg); barGrid.Children.Add(barFg);
            Grid.SetColumn(barGrid, 1);
            var pct = new TextBlock
            {
                Text = $"{b.Pct}%",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFromString(b.Color)!,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(pct, 2);

            var lbl = new TextBlock { Text = b.Range, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl); grid.Children.Add(barGrid); grid.Children.Add(pct);
            return grid;
        }

        private UIElement BuildTopStudentRow(TopStudent s)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var icon = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7))
            };
            icon.Child = new TextBlock { Text = s.Medal, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var info = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            info.Children.Add(new TextBlock { Text = $"{s.Name} – {s.ClassName}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) });
            info.Children.Add(new TextBlock { Text = $"Điểm TB: {s.Avg:0.0}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
            sp.Children.Add(icon); sp.Children.Add(info);
            return sp;
        }

        // -- Event handlers -------------------------------------------
        private void BtnSemester_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            foreach (var s in new[] { "Học kỳ 2 – 2024", "Học kỳ 1 – 2024", "Học kỳ 2 – 2023" })
            {
                var item = new MenuItem { Header = s };
                item.Click += (_, _) => { _semester = s; TxtSemesterLabel.Text = $"  {s}"; Refresh(); };
                menu.Items.Add(item);
            }
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"BaoCao_{_semester.Replace(" ", "_").Replace("–", "")}.csv",
                DefaultExt = ".csv",
                Filter = "CSV (*.csv)|*.csv"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Môn học,Lớp,Si số,Điểm TB,Đạt,Xuất sắc\n");
                foreach (var r in _reports)
                    sb.AppendLine($"{r.Subject},{r.ClassName},{r.StudentCount},{r.AvgScore:0.0},{r.PassRate:0}%,{r.ExcelRate:0}%");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                CustomDialog.Show($"Đã xuất: {dlg.FileName}", "Thành công", DialogType.Success);
            }
            catch (Exception ex) { CustomDialog.Show($"Lỗi: {ex.Message}", "Lỗi", DialogType.Error); }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true) pd.PrintVisual(this, $"Báo cáo {_semester}");
        }

        private void BtnViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string cls = btn.Tag?.ToString() ?? "";
            var r = _reports.FirstOrDefault(x => x.ClassName == cls);
            if (r == null) return;
            CustomDialog.Show(
                $"Lớp: {r.Subject} – {r.ClassName}\nSi số: {r.StudentCount}  |  Điểm TB: {r.AvgScore:0.0}\n" +
                $"Tỷ lệ đạt: {r.PassRate:0}%  |  Xuất sắc: {r.ExcelRate:0}%",
                "Chi tiết lớp", DialogType.Info);
        }

        private void BtnViewAllClasses_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new MyClassesView(_dbManager));
        }
    }
}
