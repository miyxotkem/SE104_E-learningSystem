using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClosedXML.Excel;
using e_learning_app.Class;

namespace e_learning_app
{
    public partial class EditExamView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Exam _exam;
        private List<QuestionEditModel> _questions = new();
        private List<string> _originalQuestionIds = new(); // ID gốc để so sánh khi save
        private bool _isPartialEdit = false;

        public EditExamView(DatabaseManager dbManager, Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
            TxtSubHeader.Text = $"{exam.Title} — {exam.ClassName}";
        }

        // ==================== LIFECYCLE ====================
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            TxtLoadingMessage.Text = "Đang kiểm tra trạng thái bài thi...";

            try
            {
                // Bước 1: Kiểm tra submissions
                int subCount = await _dbManager.GetSubmissionCountByExamAsync(_exam.Id);
                _isPartialEdit = subCount > 0;

                // Bước 2: Load câu hỏi
                TxtLoadingMessage.Text = "Đang tải câu hỏi...";
                var questions = await _dbManager.GetExamQuestionsAsync(_exam.Id);
                _questions = questions.OrderBy(q => q.QuestionOrder).Select(QuestionEditModel.FromExamQuestion).ToList();
                _originalQuestionIds = questions.Select(q => q.Id).ToList();

                // Bước 3: Pre-fill form
                PreFillForm();

                // Bước 4: Áp dụng chế độ edit
                if (_isPartialEdit) ApplyPartialEditMode(subCount);

                RefreshQuestionsList();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi tải dữ liệu: {ex.Message}", "Lỗi", DialogType.Error);
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // ==================== PRE-FILL ====================
        private void PreFillForm()
        {
            TxtTitle.Text = _exam.Title;
            TxtDescription.Text = _exam.Description;

            // Time limit
            foreach (ComboBoxItem item in CbTimeLimit.Items)
                if (item.Content.ToString() == _exam.TimeLimitMinutes.ToString()) { CbTimeLimit.SelectedItem = item; break; }
            if (CbTimeLimit.SelectedItem == null) CbTimeLimit.SelectedIndex = 3; // 60 default

            // Passing score
            foreach (ComboBoxItem item in CbPassingScore.Items)
                if (item.Content.ToString() == ((int)_exam.PassingScore).ToString()) { CbPassingScore.SelectedItem = item; break; }
            if (CbPassingScore.SelectedItem == null) CbPassingScore.SelectedIndex = 1; // 50 default

            // Deadline
            if (_exam.Deadline.HasValue)
            {
                var local = _exam.Deadline.Value.ToLocalTime();
                DpDeadline.SelectedDate = local.Date;
                CbDeadlineTime.Text = local.ToString("HH:mm");
            }

            // Checkboxes
            ChkPublished.IsChecked = _exam.IsPublished;
            ChkActive.IsChecked = _exam.IsActive;
            ChkAllowReview.IsChecked = _exam.AllowReview;
            ChkRandomize.IsChecked = _exam.RandomizeQuestions;
            ChkShowScore.IsChecked = _exam.ShowScore;
            ChkMultipleAttempts.IsChecked = _exam.AllowMultipleAttempts;
            TxtMaxAttempts.Text = _exam.MaxAttempts.ToString();
            PanelMaxAttempts.Visibility = _exam.AllowMultipleAttempts ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================== PARTIAL EDIT MODE ====================
        private void ApplyPartialEditMode(int subCount)
        {
            InfoBarPartialEdit.Visibility = Visibility.Visible;
            TxtSubHeader.Text += $" — ⚠️ {subCount} bài nộp";

            // Khóa phần câu hỏi
            PanelAddQuestion.Visibility = Visibility.Collapsed;
            BtnImportExcel.Visibility = Visibility.Collapsed;
            BtnDownloadTemplate.Visibility = Visibility.Collapsed;
        }

        // ==================== QUESTIONS LIST ====================
        private void RefreshQuestionsList()
        {
            QuestionsListPanel.Children.Clear();
            TxtQuestionCount.Text = $" ({_questions.Count} câu)";

            for (int i = 0; i < _questions.Count; i++)
            {
                _questions[i].Order = i + 1;
                QuestionsListPanel.Children.Add(BuildQuestionCard(_questions[i]));
            }
        }

        private Border BuildQuestionCard(QuestionEditModel q)
        {
            var card = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1),
                Tag = q
            };
            card.Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.03, Color = Colors.Black };

