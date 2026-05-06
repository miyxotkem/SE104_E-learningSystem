using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Input;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class MyClassesView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly string _currentUserId;
        private List<Course> _allClasses = new();
        private Dictionary<string, string> _myRegistrations = new(); // Dùng để lưu trạng thái [CourseId] -> [Status]

        private string _filterMode = "all";
        private string _searchText = "";
        private bool _isInstructor = false;

        public MyClassesView(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
            InitializeComponent();
        }

        public MyClassesView(DatabaseManager dbManager, string currentUserId)
        {
            _dbManager = dbManager;
            _currentUserId = currentUserId;
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyRolePermissions();
            await LoadDataAsync();
            ApplyFilter();
        }

        private void ApplyRolePermissions()
        {
            var currentUser = _dbManager.GetCurrentUser();
            _isInstructor = (currentUser != null && currentUser.Role == "Instructor");

            if (_isInstructor)
            {
                BtnCreateClass.Visibility = Visibility.Visible;
                BtnShowRegister.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnCreateClass.Visibility = Visibility.Collapsed;
                BtnShowRegister.Visibility = Visibility.Visible;
            }
        }

        // ─── Data Loading ─────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            if (_dbManager == null || _dbManager.GetDb == null) return;

            try
            {
                // 1. Tải toàn bộ khóa học
                var snapshot = await _dbManager.GetDb.Collection("Courses").GetSnapshotAsync();
                _allClasses.Clear();

                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var course = doc.ConvertTo<Course>();
                        course.Id = doc.Id;
                        _allClasses.Add(course);
                    }
                }

                // 2. Nếu là Sinh viên, tải toàn bộ dữ liệu đăng ký của người này
                if (!_isInstructor)
                {
                    var regSnap = await _dbManager.GetDb.Collection("courseRegistrations")
                        .WhereEqualTo("userId", _dbManager.GetCurrentUser().Id)
                        .GetSnapshotAsync();

                    _myRegistrations.Clear();
                    foreach (var doc in regSnap.Documents)
                    {
                        string courseId = doc.GetValue<string>("courseId");
                        string status = doc.GetValue<string>("status");
                        _myRegistrations[courseId] = status;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi Tải Dữ Liệu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Filtering & Rendering ────────────────────────────────────
        private void ApplyFilter()
        {
            var filtered = _allClasses.Where(c =>
            {
                // Lọc theo trạng thái Đang học / Kết thúc
                bool statusMatch = _filterMode switch { "active" => c.IsActive, "ended" => !c.IsActive, _ => true };

                // Lọc theo thanh tìm kiếm
                bool searchMatch = string.IsNullOrWhiteSpace(_searchText) ||
                                 c.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                                 c.ClassName.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

                // Lọc theo Phân quyền (QUAN TRỌNG)
                bool roleMatch = false;
                if (_isInstructor)
                {
                    // Giảng viên chỉ nhìn thấy lớp của mình tạo ra
                    roleMatch = c.InstructorId == _dbManager.GetCurrentUser().Id;
                }
                else
                {
                    // Sinh viên chỉ nhìn thấy lớp đã đăng ký (Pending hoặc Accepted)
                    roleMatch = _myRegistrations.TryGetValue(c.Id, out string status) && status != "rejected";
                }

                return statusMatch && searchMatch && roleMatch;
            }).ToList();

            UpdateUI(filtered);
        }

        private void UpdateUI(List<Course> courses)
        {
            ClassesPanel.Children.Clear();

            // Cập nhật Subtitle cho chuẩn
            int activeCount = courses.Count(c => c.IsActive);
            TxtSubtitle.Text = _isInstructor
                ? $"Bạn đang phụ trách {activeCount} lớp học đang hoạt động."
                : $"Bạn đang tham gia {activeCount} lớp học.";

            foreach (var course in courses)
                ClassesPanel.Children.Add(CreateCourseCard(course));

            EmptyState.Visibility = courses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClassesPanel.Visibility = courses.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ─── Search & UI Handlers ─────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = TxtSearch.Text;
            if (TxtSearchPlaceholder != null) TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Visible : Visibility.Collapsed;
            if (BtnClearSearch != null) BtnClearSearch.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Collapsed : Visibility.Visible;
            ApplyFilter();
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e) => TxtSearchPlaceholder.Visibility = Visibility.Collapsed;

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSearch.Text)) TxtSearchPlaceholder.Visibility = Visibility.Visible;
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtSearch.Focus();
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton == null) return;

            ResetFilterButtons();
            clickedButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
            clickedButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            clickedButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFDBFE"));

            _filterMode = clickedButton.Tag.ToString();
            ApplyFilter();
        }

        private void ResetFilterButtons()
        {
            var inactiveBg = Brushes.White;
            var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));

            Button[] filterButtons = { BtnFilterAll, BtnFilterActive, BtnFilterEnded };
            foreach (var btn in filterButtons)
            {
                btn.Background = inactiveBg;
                btn.Foreground = inactiveFg;
                btn.BorderBrush = inactiveBorder;
            }
        }

        private async void BtnCreateClass_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = Window.GetWindow(this) as MainWindow;
            if (mainWin != null)
            {
                mainWin.MainContentArea.Content = new CreateCoursesView(_dbManager);
            }
        }

        private async void BtnEnterClass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string courseId)
            {
                var selectedCourse = _allClasses.FirstOrDefault(c => c.Id == courseId);
                if (selectedCourse == null) return;

                var mainWin = Window.GetWindow(this) as MainWindow;
                if (mainWin != null)
                {
                    mainWin.MainContentArea.Content = new CourseDetailView(_dbManager, selectedCourse);
                }
            }
        }

        // =========================================================
        // LOGIC ĐĂNG KÝ MỚI & HỦY YÊU CẦU (SINH VIÊN)
        // =========================================================
        private void BtnShowRegister_Click(object sender, RoutedEventArgs e)
        {
            PopulateUnregisteredList();
            RegisterOverlay.Visibility = Visibility.Visible;
            if (MainScrollViewer != null) MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
        }

        private void CloseRegisterOverlay_Click(object sender, RoutedEventArgs e)
        {
            RegisterOverlay.Visibility = Visibility.Collapsed;
            if (MainScrollViewer != null) MainScrollViewer.Effect = null;
        }

        private void PopulateUnregisteredList()
        {
            // Lấy danh sách các lớp học MÀ Sinh viên KHÔNG là Giảng viên, VÀ chưa đăng ký (hoặc đã bị từ chối)
            var unregistered = _allClasses.Where(c =>
                c.InstructorId != _dbManager.GetCurrentUser().Id &&
                (!_myRegistrations.ContainsKey(c.Id) || _myRegistrations[c.Id] == "rejected")
            ).ToList();

            UnregisteredList.ItemsSource = unregistered;
            TxtNoCoursesAvailable.Visibility = unregistered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnSubmitRegistration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string courseId)
            {
                var currentUser = _dbManager.GetCurrentUser();
                if (currentUser == null) return;

                btn.IsEnabled = false;
                btn.Content = "Đang gửi...";

                try
                {
                    string registrationId = $"{currentUser.Id}_{courseId}";
                    var registrationRef = _dbManager.GetDb.Collection("courseRegistrations").Document(registrationId);

                    var registrationData = new
                    {
                        userId = currentUser.Id,
                        courseId = courseId,
                        status = "pending",
                        requestDate = Google.Cloud.Firestore.FieldValue.ServerTimestamp,
                        approvedDate = (DateTime?)null,
                        progressPercentage = 0.0
                    };

                    await registrationRef.SetAsync(registrationData);

                    MessageBox.Show("Đã gửi yêu cầu đăng ký thành công! Vui lòng chờ giảng viên duyệt.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 1. Cập nhật lại danh sách cache cục bộ
                    _myRegistrations[courseId] = "pending";

                    // 2. Làm mới danh sách trong Popup chưa đăng ký
                    PopulateUnregisteredList();

                    // 3. Render lại danh sách ở màn hình chính
                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi đăng ký: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    btn.IsEnabled = true;
                    btn.Content = "Gửi yêu cầu";
                }
            }
        }

        private async void BtnCancelRegistration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string courseId)
            {
                var course = _allClasses.FirstOrDefault(c => c.Id == courseId);
                string courseName = course != null ? course.Title : "khóa học này";

                // 1. Hiện thông báo xác nhận
                var result = MessageBox.Show($"Bạn có chắc chắn muốn hủy yêu cầu đăng ký lớp \"{courseName}\" không?",
                    "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        btn.IsEnabled = false;
                        btn.Content = "Đang hủy...";

                        // 2. Tìm ID và xóa document trên Firebase
                        string regId = $"{_dbManager.GetCurrentUser().Id}_{courseId}";
                        await _dbManager.GetDb.Collection("courseRegistrations").Document(regId).DeleteAsync();

                        // 3. Cập nhật giao diện
                        _myRegistrations.Remove(courseId);
                        MessageBox.Show("Đã hủy yêu cầu đăng ký thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                        ApplyFilter(); // Cập nhật lại màn hình chính
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi hủy: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        btn.IsEnabled = true;
                        btn.Content = "Đang chờ duyệt - Nhấn để hủy";
                    }
                }
            }
        }


        // ─── UI Factory ───────────────────────────────────────────────
        private Border CreateCourseCard(Course c)
        {
            var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(c.AccentColor) ?? Brushes.SlateBlue;
            var card = new Border
            {
                Width = 310,
                Margin = new Thickness(0, 0, 16, 16),
                CornerRadius = new CornerRadius(20),
                Background = Brushes.White,
                Effect = new DropShadowEffect { BlurRadius = 15, Opacity = 0.07 }
            };

            var stack = new StackPanel();
            card.Child = stack;

            stack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(20, 20, 0, 0),
                Height = 95,
                Background = accent,
                Child = new Grid
                {
                    Margin = new Thickness(20, 12, 20, 12),
                    Children = {
                        new Border {
                            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                            Padding = new Thickness(9, 4, 9, 4), CornerRadius = new CornerRadius(12),
                            Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                            Child = new TextBlock { Text = c.IsActive ? "Đang học" : "Kết thúc", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
                        },
                        new TextBlock { Text = c.Emoji, FontSize = 34, VerticalAlignment = VerticalAlignment.Bottom }
                    }
                }
            });

            var bodyBorder = new Border { Padding = new Thickness(20, 16, 20, 16) };
            var body = new StackPanel();
            bodyBorder.Child = body;

            body.Children.Add(new TextBlock { Text = c.Title, FontSize = 17, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(new TextBlock { Text = $"Lớp {c.ClassName}  •  {c.Semester}", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 15) });

            var stats = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 15) };
            stats.Children.Add(CreateStat(c.StudentCount.ToString(), "Học sinh"));
            stats.Children.Add(CreateStat(c.AssignmentCount.ToString(), "Bài tập"));
            body.Children.Add(stats);
            body.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 15) });

            // Layout Card luôn là 1 cột cho nút bấm
            var btns = new System.Windows.Controls.Primitives.UniformGrid { Columns = 1 };

            if (_isInstructor || (_myRegistrations.TryGetValue(c.Id, out string statusAcc) && statusAcc == "accepted"))
            {
                btns.Children.Add(CreateBtn("Vào lớp", accent, Brushes.White, c.Id, BtnEnterClass_Click));
            }
            else if (_myRegistrations.TryGetValue(c.Id, out string statusPen) && statusPen == "pending")
            {
                // Nút Hủy đăng ký với màu vàng nhạt và chữ cam
                var btnCancel = CreateBtn("Đang chờ duyệt - Nhấn để hủy",
                    new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                    new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                    c.Id, BtnCancelRegistration_Click);

                btnCancel.ToolTip = "Nhấn để hủy yêu cầu tham gia lớp này";
                btns.Children.Add(btnCancel);
            }

            body.Children.Add(btns);
            stack.Children.Add(bodyBorder);

            return card;
        }

        private UIElement CreateStat(string v, string l)
        {
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = v, FontSize = 16, FontWeight = FontWeights.Bold });
            s.Children.Add(new TextBlock { Text = l, FontSize = 10, Foreground = Brushes.Gray });
            return s;
        }

        private Button CreateBtn(string t, Brush b, Brush f, string id, RoutedEventHandler h)
        {
            var btn = new Button { Content = t, Background = b, Foreground = f, Height = 36, Margin = new Thickness(4, 0, 4, 0), Tag = id, FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            if (h != null) btn.Click += h;
            btn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='10'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            return btn;
        }
    }
}