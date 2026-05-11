using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e_learning_app;

namespace e_learning_app
{
    public partial class CreateCoursesView : UserControl
    {
        private readonly DatabaseManager _dbManager;

        private string _selectedColor = "#3B82F6";
        private readonly List<string> _vibeColors = new() { "#3B82F6", "#8B5CF6", "#F59E0B", "#10B981", "#EC4899", "#64748B" };
        private bool _isInitialized = false;

        public CreateCoursesView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;

            PopulateYears();

            _isInitialized = true;
            UpdatePreview();

            CbDayOfWeek_SelectionChanged(CbDayOfWeek, null);
        }

        private void PopulateYears()
        {
            if (CbYear == null) return;

            DateTime pivotDate = new DateTime(2025, 8, 1);

            int startYear = DateTime.Now < pivotDate ? 2024 : 2025;

            CbYear.Items.Clear();

            for (int i = -1; i < 4; i++)
            {
                CbYear.Items.Add($"{startYear + i} - {startYear + i + 1}");
            }

            CbYear.SelectedIndex = 0;
        }

        private void ColorRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || PreviewHeader == null) return;

            if (sender is RadioButton rb && rb.Background is SolidColorBrush brush)
            {
                _selectedColor = $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                PreviewHeader.Background = brush;
            }
        }

        private void Sync_UI(object sender, TextChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        private void Sync_UI_Selection(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }

        private void UpdatePreview()
        {
            if (!_isInitialized || PreviewTitle == null || PreviewClass == null) return;

            PreviewTitle.Text = string.IsNullOrWhiteSpace(TxtTitle.Text) ? "Tên môn học" : TxtTitle.Text;
            PreviewEmoji.Text = string.IsNullOrWhiteSpace(TxtEmoji.Text) ? "📚" : TxtEmoji.Text;

            if (PreviewDesc != null)
                PreviewDesc.Text = string.IsNullOrWhiteSpace(TxtDescription.Text) ? "Mô tả tóm tắt môn học sẽ hiển thị ở đây..." : TxtDescription.Text;

            string sem = (CbSemester.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Học kỳ 1";
            string year = CbYear.SelectedItem?.ToString() ?? $"{DateTime.Now.Year} - {DateTime.Now.Year + 1}";
            string cls = string.IsNullOrWhiteSpace(TxtClass.Text) ? "Lớp học" : TxtClass.Text;

            string cat = string.IsNullOrWhiteSpace(TxtCategory.Text) ? "" : $" • {TxtCategory.Text}";

            PreviewClass.Text = $"{cls} • {sem} • {year}{cat}";
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text) ||
                string.IsNullOrWhiteSpace(TxtClass.Text) ||
                string.IsNullOrWhiteSpace(TxtCategory.Text) ||
                string.IsNullOrWhiteSpace(TxtDescription.Text))
            {
                CustomDialog.Show("Vui lòng điền đầy đủ tất cả các thông tin (Tên môn, Mô tả, Mã lớp, Chuyên ngành) trước khi tạo lớp!",
                                "Thiếu thông tin", DialogType.Warning);
                return;
            }

            BtnSubmit.IsEnabled = false;
            BtnSubmit.Content = "Đang xử lý...";

            string day = (CbDayOfWeek.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hình thức 2";
            int startP = 0, endP = 0;

            if (day != "Hình thức 2")
            {
                if (!int.TryParse(TxtStartPeriod.Text, out startP) || !int.TryParse(TxtEndPeriod.Text, out endP) ||
                    startP >= endP || !((startP >= 1 && endP <= 5) || (startP >= 6 && endP <= 10)))
                {
                    CustomDialog.Show("Tiết học không hợp lệ!\n- Tiết bắt đầu phải nhỏ hơn tiết kết thúc.\n- Cùng thuộc 1 buổi (Sáng: 1-5, Chiều: 6-10).",
                                    "Lỗi nhập liệu", DialogType.Warning);
                    BtnSubmit.IsEnabled = true;
                    BtnSubmit.Content = "Hoàn tất và Tạo lớp";
                    return;
                }
            }

            try
            {
                var course = new Course
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = TxtTitle.Text.Trim(),
                    Description = TxtDescription.Text.Trim(),
                    ClassName = TxtClass.Text.Trim(),

                    CourseType = (CbCourseType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Đại cương",
                    Category = TxtCategory.Text.Trim(),

                    DayOfWeek = day,
                    StartPeriod = startP,
                    EndPeriod = endP,

                    Semester = $"{(CbSemester.SelectedItem as ComboBoxItem).Content} - {CbYear.SelectedItem}",
                    Emoji = PreviewEmoji.Text,
                    AccentColor = _selectedColor,
                    InstructorId = _dbManager.GetCurrentUser()?.Id ?? "Unknown",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                bool success = await _dbManager.CreateCourseAsync(course);

                if (success)
                {
                    NavigateBack();
                }
                else
                {
                    throw new Exception("Firebase rejected the save operation.");
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi khi lưu vào Firebase: {ex.Message}", "Lỗi", DialogType.Error);
                BtnSubmit.IsEnabled = true;
                BtnSubmit.Content = "Hoàn tất và Tạo lớp";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => NavigateBack();
        private void BtnBack_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            var mainWin = Window.GetWindow(this) as MainWindow;
            if (mainWin != null)
            {
                string currentUserId = _dbManager.GetCurrentUser()?.Id;
                mainWin.MainContentArea.Content = new Views.MyClassesView(_dbManager, currentUserId);
            }
        }

        private void CbDayOfWeek_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || TxtStartPeriod == null || TxtEndPeriod == null) return;

            if (CbDayOfWeek.SelectedItem is ComboBoxItem item)
            {
                if (item.Content.ToString() == "Hình thức 2")
                {
                    TxtStartPeriod.Text = "0";
                    TxtEndPeriod.Text = "0";
                    TxtStartPeriod.IsEnabled = false;
                    TxtEndPeriod.IsEnabled = false;
                    TxtStartPeriod.Opacity = 0.5;
                    TxtEndPeriod.Opacity = 0.5;
                }
                else
                {
                    if (TxtStartPeriod.Text == "0") TxtStartPeriod.Text = "";
                    if (TxtEndPeriod.Text == "0") TxtEndPeriod.Text = "";
                    TxtStartPeriod.IsEnabled = true;
                    TxtEndPeriod.IsEnabled = true;
                    TxtStartPeriod.Opacity = 1;
                    TxtEndPeriod.Opacity = 1;
                }
            }
        }
    }
}