            var mainStack = new StackPanel();

            // === Header: Câu X + nút Sửa/Xóa ===
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtTitle = new TextBlock
            {
                Text = $"Câu {q.Order}  ({q.Points} đ)", FontWeight = FontWeights.Bold, FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtTitle, 0);
            headerGrid.Children.Add(txtTitle);

            if (!_isPartialEdit)
            {
                var btnEdit = MakeIconButton("✏️", "#3B82F6");
                btnEdit.Tag = q;
                btnEdit.Click += (s, e) => { q.IsExpanded = !q.IsExpanded; RefreshQuestionsList(); };
                Grid.SetColumn(btnEdit, 1);
                headerGrid.Children.Add(btnEdit);

                var btnDel = MakeIconButton("🗑️", "#EF4444");
                btnDel.Tag = q;
                btnDel.Click += BtnDeleteQuestion_Click;
                Grid.SetColumn(btnDel, 2);
                headerGrid.Children.Add(btnDel);
            }
            mainStack.Children.Add(headerGrid);

            // === Nội dung rút gọn ===
            var txtContent = new TextBlock
            {
                Text = q.Content, TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), Margin = new Thickness(0, 0, 0, 6)
            };
            mainStack.Children.Add(txtContent);

            // Hiển thị 4 đáp án (read-only summary)
            if (!q.IsExpanded)
            {
                string[] opts = { q.OptA, q.OptB, q.OptC, q.OptD };
                string[] labels = { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    bool isCorrect = labels[i] == q.CorrectAnswer;
                    mainStack.Children.Add(new TextBlock
                    {
                        Text = $"  {labels[i]}. {opts[i]}", FontSize = 12, Margin = new Thickness(4, 1, 0, 1),
                        Foreground = isCorrect ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)) : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                        FontWeight = isCorrect ? FontWeights.Bold : FontWeights.Normal
                    });
                }
            }

            // === Inline Edit Form (expanded) ===
            if (q.IsExpanded && !_isPartialEdit)
            {
                var editPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 0)
                };
                var sp = new StackPanel();

                sp.Children.Add(MakeLabel("Nội dung câu hỏi:"));
                var tbContent = new TextBox { Text = q.Content, TextWrapping = TextWrapping.Wrap, Height = 50, Padding = new Thickness(8, 6, 8, 6), FontSize = 12, BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), Margin = new Thickness(0, 0, 0, 8) };
                tbContent.TextChanged += (s, e) => q.Content = tbContent.Text;
                sp.Children.Add(tbContent);

                string[] optLabels = { "A", "B", "C", "D" };
                string[] optVals = { q.OptA, q.OptB, q.OptC, q.OptD };
                var optBoxes = new TextBox[4];
                for (int i = 0; i < 4; i++)
                {
                    sp.Children.Add(MakeLabel($"Đáp án {optLabels[i]}:"));
                    var tb = new TextBox { Text = optVals[i], Height = 30, Padding = new Thickness(8, 4, 8, 4), FontSize = 12, BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), Margin = new Thickness(0, 0, 0, 6) };
                    int idx = i;
                    tb.TextChanged += (s, e) => { if (idx == 0) q.OptA = tb.Text; else if (idx == 1) q.OptB = tb.Text; else if (idx == 2) q.OptC = tb.Text; else q.OptD = tb.Text; };
                    optBoxes[i] = tb;
                    sp.Children.Add(tb);
                }

                // Đáp án đúng + Điểm
                var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                bottomRow.Children.Add(MakeLabel("Đáp án đúng:"));
                var cbCorrect = new ComboBox { Width = 60, Height = 28, FontSize = 12, Margin = new Thickness(6, 0, 16, 0) };
                foreach (var l in optLabels) cbCorrect.Items.Add(l);
                cbCorrect.SelectedItem = q.CorrectAnswer;
                cbCorrect.SelectionChanged += (s, e) => { if (cbCorrect.SelectedItem is string v) q.CorrectAnswer = v; };
                bottomRow.Children.Add(cbCorrect);

                bottomRow.Children.Add(MakeLabel("Điểm:"));
                var tbPoints = new TextBox { Text = q.Points.ToString(), Width = 50, Height = 28, Padding = new Thickness(6, 4, 6, 4), FontSize = 12, Margin = new Thickness(6, 0, 0, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)) };
                tbPoints.TextChanged += (s, e) => { if (double.TryParse(tbPoints.Text, out double p)) q.Points = p; };
                bottomRow.Children.Add(tbPoints);
                sp.Children.Add(bottomRow);

                editPanel.Child = sp;
                mainStack.Children.Add(editPanel);
            }

            card.Child = mainStack;
            return card;
        }

        // ==================== ACTIONS ====================
        private void BtnDeleteQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is QuestionEditModel q)
            {
                bool confirmed = CustomDialog.Confirm($"Xóa câu hỏi #{q.Order}?\n\n\"{q.Content}\"", "Xác nhận xóa", "Xóa", "Hủy", DialogType.Warning);
                if (confirmed) { _questions.Remove(q); RefreshQuestionsList(); }
            }
        }

        private void BtnAddQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewContent.Text))
            { CustomDialog.Show("Vui lòng nhập nội dung câu hỏi.", "Thiếu dữ liệu", DialogType.Warning); return; }

            string correct = "A";
            if (RbNewB.IsChecked == true) correct = "B";
            else if (RbNewC.IsChecked == true) correct = "C";
            else if (RbNewD.IsChecked == true) correct = "D";

            _questions.Add(new QuestionEditModel
            {
                OriginalId = null, Order = _questions.Count + 1,
                Content = TxtNewContent.Text.Trim(),
                OptA = TxtNewOptA.Text.Trim(), OptB = TxtNewOptB.Text.Trim(),
                OptC = TxtNewOptC.Text.Trim(), OptD = TxtNewOptD.Text.Trim(),
                CorrectAnswer = correct, Points = 1.0
            });

            TxtNewContent.Clear(); TxtNewOptA.Clear(); TxtNewOptB.Clear(); TxtNewOptC.Clear(); TxtNewOptD.Clear();
            RbNewA.IsChecked = true;
            RefreshQuestionsList();
        }

        private void ChkMultipleAttempts_Changed(object sender, RoutedEventArgs e)
        {
            PanelMaxAttempts.Visibility = ChkMultipleAttempts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================== EXCEL IMPORT ====================
        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "Exam_Template", DefaultExt = ".xlsx", Filter = "Excel (*.xlsx)|*.xlsx" };
            if (dlg.ShowDialog() == true)
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Questions");
                ws.Cell(1, 1).Value = "Câu hỏi"; ws.Cell(1, 2).Value = "Đáp án A"; ws.Cell(1, 3).Value = "Đáp án B";
                ws.Cell(1, 4).Value = "Đáp án C"; ws.Cell(1, 5).Value = "Đáp án D"; ws.Cell(1, 6).Value = "Đáp án đúng";
                ws.Range("A1:F1").Style.Font.Bold = true;
                ws.Range("A1:F1").Style.Fill.BackgroundColor = XLColor.LightBlue;
                ws.Cell(2, 1).Value = "Thủ đô của Việt Nam là gì?";
                ws.Cell(2, 2).Value = "Hà Nội"; ws.Cell(2, 3).Value = "Hồ Chí Minh"; ws.Cell(2, 4).Value = "Đà Nẵng"; ws.Cell(2, 5).Value = "Hải Phòng"; ws.Cell(2, 6).Value = "A";
                ws.Columns().AdjustToContents();
                wb.SaveAs(dlg.FileName);
                CustomDialog.Show("Đã tải file mẫu thành công!", "Thành công", DialogType.Success);
            }
        }

        private async void BtnImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Chọn file câu hỏi" };
            if (dlg.ShowDialog() == true) await ProcessExcelFileAsync(dlg.FileName);
        }

        private async Task ProcessExcelFileAsync(string filePath)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            TxtLoadingMessage.Text = "Đang đọc file Excel...";

            try
            {
                int added = 0, failed = 0;
                await Task.Run(() =>
                {
                    using var wb = new XLWorkbook(filePath);
                    var ws = wb.Worksheet(1);
                    var range = ws.RangeUsed();
                    if (range == null) return;

                    int row = 0;
                    foreach (var r in range.RowsUsed())
                    {
                        row++;
                        if (row == 1) continue; // skip header

                        string content = r.Cell(1).GetString(), a = r.Cell(2).GetString(), b = r.Cell(3).GetString(),
                               c = r.Cell(4).GetString(), d = r.Cell(5).GetString(), ans = r.Cell(6).GetString().Trim().ToUpper();

                        if (string.IsNullOrWhiteSpace(content) || (ans != "A" && ans != "B" && ans != "C" && ans != "D"))
                        { failed++; continue; }

                        _questions.Add(new QuestionEditModel
                        {
                            OriginalId = null, Content = content, OptA = a, OptB = b, OptC = c, OptD = d,
                            CorrectAnswer = ans, Points = 1.0
                        });
                        added++;
                    }
                });

                RefreshQuestionsList();
                string msg = $"Import thành công {added} câu hỏi.";
                if (failed > 0) msg += $"\nBỏ qua {failed} câu bị lỗi.";
                CustomDialog.Show(msg, "Hoàn tất", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi đọc file: {ex.Message}", "Lỗi", DialogType.Error);
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // ==================== SAVE ====================
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            { CustomDialog.Show("Tên bài thi không được để trống.", "Thiếu dữ liệu", DialogType.Warning); return; }

            BtnSave.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;
            TxtLoadingMessage.Text = "Đang lưu thay đổi...";

            try
            {
                // Cập nhật Exam object từ form
                _exam.Title = TxtTitle.Text.Trim();
                _exam.Description = TxtDescription.Text?.Trim() ?? "";
                if (CbTimeLimit.SelectedItem is ComboBoxItem tItem && int.TryParse(tItem.Content.ToString(), out int t)) _exam.TimeLimitMinutes = t;
                if (CbPassingScore.SelectedItem is ComboBoxItem pItem && int.TryParse(pItem.Content.ToString(), out int p)) _exam.PassingScore = p;

                // Deadline
                if (DpDeadline.SelectedDate.HasValue)
                {
                    var date = DpDeadline.SelectedDate.Value;
                    if (TimeSpan.TryParse(CbDeadlineTime.Text, out TimeSpan time))
                        _exam.Deadline = (date + time).ToUniversalTime();
                    else
                        _exam.Deadline = date.ToUniversalTime();
                }

                _exam.IsPublished = ChkPublished.IsChecked == true;
                _exam.IsActive = ChkActive.IsChecked == true;
                _exam.AllowReview = ChkAllowReview.IsChecked == true;
                _exam.RandomizeQuestions = ChkRandomize.IsChecked == true;
                _exam.ShowScore = ChkShowScore.IsChecked == true;
                _exam.AllowMultipleAttempts = ChkMultipleAttempts.IsChecked == true;
                if (int.TryParse(TxtMaxAttempts.Text, out int ma)) _exam.MaxAttempts = ma;
                _exam.UpdatedAt = DateTime.UtcNow;

                if (_isPartialEdit)
                {
                    // Partial edit: chỉ cập nhật metadata bài thi
                    bool success = await _dbManager.UpdateExamAsync(_exam);
                    if (!success) { BtnSave.IsEnabled = true; LoadingOverlay.Visibility = Visibility.Collapsed; return; }
                }
                else
                {
                    // Full edit: cập nhật cả câu hỏi bằng batch
                    var newQuestions = _questions.Select(q => q.ToExamQuestion()).ToList();
                    _exam.TotalQuestions = newQuestions.Count;
                    _exam.QuestionIds = newQuestions.Select(q => q.Id).ToList();

                    // Xóa câu hỏi cũ bị loại bỏ
                    var newIds = newQuestions.Select(q => q.Id).ToHashSet();
                    foreach (var oldId in _originalQuestionIds)
                    {
                        if (!newIds.Contains(oldId))
                            await _dbManager.DeleteExamQuestionAsync(_exam.Id, oldId);
                    }

                    // Lưu exam + questions
                    bool success = await _dbManager.SaveExamWithQuestionsAsync(_exam, newQuestions);
                    if (!success) { BtnSave.IsEnabled = true; LoadingOverlay.Visibility = Visibility.Collapsed; return; }
                }

                CustomDialog.Show("✅ Đã lưu thay đổi thành công!", "Thành Công", DialogType.Success);
                NavigateBack();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"❌ Lỗi khi lưu: {ex.Message}", "Lỗi", DialogType.Error);
                BtnSave.IsEnabled = true;
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // ==================== NAVIGATION ====================
        private void BtnBack_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new ExamManagementView(_dbManager));
        }

        // ==================== HELPERS ====================
        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), Margin = new Thickness(0, 0, 0, 4)
        };

        private static Button MakeIconButton(string icon, string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            return new Button
            {
                Content = icon, FontSize = 14, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.Hand, Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 0, 0, 0)
            };
        }
    }
}
