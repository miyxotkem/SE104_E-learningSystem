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
                _submissions = await _dbManager.GetSubmissionsByExamAsync(_exam.Id);
            }

            ProcessStatistics();
            
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProcessStatistics()
        {
            if (_submissions == null || _submissions.Count == 0)
            {
                // No submissions
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

            // Format data for datagrid
            var displayList = _submissions.Select(s => new
            {
                s.StudentName,
                s.SubmittedAt,
                s.Score,
                s.Percentage,
                TimeSpentFormatted = TimeSpan.FromSeconds(s.TimeSpentSeconds).ToString(@"mm\:ss")
            }).OrderByDescending(s => s.Score).ToList();

            DgSubmissions.ItemsSource = displayList;

            // Draw Distribution
            DrawDistributionChart();
        }

        private void DrawDistributionChart()
        {
            DistributionPanel.Children.Clear();

            // Ranges based on Percentage to be robust against different max scores
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
                
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var txtLabel = new TextBlock { Text = b.Range, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)) };
                var txtCount = new TextBlock { Text = count.ToString() + " hs", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) };
                
                Grid.SetColumn(txtLabel, 0);
                Grid.SetColumn(txtCount, 1);
                headerGrid.Children.Add(txtLabel);
                headerGrid.Children.Add(txtCount);
                
                Grid.SetRow(headerGrid, 0);
                grid.Children.Add(headerGrid);

                var barBg = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), Height = 12, CornerRadius = new CornerRadius(6) };
                var barFg = new Border { 
                    Background = (Brush)new BrushConverter().ConvertFromString(b.Color), 
                    Height = 12, 
                    CornerRadius = new CornerRadius(6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };

                // Bind width proportion to parent container size
                var widthBinding = new Binding("ActualWidth")
                {
                    Source = barBg,
                    Converter = new PercentageConverter(),
                    ConverterParameter = widthPct / 100.0
                };
                barFg.SetBinding(Border.WidthProperty, widthBinding);

                var barGrid = new Grid();
                barGrid.Children.Add(barBg);
                barGrid.Children.Add(barFg);

                Grid.SetRow(barGrid, 1);
                grid.Children.Add(barGrid);

                DistributionPanel.Children.Add(grid);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                // Go back to Exam Management View
                mw.NavigateTo(new ExamManagementView(_dbManager));
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_submissions == null || _submissions.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"BaoCao_{_exam.Title.Replace(" ", "_")}.csv",
                DefaultExt = ".csv",
                Filter = "CSV (*.csv)|*.csv"
            };
            
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Hoc sinh,Thoi gian nop,Diem,Phan tram,Thoi gian lam(giay)");
                    foreach (var s in _submissions)
                    {
                        sb.AppendLine($"{s.StudentName},{s.SubmittedAt:dd/MM/yyyy HH:mm},{s.Score},{s.Percentage:0.0}%,{s.TimeSpentSeconds}");
                    }
                    System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Đã xuất thành công: {dlg.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class PercentageConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double width && parameter is double pct)
            {
                return width * pct;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
