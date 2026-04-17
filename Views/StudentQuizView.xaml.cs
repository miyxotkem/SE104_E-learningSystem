using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class StudentQuizView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private List<Exam> _allExams = new();

        public StudentQuizView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            RenderExams();
        }

        private async Task LoadDataAsync()
        {
            if (_dbManager == null || _dbManager.GetDb == null) return;
            try
            {
                var snapshot = await _dbManager.GetDb.Collection("exams")
                    .WhereEqualTo("IsPublished", true)
                    .GetSnapshotAsync();

                _allExams.Clear();
                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var exam = doc.ConvertTo<Exam>();
                        _allExams.Add(exam);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải bài kiểm tra: {ex.Message}");
            }
        }

        private void RenderExams()
        {
            ExamsPanel.Children.Clear();
            foreach (var exam in _allExams)
            {
                var card = new Border
                {
                    Width = 300,
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 16, 16),
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(12),
                    Effect = new DropShadowEffect { BlurRadius = 10, Opacity = 0.05, ShadowDepth = 2 }
                };

                var sp = new StackPanel();

                // Status tag
                var tag = new Border
                {
                    Background = exam.IsActive ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)) : new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = new TextBlock
                    {
                        Text = exam.IsActive ? "Đang mở" : "Đã đóng",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = exam.IsActive ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
                    }
                };
                sp.Children.Add(tag);

                // Title
                sp.Children.Add(new TextBlock
                {
                    Text = exam.Title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
                });

                // Description
                sp.Children.Add(new TextBlock
                {
                    Text = exam.Description,
                    FontSize = 13,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                // Meta
                sp.Children.Add(new TextBlock
                {
                    Text = $"⏱️ {exam.TimeLimitMinutes} phút   •   🎯 Giới hạn: {(int)exam.PassingScore}%",
                    FontSize = 12,
                    Foreground = Brushes.DimGray,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                var btnTake = new Button
                {
                    Content = exam.IsActive ? "Vào thi" : "Đã khóa",
                    Height = 35,
                    Style = (Style)FindResource("TakeBtn"),
                    Cursor = exam.IsActive ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Tag = exam.Id,
                    IsEnabled = exam.IsActive
                };
                
                btnTake.Click += BtnTake_Click;
                sp.Children.Add(btnTake);

                card.Child = sp;
                ExamsPanel.Children.Add(card);
            }
        }

        private void BtnTake_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string examId)
            {
                var selectedExam = _allExams.FirstOrDefault(x => x.Id == examId);
                var studentWin = Window.GetWindow(this) as StudentMainWindow;
                if (studentWin != null && selectedExam != null)
                {
                    studentWin.StudentContentArea.Content = new TakeQuizView(_dbManager, selectedExam);
                }
            }
        }
    }
}
