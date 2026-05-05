using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e_learning_app.Class;

namespace e_learning_app
{
    public partial class CreateExamQuestionsView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Exam _exam;
        private List<ExamQuestion> _questions = new List<ExamQuestion>();

        public CreateExamQuestionsView(DatabaseManager dbManager, Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
            
            TxtExamInfo.Text = $"Đang tạo bài thi: {_exam.Title} ({_exam.ClassName})";
            RefreshQuestionsList();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                // Note: Trở lại màn hình CreateExamView có thể làm mất dữ liệu nhập dở, 
                // nhưng hiện tại ta có thể quay lại ExamManagementView hoặc tạo mới lại.
                // Ở đây ta quay về Quản lý bài thi để đơn giản hóa.
                mw.NavigateTo(new ExamManagementView(_dbManager));
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_questions.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất 1 câu hỏi cho bài thi!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnSubmit.IsEnabled = false;
                BtnSubmit.Content = "⏳ Đang lưu dữ liệu...";

                // Cập nhật lại số lượng câu hỏi và danh sách ID
                _exam.TotalQuestions = _questions.Count;
                _exam.QuestionIds.Clear();
                foreach (var q in _questions)
                {
                    _exam.QuestionIds.Add(q.Id);
                }

                bool success = await _dbManager.SaveExamWithQuestionsAsync(_exam, _questions);

                if (success)
                {
                    MessageBox.Show($"✅ Tạo bài thi \"{_exam.Title}\" với {_questions.Count} câu hỏi thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (Window.GetWindow(this) is MainWindow mw)
                    {
                        mw.NavigateTo(new ExamManagementView(_dbManager));
                    }
                }
                else
                {
                    throw new Exception("Firebase từ chối thao tác lưu.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Lỗi khi lưu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSubmit.IsEnabled = true;
                BtnSubmit.Content = "✅ Hoàn Tất Tạo Bài Thi";
            }
        }

        private void BtnAddManual_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtContent.Text) ||
                string.IsNullOrWhiteSpace(TxtOptA.Text) || string.IsNullOrWhiteSpace(TxtOptB.Text) ||
                string.IsNullOrWhiteSpace(TxtOptC.Text) || string.IsNullOrWhiteSpace(TxtOptD.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ nội dung câu hỏi và 4 đáp án.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int correctIdx = 0;
            if (RbOptB.IsChecked == true) correctIdx = 1;
            else if (RbOptC.IsChecked == true) correctIdx = 2;
            else if (RbOptD.IsChecked == true) correctIdx = 3;

            double points = 1.0;
            if (double.TryParse(TxtPoints.Text, out double p)) points = p;

            var newQuestion = new ExamQuestion
            {
                Id = Guid.NewGuid().ToString("N"),
                QuestionOrder = _questions.Count + 1,
                Type = QuestionType.MultipleChoice,
                Content = TxtContent.Text.Trim(),
                Options = new List<string> { TxtOptA.Text.Trim(), TxtOptB.Text.Trim(), TxtOptC.Text.Trim(), TxtOptD.Text.Trim() },
                CorrectAnswerIndex = correctIdx,
                Points = points
            };

            _questions.Add(newQuestion);
            RefreshQuestionsList();

            // Clear form
            TxtContent.Clear();
            TxtOptA.Clear(); TxtOptB.Clear(); TxtOptC.Clear(); TxtOptD.Clear();
            RbOptA.IsChecked = true;
            TxtPoints.Text = "1";
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Chọn file câu hỏi (TXT)"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(dlg.FileName);
                    int addedCount = 0;

                    // Parse the TXT file. Simple format: 6 lines per question.
                    // 1: Content
                    // 2: Opt A
                    // 3: Opt B
                    // 4: Opt C
                    // 5: Opt D
                    // 6: Correct Answer Index (0-3)
                    for (int i = 0; i < lines.Length; i += 6)
                    {
                        // Skip empty lines between questions
                        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                        {
                            i++;
                        }

                        if (i + 5 < lines.Length)
                        {
                            string content = lines[i];
                            string optA = lines[i + 1];
                            string optB = lines[i + 2];
                            string optC = lines[i + 3];
                            string optD = lines[i + 4];
                            
                            int correctIdx = 0;
                            int.TryParse(lines[i + 5].Trim(), out correctIdx);

                            var q = new ExamQuestion
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                QuestionOrder = _questions.Count + 1,
                                Type = QuestionType.MultipleChoice,
                                Content = content,
                                Options = new List<string> { optA, optB, optC, optD },
                                CorrectAnswerIndex = correctIdx,
                                Points = 1
                            };

                            _questions.Add(q);
                            addedCount++;
                        }
                    }

                    RefreshQuestionsList();
                    MessageBox.Show($"Đã import thành công {addedCount} câu hỏi!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi đọc file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshQuestionsList()
        {
            TxtQuestionCount.Text = $"{_questions.Count} câu";
            QuestionsListPanel.Children.Clear();

            foreach (var q in _questions)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), // #F8FAFC
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), // #E2E8F0
                    BorderThickness = new Thickness(1)
                };

                var sp = new StackPanel();
                
                // Header: Question number + points + Delete Button
                var headerGrid = new Grid { Margin = new Thickness(0,0,0,8) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtTitle = new TextBlock 
                { 
                    Text = $"Câu {q.QuestionOrder} ({q.Points} điểm)", 
                    FontWeight = FontWeights.Bold, 
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) 
                };
                Grid.SetColumn(txtTitle, 0);
                headerGrid.Children.Add(txtTitle);

                var btnDelete = new Button
                {
                    Content = "🗑️ Xóa",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Brushes.Red,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = q.Id
                };
                btnDelete.Click += BtnDelete_Click;
                Grid.SetColumn(btnDelete, 1);
                headerGrid.Children.Add(btnDelete);

                sp.Children.Add(headerGrid);

                // Content
                var txtContent = new TextBlock 
                { 
                    Text = q.Content, 
                    TextWrapping = TextWrapping.Wrap, 
                    Margin = new Thickness(0,0,0,10),
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
                };
                sp.Children.Add(txtContent);

                // Options
                char[] optionLetters = { 'A', 'B', 'C', 'D' };
                for (int i = 0; i < q.Options.Count; i++)
                {
                    bool isCorrect = (i == q.CorrectAnswerIndex);
                    var optText = new TextBlock
                    {
                        Text = $"{optionLetters[i]}. {q.Options[i]}",
                        Foreground = isCorrect ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Green if correct, gray otherwise
                        FontWeight = isCorrect ? FontWeights.Bold : FontWeights.Normal,
                        Margin = new Thickness(8, 0, 0, 4),
                        TextWrapping = TextWrapping.Wrap
                    };
                    sp.Children.Add(optText);
                }

                card.Child = sp;
                QuestionsListPanel.Children.Add(card);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var q = _questions.Find(x => x.Id == id);
                if (q != null)
                {
                    _questions.Remove(q);
                    // Reorder
                    for (int i = 0; i < _questions.Count; i++)
                    {
                        _questions[i].QuestionOrder = i + 1;
                    }
                    RefreshQuestionsList();
                }
            }
        }
    }
}
