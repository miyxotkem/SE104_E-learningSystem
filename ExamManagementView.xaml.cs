using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class ExamManagementView : UserControl
    {
        // ==================== FIELDS ====================
        private List<Exam> _allExams = new();
        private HashSet<string> _statusFilters = new();
        private HashSet<string> _typeFilters = new();
        private string _searchText = "";
        private string _currentClassId = "";

        // Color Dictionary for Exam Types
        private static readonly Dictionary<ExamType, (string bg, string fg, string icon)> TypeColors = new()
        {
            { ExamType.Quiz, ("#EFF6FF", "#3B82F6", "🎯") },
            { ExamType.Midterm, ("#FEF3C7", "#D97706", "📚") },
            { ExamType.Final, ("#F0F9FF", "#0369A1", "🏆") },
            { ExamType.Practice, ("#F0FDF4", "#16A34A", "💪") },
            { ExamType.Assignment, ("#FCE7F3", "#EC4899", "📋") }
        };

        // ==================== CONSTRUCTOR ====================
        private DatabaseManager _dbManager;
        public ExamManagementView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        // ==================== LIFECYCLE ====================
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeFilters();
            await LoadDataFromFirebaseAsync();
        }

        private void InitializeFilters()
        {
            _statusFilters.Clear();
            _typeFilters.Clear();

            // All unchecked by default
            ChkActive.IsChecked = false;
            ChkClosed.IsChecked = false;
            ChkDraft.IsChecked = false;
            ChkQuiz.IsChecked = false;
            ChkMidterm.IsChecked = false;
            ChkFinal.IsChecked = false;
            ChkPractice.IsChecked = false;
        }

        // ==================== DATA LOADING ====================
        private async System.Threading.Tasks.Task LoadDataFromFirebaseAsync()
        {
            if (_dbManager == null || _dbManager.GetDb == null) return;

            try
            {
                var query = _dbManager.GetDb.Collection("exams");
                var snapshot = await query.GetSnapshotAsync();

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
                MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}");
            }

            Refresh();
        }

        // ==================== FILTERING & RENDERING ====================
        private IEnumerable<Exam> GetFilteredExams()
        {
            var filtered = _allExams.AsEnumerable();

            // Status filter
            if (_statusFilters.Count > 0)
            {
                filtered = filtered.Where(e =>
                {
                    if (_statusFilters.Contains("active") && e.IsActive) return true;
                    if (_statusFilters.Contains("closed") && !e.IsActive && e.IsPublished) return true;
                    if (_statusFilters.Contains("draft") && !e.IsPublished) return true;
                    return false;
                });
            }

            // Type filter
            if (_typeFilters.Count > 0)
                filtered = filtered.Where(e => _typeFilters.Contains(e.Type.ToString()));

            // Search filter
            if (!string.IsNullOrWhiteSpace(_searchText))
                filtered = filtered.Where(e =>
                    e.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            return filtered;
        }

        private void Refresh()
        {
            var filtered = GetFilteredExams().ToList();

            // Update statistics
            UpdateStats();

            // Render exams
            ExamsPanel.Children.Clear();
            if (filtered.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                foreach (var exam in filtered)
                {
                    var examCard = BuildExamCard(exam);
                    ExamsPanel.Children.Add(examCard);
                }
            }
        }

        private void UpdateStats()
        {
            TxtTotalExams.Text = _allExams.Count.ToString();
            TxtActiveExams.Text = _allExams.Count(e => e.IsActive).ToString();
            TxtPendingExams.Text = _allExams.Count(e => !e.IsPublished).ToString();
        }

        private Border BuildExamCard(Exam exam)
        {
            // Get color based on exam type
            var (bgColor, fgColor, examIcon) = TypeColors.GetValueOrDefault(exam.Type, ("#F1F5F9", "#64748B", "📋"));

            // Main card border
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // ==================== LEFT CONTENT ====================
            var left = new StackPanel();

            // Tags Row
            var tagsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            // Type Tag
            tagsPanel.Children.Add(MakePill(bgColor, fgColor, $"{examIcon} {exam.Type}", 0));

            // Status Tag
            var statusColor = exam.IsActive ? ("#DCFCE7", "#16A34A") :
                            exam.IsPublished ? ("#FEE2E2", "#DC2626") :
                            ("#FEF3C7", "#D97706");
            var statusText = exam.IsActive ? "✅ Đang Mở" :
                           exam.IsPublished ? "🔒 Đã Khóa" :
                           "📝 Nháp";
            tagsPanel.Children.Add(MakePill(statusColor.Item1, statusColor.Item2, statusText, 8));

            // Time Tag
            tagsPanel.Children.Add(MakePill("#F1F5F9", "#64748B", $"⏱️ {exam.TimeLimitMinutes} phút", 8));

            left.Children.Add(tagsPanel);

            // Title
            left.Children.Add(new TextBlock
            {
                Text = exam.Title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Description
            left.Children.Add(new TextBlock
            {
                Text = exam.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Meta information
            var metaPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var metaInfo = new List<string>
            {
                $"📊 {exam.TotalQuestions} câu",
                $"🎯 Điểm qua: {exam.PassingScore}%",
                $"📅 {exam.CreatedAt:dd/MM/yyyy}"
            };

            for (int i = 0; i < metaInfo.Count; i++)
            {
                if (i > 0)
                {
                    metaPanel.Children.Add(new TextBlock
                    {
                        Text = "  •  ",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                        FontSize = 11
                    });
                }

                metaPanel.Children.Add(new TextBlock
                {
                    Text = metaInfo[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
                });
            }

            left.Children.Add(metaPanel);

            Grid.SetColumn(left, 0);

            // ==================== RIGHT BUTTONS ====================
            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };

            var btnView = CreateActionButton("👁️ Xem", "#F8FAFC", exam.Id);
            btnView.Click += (s, e) => ViewExam(exam);

            var btnEdit = CreateActionButton("✏️ Sửa", "#F8FAFC", exam.Id);
            btnEdit.Margin = new Thickness(6, 0, 0, 0);
            btnEdit.Click += (s, e) => EditExam(exam);

            var btnDelete = CreateActionButton("🗑️ Xóa", "#FFF1F2", exam.Id);
            btnDelete.Margin = new Thickness(6, 0, 0, 0);
            btnDelete.Click += (s, e) => DeleteExam(exam);

            buttonStack.Children.Add(btnView);
            buttonStack.Children.Add(btnEdit);
            buttonStack.Children.Add(btnDelete);

            Grid.SetColumn(buttonStack, 1);

            root.Children.Add(left);
            root.Children.Add(buttonStack);

            card.Child = root;
            return card;
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Tạo pill/badge hiển thị tag
        /// </summary>
        private static Border MakePill(string bgHex, string fgHex, string text, double leftMargin = 0)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(10, 6, 10, 6),
                Background = (Brush)new BrushConverter().ConvertFromString(bgHex),
                Margin = new Thickness(leftMargin, 0, 0, 0)
            };

            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFromString(fgHex)
            };

            return border;
        }

        /// <summary>
        /// Tạo button action (View, Edit, Delete)
        /// </summary>
        private static Button CreateActionButton(string text, string bgHex, string id)
        {
            var btn = new Button
            {
                Content = text,
                Background = (Brush)new BrushConverter().ConvertFromString(bgHex),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };

            // Custom button template
            var controlTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(0));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            controlTemplate.VisualTree = borderFactory;
            btn.Template = controlTemplate;

            return btn;
        }

        /// <summary>
        /// Tạo TextBlock label cho dialog
        /// </summary>
        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        /// <summary>
        /// Tạo TextBox input
        /// </summary>
        private static TextBox CreateTextBox(string text = "")
        {
            return new TextBox
            {
                Text = text,
                Height = 40,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Tạo ComboBox
        /// </summary>
        private static ComboBox CreateComboBox(string[] items)
        {
            return new ComboBox
            {
                ItemsSource = items,
                Height = 40,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1)
            };
        }

        // ==================== EVENT HANDLERS ====================

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkbox) return;

            string tag = checkbox.Tag?.ToString() ?? "";

            // Status filters
            if (tag == "active" || tag == "closed" || tag == "draft")
            {
                if (checkbox.IsChecked == true)
                    _statusFilters.Add(tag);
                else
                    _statusFilters.Remove(tag);
            }
            // Type filters
            else if (!string.IsNullOrEmpty(tag))
            {
                if (checkbox.IsChecked == true)
                    _typeFilters.Add(tag);
                else
                    _typeFilters.Remove(tag);
            }

            Refresh();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = TxtSearch.Text;
            Refresh();
        }

        private void BtnCreateExam_Click(object sender, RoutedEventArgs e)
        {
            ShowCreateExamDialog();
        }

        private void ViewExam(Exam exam)
        {
            MessageBox.Show(
                $"Xem bài thi: {exam.Title}\n\n" +
                $"📊 {exam.TotalQuestions} câu hỏi\n" +
                $"⏱️ {exam.TimeLimitMinutes} phút\n" +
                $"🎯 Điểm qua: {exam.PassingScore}%",
                "Chi Tiết Bài Thi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditExam(Exam exam)
        {
            ShowEditExamDialog(exam);
        }

        private async void DeleteExam(Exam exam)
        {
            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa bài thi:\n\n\"{exam.Title}\"?\n\nHành động này không thể hoàn tác.",
                "Xác Nhận Xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (_dbManager != null)
                    {
                        var docRef = _dbManager.GetDb.Collection("exams").Document(exam.Id);
                        await docRef.DeleteAsync();
                    }
                    _allExams.Remove(exam);
                    Refresh();
                    MessageBox.Show("✅ Xóa bài thi thành công!", "Thành Công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xóa dữ liệu: {ex.Message}");
                }
            }
        }

        // ==================== DIALOGS ====================

        /// <summary>
        /// Dialog tạo bài thi mới
        /// </summary>
        private void ShowCreateExamDialog()
        {
            var dialogWindow = new Window
            {
                Title = "➕ Tạo Bài Thi Mới",
                Width = 600,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC))
            };

            var mainStack = new StackPanel { Margin = new Thickness(28) };

            // Title
            mainStack.Children.Add(CreateLabel("📝 Tên Bài Thi:"));
            var txtTitle = CreateTextBox();
            mainStack.Children.Add(txtTitle);

            // Description
            mainStack.Children.Add(CreateLabel("📄 Mô Tả Chi Tiết:"));
            var txtDescription = new TextBox
            {
                Height = 80,
                Padding = new Thickness(12, 10, 12, 10),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Margin = new Thickness(0, 0, 0, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            mainStack.Children.Add(txtDescription);

            // Config
            mainStack.Children.Add(CreateLabel("⚙️ Cấu Hình:"));

            var configGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < 5; i++)
            {
                if (i % 2 == 0)
                    configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                else
                    configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            }

            var cbExamType = CreateComboBox(new[] { "Quiz", "Midterm", "Final", "Practice", "Assignment" });
            cbExamType.SelectedIndex = 0;
            Grid.SetColumn(cbExamType, 0);

            var cbTimeLimit = CreateComboBox(new[] { "15", "30", "45", "60", "90", "120" });
            cbTimeLimit.SelectedIndex = 3;
            Grid.SetColumn(cbTimeLimit, 2);

            var cbPassingScore = CreateComboBox(new[] { "40%", "50%", "60%", "70%", "80%" });
            cbPassingScore.SelectedIndex = 1;
            Grid.SetColumn(cbPassingScore, 4);

            configGrid.Children.Add(cbExamType);
            configGrid.Children.Add(cbTimeLimit);
            configGrid.Children.Add(cbPassingScore);
            mainStack.Children.Add(configGrid);

            // Labels
            var labelGrid = new Grid();
            for (int i = 0; i < 5; i++)
            {
                if (i % 2 == 0)
                    labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                else
                    labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            }

            var lbl1 = new TextBlock
            {
                Text = "Loại Bài Thi",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
            };
            Grid.SetColumn(lbl1, 0);

            var lbl2 = new TextBlock
            {
                Text = "Thời Gian (phút)",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
            };
            Grid.SetColumn(lbl2, 2);

            var lbl3 = new TextBlock
            {
                Text = "Điểm Qua",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
            };
            Grid.SetColumn(lbl3, 4);

            labelGrid.Children.Add(lbl1);
            labelGrid.Children.Add(lbl2);
            labelGrid.Children.Add(lbl3);
            mainStack.Children.Add(labelGrid);

            // Buttons
            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 28, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "❌ Hủy",
                Width = 100,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            btnCancel.Click += (s, e) => dialogWindow.Close();

            var btnCreate = new Button
            {
                Content = "✅ Tạo Bài Thi",
                Width = 130,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };

            btnCreate.Click += async (s, e) =>
            {
                // Validation
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    MessageBox.Show("⚠️ Vui lòng nhập tên bài thi!", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create new exam
                var newExam = new Exam
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassId = _currentClassId,
                    Title = txtTitle.Text,
                    Description = txtDescription.Text,
                    Type = Enum.Parse<ExamType>(cbExamType.SelectedItem?.ToString() ?? "Quiz"),
                    TimeLimitMinutes = int.Parse(cbTimeLimit.SelectedItem?.ToString() ?? "60"),
                    PassingScore = double.Parse(cbPassingScore.SelectedItem?.ToString()?.TrimEnd('%') ?? "50"),
                    IsActive = true,
                    IsPublished = true, // We auto-publish as we're testing or the teacher can decide, currently ExamManagement creates with IsPublished=false.
                    TotalQuestions = 0,
                    QuestionIds = new List<string>()
                };

                try
                {
                    if (_dbManager != null)
                    {
                        var docRef = _dbManager.GetDb.Collection("exams").Document(newExam.Id);
                        await docRef.SetAsync(newExam);
                    }

                    _allExams.Add(newExam);
                    Refresh();
                    dialogWindow.Close();

                    MessageBox.Show($"✅ Tạo bài thi \"{newExam.Title}\" thành công!", "Thành Công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tạo bài thi: {ex.Message}");
                }
            };

            buttonRow.Children.Add(btnCancel);
            buttonRow.Children.Add(btnCreate);
            mainStack.Children.Add(buttonRow);

            dialogWindow.Content = mainStack;
            dialogWindow.ShowDialog();
        }

        /// <summary>
        /// Dialog chỉnh sửa bài thi
        /// </summary>
        private void ShowEditExamDialog(Exam exam)
        {
            var dialogWindow = new Window
            {
                Title = "✏️ Chỉnh Sửa Bài Thi",
                Width = 600,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC))
            };

            var mainStack = new StackPanel { Margin = new Thickness(28) };

            // Title
            mainStack.Children.Add(CreateLabel("📝 Tên Bài Thi:"));
            var txtTitle = CreateTextBox(exam.Title);
            mainStack.Children.Add(txtTitle);

            // Description
            mainStack.Children.Add(CreateLabel("📄 Mô Tả Chi Tiết:"));
            var txtDescription = new TextBox
            {
                Height = 80,
                Text = exam.Description,
                Padding = new Thickness(12, 10, 12, 10),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Margin = new Thickness(0, 0, 0, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            mainStack.Children.Add(txtDescription);

            // Config
            mainStack.Children.Add(CreateLabel("⚙️ Cấu Hình:"));

            var configGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < 5; i++)
            {
                if (i % 2 == 0)
                    configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                else
                    configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            }

            var cbExamType = CreateComboBox(new[] { "Quiz", "Midterm", "Final", "Practice", "Assignment" });
            cbExamType.SelectedItem = exam.Type.ToString();
            Grid.SetColumn(cbExamType, 0);

            var cbTimeLimit = CreateComboBox(new[] { "15", "30", "45", "60", "90", "120" });
            cbTimeLimit.SelectedItem = exam.TimeLimitMinutes.ToString();
            Grid.SetColumn(cbTimeLimit, 2);

            var cbPassingScore = CreateComboBox(new[] { "40%", "50%", "60%", "70%", "80%" });
            cbPassingScore.SelectedItem = $"{(int)exam.PassingScore}%";
            Grid.SetColumn(cbPassingScore, 4);

            configGrid.Children.Add(cbExamType);
            configGrid.Children.Add(cbTimeLimit);
            configGrid.Children.Add(cbPassingScore);
            mainStack.Children.Add(configGrid);

            // Status
            var chkPublished = new CheckBox
            {
                Content = "✅ Công Bố Bài Thi",
                IsChecked = exam.IsPublished,
                Margin = new Thickness(0, 0, 0, 14),
                FontSize = 12
            };
            mainStack.Children.Add(chkPublished);

            // Buttons
            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 28, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "❌ Hủy",
                Width = 100,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            btnCancel.Click += (s, e) => dialogWindow.Close();

            var btnSave = new Button
            {
                Content = "💾 Lưu Thay Đổi",
                Width = 130,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };

            btnSave.Click += async (s, e) =>
            {
                // Update exam properties
                exam.Title = txtTitle.Text;
                exam.Description = txtDescription.Text;
                exam.Type = Enum.Parse<ExamType>(cbExamType.SelectedItem?.ToString() ?? "Quiz");
                exam.TimeLimitMinutes = int.Parse(cbTimeLimit.SelectedItem?.ToString() ?? "60");
                exam.PassingScore = double.Parse(cbPassingScore.SelectedItem?.ToString()?.TrimEnd('%') ?? "50");
                exam.IsPublished = chkPublished.IsChecked ?? false;
                exam.UpdatedAt = DateTime.UtcNow;

                try
                {
                    if (_dbManager != null)
                    {
                        var docRef = _dbManager.GetDb.Collection("exams").Document(exam.Id);
                        await docRef.SetAsync(exam);
                    }

                    Refresh();
                    dialogWindow.Close();

                    MessageBox.Show("✅ Cập nhật bài thi thành công!", "Thành Công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi cập nhật bài thi: {ex.Message}");
                }
            };

            buttonRow.Children.Add(btnCancel);
            buttonRow.Children.Add(btnSave);
            mainStack.Children.Add(buttonRow);

            dialogWindow.Content = mainStack;
            dialogWindow.ShowDialog();
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Set class ID cho view này
        /// </summary>
        public void SetClassId(string classId)
        {
            _currentClassId = classId;
        }

        /// <summary>
        /// Refresh dữ liệu từ Firebase
        /// </summary>
        public async void RefreshData()
        {
            await LoadDataFromFirebaseAsync();
        }
    }
}