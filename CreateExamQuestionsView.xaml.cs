using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClosedXML.Excel;
using e_learning_app.Class;

namespace e_learning_app
{
    public class ImportQuestionItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _content;
        public string Content { get => _content; set { _content = value; Validate(); OnPropertyChanged(nameof(Content)); } }
        
        private string _optA;
        public string OptA { get => _optA; set { _optA = value; Validate(); OnPropertyChanged(nameof(OptA)); } }
        
        private string _optB;
        public string OptB { get => _optB; set { _optB = value; Validate(); OnPropertyChanged(nameof(OptB)); } }
        
        private string _optC;
        public string OptC { get => _optC; set { _optC = value; Validate(); OnPropertyChanged(nameof(OptC)); } }
        
        private string _optD;
        public string OptD { get => _optD; set { _optD = value; Validate(); OnPropertyChanged(nameof(OptD)); } }
        
        private string _correctAns;
        public string CorrectAns { get => _correctAns; set { _correctAns = value; Validate(); OnPropertyChanged(nameof(CorrectAns)); } }

        private bool _isValid;
        public bool IsValid { get => _isValid; set { _isValid = value; OnPropertyChanged(nameof(IsValid)); } }

        private string _errorMessage;
        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); } }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Content)) { IsValid = false; ErrorMessage = "Lỗi: Thiếu nội dung"; return; }
            if (string.IsNullOrWhiteSpace(OptA) || string.IsNullOrWhiteSpace(OptB) || string.IsNullOrWhiteSpace(OptC) || string.IsNullOrWhiteSpace(OptD)) { IsValid = false; ErrorMessage = "Lỗi: Thiếu đáp án"; return; }
            var ans = CorrectAns?.Trim().ToUpper();
            if (ans != "A" && ans != "B" && ans != "C" && ans != "D") { IsValid = false; ErrorMessage = "Lỗi: Đáp án đúng phải là A,B,C,D"; return; }
            
            IsValid = true;
            ErrorMessage = "Hợp lệ";
        }
    }

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
                mw.NavigateTo(new CreateExamView(_dbManager, _exam));
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_questions.Count == 0)
            {
                CustomDialog.Show("Vui lòng thêm ít nhất 1 câu hỏi hợp lệ cho bài thi!", "Cảnh báo", DialogType.Warning);
                return;
            }

            try
            {
                BtnSubmit.IsEnabled = false;
                BtnSubmit.Content = "Đang lưu dữ liệu...";

                _exam.TotalQuestions = _questions.Count;
                _exam.QuestionIds.Clear();
                foreach (var q in _questions)
                {
                    _exam.QuestionIds.Add(q.Id);
                }

                var request = new e_learning_app.Class.CreateExamRequest
                {
                    CourseId = _exam.ClassId,
                    Title = _exam.Title,
                    Description = _exam.Description,
                    DurationMinutes = _exam.TimeLimitMinutes,
                    PassingScore = _exam.PassingScore,
                    IsPublished = _exam.IsPublished,
                    IsActive = _exam.IsActive,
                    AllowReview = _exam.AllowReview,
                    RandomizeQuestions = _exam.RandomizeQuestions,
                    ShowScore = _exam.ShowScore,
                    AllowMultipleAttempts = _exam.AllowMultipleAttempts,
                    MaxAttempts = _exam.MaxAttempts,
                    Questions = _questions.Select(q => new e_learning_app.Class.QuestionModel
                    {
                        QuestionText = q.Content,
                        Options = q.Options ?? new System.Collections.Generic.List<string>(),
                        CorrectOptionIndex = q.CorrectAnswerIndex,
                        Points = q.Points
                    }).ToList()
                };
                var response = await e_learning_app.Class.ApiService.PostAsync<dynamic>("exams", request);
                bool success = response != null;

                if (success)
                {
                    if (_exam.IsPublished)
                    {
                        try
                        {
                            var currentUser = _dbManager.GetCurrentUser();
                            await NotificationService.SendToClassAsync(
                                _dbManager, 
                                _exam.ClassId, 
                                "Bài kiểm tra mới", 
                                $"Giáo viên vừa tạo bài kiểm tra: {_exam.Title}", 
                                "Exam", 
                                currentUser?.Id, 
                                "Giáo viên"
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Lỗi gửi thông báo: " + ex.Message);
                        }
                    }

                    CustomDialog.Show($"Tạo bài thi thành công!\nTổng cộng: {_questions.Count} câu hỏi.", "Thành Công", DialogType.Success);
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
                CustomDialog.Show($"Lỗi khi lưu: {ex.Message}", "Lỗi", DialogType.Error);
                BtnSubmit.IsEnabled = true;
                BtnSubmit.Content = "Hoàn Tất Tạo Bài Thi";
            }
        }

        private void BtnAddManual_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtContent.Text) ||
                string.IsNullOrWhiteSpace(TxtOptA.Text) || string.IsNullOrWhiteSpace(TxtOptB.Text) ||
                string.IsNullOrWhiteSpace(TxtOptC.Text) || string.IsNullOrWhiteSpace(TxtOptD.Text))
            {
                CustomDialog.Show("Vui lòng nhập đầy đủ nội dung câu hỏi và 4 đáp án.", "Cảnh báo", DialogType.Warning);
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

            TxtContent.Clear();
            TxtOptA.Clear(); TxtOptB.Clear(); TxtOptC.Clear(); TxtOptD.Clear();
            RbOptA.IsChecked = true;
            TxtPoints.Text = "1";
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "Exam_Template",
                DefaultExt = ".xlsx",
                Filter = "Excel files (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Questions");
                    
                    worksheet.Cell(1, 1).Value = "Câu hỏi";
                    worksheet.Cell(1, 2).Value = "Đáp án A";
                    worksheet.Cell(1, 3).Value = "Đáp án B";
                    worksheet.Cell(1, 4).Value = "Đáp án C";
                    worksheet.Cell(1, 5).Value = "Đáp án D";
                    worksheet.Cell(1, 6).Value = "Đáp án đúng";
                    
                    var headerRange = worksheet.Range("A1:F1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    
                    worksheet.Cell(2, 1).Value = "Thủ đô của Việt Nam là gì?";
                    worksheet.Cell(2, 2).Value = "Hà Nội";
                    worksheet.Cell(2, 3).Value = "Hồ Chí Minh";
                    worksheet.Cell(2, 4).Value = "Đà Nẵng";
                    worksheet.Cell(2, 5).Value = "Hải Phòng";
                    worksheet.Cell(2, 6).Value = "A";
                    
                    worksheet.Columns().AdjustToContents();
                    
                    workbook.SaveAs(dlg.FileName);
                    CustomDialog.Show("Đã tải file mẫu thành công!", "Thành công", DialogType.Success);
                }
            }
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropZone.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.Background = Brushes.White;
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Background = Brushes.White;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    await ProcessExcelFileAsync(files[0]);
                }
            }
        }

        private async void DropZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                Title = "Chọn file câu hỏi (Excel)"
            };

            if (dlg.ShowDialog() == true)
            {
                await ProcessExcelFileAsync(dlg.FileName);
            }
        }

        private void BtnImportExcel_Click(object sender, RoutedEventArgs e) { }

        private async Task ProcessExcelFileAsync(string filePath)
        {
            if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                CustomDialog.Show("Vui lòng chọn file Excel (.xlsx) hợp lệ.", "Lỗi định dạng", DialogType.Warning);
                return;
            }

            ProgressPanel.Visibility = Visibility.Visible;
            ImportProgress.Value = 0;
            TxtImportStatus.Text = "Đang mở file Excel...";
            
            var newItems = new List<ImportQuestionItem>();

            try
            {
                await Task.Run(() =>
                {
                    using (var workbook = new XLWorkbook(filePath))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rangeUsed = worksheet.RangeUsed();
                        if (rangeUsed == null) return;
                        var rows = rangeUsed.RowsUsed();
                        
                        int totalRows = 0;
                        foreach (var row in rows) totalRows++;
                        
                        int currentRow = 0;
                        foreach (var row in rows)
                        {
                            currentRow++;
                            if (currentRow == 1) continue;

                            var item = new ImportQuestionItem
                            {
                                Content = row.Cell(1).GetString(),
                                OptA = row.Cell(2).GetString(),
                                OptB = row.Cell(3).GetString(),
                                OptC = row.Cell(4).GetString(),
                                OptD = row.Cell(5).GetString(),
                                CorrectAns = row.Cell(6).GetString()
                            };
                            item.Validate();
                            newItems.Add(item);

                            Dispatcher.Invoke(() =>
                            {
                                ImportProgress.Value = (double)currentRow / totalRows * 100;
                                TxtImportStatus.Text = $"Đang đọc dữ liệu: {currentRow}/{totalRows} dòng...";
                            });
                        }
                    }
                });

                int added = 0;
                int failed = 0;
                foreach (var item in newItems)
                {
                    if (item.IsValid)
                    {
                        int correctIdx = item.CorrectAns.Trim().ToUpper() switch {
                            "A" => 0, "B" => 1, "C" => 2, "D" => 3, _ => 0
                        };
                        var newQuestion = new ExamQuestion
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            QuestionOrder = _questions.Count + 1,
                            Type = QuestionType.MultipleChoice,
                            Content = item.Content,
                            Options = new List<string> { item.OptA, item.OptB, item.OptC, item.OptD },
                            CorrectAnswerIndex = correctIdx,
                            Points = 1.0
                        };
                        _questions.Add(newQuestion);
                        added++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                RefreshQuestionsList();
                ProgressPanel.Visibility = Visibility.Collapsed;
                
                string msg = $"Đã import thành công {added} câu hỏi lên danh sách.";
                if (failed > 0) msg += $"\nĐã tự động bỏ qua {failed} câu bị lỗi/thiếu dữ liệu trong file.";
                CustomDialog.Show(msg, "Đọc file hoàn tất", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi đọc file: " + ex.Message, "Lỗi", DialogType.Error);
                ProgressPanel.Visibility = Visibility.Collapsed;
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
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1)
                };

                var sp = new StackPanel();
                
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
                    Content = "Xóa",
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

                var txtContent = new TextBlock 
                { 
                    Text = q.Content, 
                    TextWrapping = TextWrapping.Wrap, 
                    Margin = new Thickness(0,0,0,10),
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
                };
                sp.Children.Add(txtContent);

                char[] optionLetters = { 'A', 'B', 'C', 'D' };
                for (int i = 0; i < q.Options.Count; i++)
                {
                    bool isCorrect = (i == q.CorrectAnswerIndex);
                    var optText = new TextBlock
                    {
                        Text = $"{optionLetters[i]}. {q.Options[i]}",
                        Foreground = isCorrect ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
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
