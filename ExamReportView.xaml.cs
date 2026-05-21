using e_learning_app;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using e_learning_app.Class;

namespace e_learning_app
{
    public partial class ExamReportView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Exam _exam;
        private List<ExamSubmission> _submissions = new();

        public ExamReportView(Exam exam, DatabaseManager dbManager)
        {
            InitializeComponent();
            _exam = exam;
            _dbManager = dbManager;
            
            TxtExamTitle.Text = exam.Title;
            TxtExamDesc.Text = $"{exam.TotalQuestions} câu hỏi | {exam.TimeLimitMinutes} phút | Điểm qua: {exam.PassingScore}%";
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            
            if (_dbManager != null && _exam != null)
            {
                var historyRes = await e_learning_app.Class.ApiService.GetAsync<System.Collections.Generic.List<e_learning_app.Class.ExamSubmissionResponse>>($"exams/{_exam.Id}/submissions");
                _submissions = historyRes != null ? historyRes.Select(h => h.Data).ToList() : new System.Collections.Generic.List<e_learning_app.Class.ExamSubmission>();
            }

            ProcessStatistics();
            UpdateTopStudents();
            
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProcessStatistics()
        {
            if (_submissions == null || _submissions.Count == 0)
            {
                TxtTotalSubmissions.Text = "0";
                TxtPassRate.Text = "0%";
                TxtAvgScore.Text = "0.0";
                TxtMaxScore.Text = "0.0";
                TxtMinScore.Text = "0.0";
                DgSubmissions.ItemsSource = null;
                DistributionPanel.Children.Clear();
                return;
            }

            int total = _submissions.Count;
            double passRate = _submissions.Count(s => s.Percentage >= _exam.PassingScore) * 100.0 / total;
            double avgScore = _submissions.Average(s => s.Score);
            double maxScore = _submissions.Max(s => s.Score);
            double minScore = _submissions.Min(s => s.Score);

            TxtTotalSubmissions.Text = total.ToString();
            TxtPassRate.Text = $"{passRate:0.0}%";
            TxtAvgScore.Text = avgScore.ToString("0.0");
            TxtMaxScore.Text = maxScore.ToString("0.0");
            TxtMinScore.Text = minScore.ToString("0.0");

            RefreshSubmissionList();
            DrawDistributionChart();
        }

        private void RefreshSubmissionList(string filter = "")
        {
            var list = _submissions.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                list = list.Where(s => s.StudentName.ToLower().Contains(filter.ToLower()));
            }

            var displayList = list.Select(s => new
            {
                SubmissionId = s.Id,
                s.StudentName,
                s.SubmittedAt,
                ScoreDisplay = $"{s.Score:F1}/{_exam.TotalQuestions}",
                s.Score,
                s.Percentage,
                TimeSpentFormatted = TimeSpan.FromSeconds(s.TimeSpentSeconds).ToString(@"mm\:ss"),
                StatusBg = s.Percentage >= _exam.PassingScore ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)) : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)),
                StatusFg = s.Percentage >= _exam.PassingScore ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
            }).OrderByDescending(s => s.Score).ToList();

            DgSubmissions.ItemsSource = displayList;
        }

        private void UpdateTopStudents()
        {
            TopStudentsPanel.Children.Clear();
            var top = _submissions.OrderByDescending(s => s.Score).Take(5).ToList();
            
            for (int i = 0; i < top.Count; i++)
            {
                var s = top[i];
                var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => (i + 1).ToString() };
                
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtMedal = new TextBlock { Text = medal, FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
                var txtName = new TextBlock { Text = s.StudentName, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), VerticalAlignment = VerticalAlignment.Center };
                var txtScore = new TextBlock { Text = s.Score.ToString("0.0"), FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), VerticalAlignment = VerticalAlignment.Center };

                Grid.SetColumn(txtMedal, 0);
                Grid.SetColumn(txtName, 1);
                Grid.SetColumn(txtScore, 2);

                grid.Children.Add(txtMedal);
                grid.Children.Add(txtName);
                grid.Children.Add(txtScore);

                TopStudentsPanel.Children.Add(grid);
            }
        }

        private void TxtSearchStudent_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = TxtSearchStudent.Text;
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(filter) ? Visibility.Visible : Visibility.Collapsed;
            RefreshSubmissionList(filter);
        }

        private void BtnViewSubmission_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string submissionId)
            {
                var submission = _submissions.FirstOrDefault(s => s.Id == submissionId);
                if (submission != null)
                {
                    if (Window.GetWindow(this) is MainWindow mw)
                    {
                        mw.NavigateTo(new Views.QuizResultDetailView(_dbManager, _exam, submission));
                    }
                }
            }
        }
        
        private void DrawDistributionChart()
        {
            DistributionPanel.Children.Clear();
            var bands = new List<(string Range, string Color, double MinPct, double MaxPct)>
            {
                ("Xuất sắc (90-100%)", "#22C55E", 90, 100),
                ("Khá giỏi (70-89%)", "#3B82F6", 70, 89.99),
                ("Trung bình (50-69%)", "#F59E0B", 50, 69.99),
                ("Yếu (<50%)", "#EF4444", 0, 49.99)
            };

            int maxCount = 0;
            var counts = new Dictionary<string, int>();
            foreach (var b in bands)
            {
                int count = _submissions.Count(s => s.Percentage >= b.MinPct && s.Percentage <= b.MaxPct);
                counts[b.Range] = count;
                if (count > maxCount) maxCount = count;
            }

            foreach (var b in bands)
            {
                int count = counts[b.Range];
                double widthPct = maxCount > 0 ? (double)count / maxCount * 100.0 : 0;
                
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                headerGrid.Children.Add(new TextBlock { Text = b.Range, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
                var txtCount = new TextBlock { Text = count.ToString() + " học sinh", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) };
                Grid.SetColumn(txtCount, 1);
                headerGrid.Children.Add(txtCount);
                
                var barBg = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), Height = 10, CornerRadius = new CornerRadius(5) };
                var barFg = new Border { 
                    Background = (Brush)new BrushConverter().ConvertFromString(b.Color), 
                    Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left,
                };

                var widthBinding = new Binding("ActualWidth") { Source = barBg, Converter = new PercentageConverter(), ConverterParameter = widthPct / 100.0 };
                barFg.SetBinding(Border.WidthProperty, widthBinding);

                var barGrid = new Grid();
                barGrid.Children.Add(barBg);
                barGrid.Children.Add(barFg);

                Grid.SetRow(barGrid, 1);
                grid.Children.Add(headerGrid);
                grid.Children.Add(barGrid);
                DistributionPanel.Children.Add(grid);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new ExamManagementView(_dbManager));
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_submissions == null || _submissions.Count == 0)
            {
                CustomDialog.Show("Chưa có dữ liệu để xuất!", "Thông báo", DialogType.Warning);
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"BaoCao_{_exam.Title.Replace(" ", "_")}.csv", DefaultExt = ".csv", Filter = "CSV (*.csv)|*.csv" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Học sinh,Thời gian nộp,Điểm,Phần trăm,Thời gian làm(giây)");
                    foreach (var s in _submissions) sb.AppendLine($"{s.StudentName},{s.SubmittedAt:dd/MM/yyyy HH:mm},{s.Score},{s.Percentage:0.0}%,{s.TimeSpentSeconds}");
                    System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    CustomDialog.Show($"Đã xuất thành công: {dlg.FileName}", "Thành công", DialogType.Success);
                }
                catch (Exception ex) { CustomDialog.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", DialogType.Error); }
            }
        }
    }

    public class PercentageConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (value is double width && parameter is double pct) ? width * pct : 0;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
