using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using e_learning_app;

namespace e_learning_app.Views
{
    public partial class MyClassesView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private List<Course> _allClasses = new();
        private string _filterMode = "all";
        private string _searchText = "";

        private string _userRole;

        public MyClassesView(DatabaseManager dbManager, string role = "Teacher")
        {
            _dbManager = dbManager;
            _userRole = role;
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_userRole == "Student" && typeof(MyClassesView).GetField("BtnCreateClass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null || FindName("BtnCreateClass") != null)
            {
                if (FindName("BtnCreateClass") is Button btnCreate)
                {
                    btnCreate.Visibility = Visibility.Collapsed;
                }
            }
            await LoadDataAsync();
            ApplyFilter();
        }

        // ─── Data Loading ─────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            if (_dbManager == null || _dbManager.GetDb == null)
            {
                MessageBox.Show("Lỗi: DatabaseManager chưa được khởi tạo hoặc chưa được truyền vào view!");
                return;
            }

            try
            {
                var snapshot = await _dbManager.GetDb.Collection("Courses").GetSnapshotAsync();

                _allClasses.Clear();

                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var course = doc.ConvertTo<Course>();

                        _allClasses.Add(course);
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
                bool statusMatch = _filterMode switch { "active" => c.IsActive, "ended" => !c.IsActive, _ => true };
                bool searchMatch = string.IsNullOrWhiteSpace(_searchText) ||
                                 c.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                                 c.ClassName.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
                return statusMatch && searchMatch;
            }).ToList();

            UpdateUI(filtered);
        }

        private void UpdateUI(List<Course> courses)
        {
            ClassesPanel.Children.Clear();
            TxtSubtitle.Text = _userRole == "Teacher" 
                ? $"Bạn đang phụ trách {_allClasses.Count(c => c.IsActive)} lớp học đang hoạt động."
                : $"Bạn đang tham gia {_allClasses.Count(c => c.IsActive)} lớp học đang hoạt động.";

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

        private void BtnEnterClass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string courseId)
            {
                // 1. Find the course object from our local list
                var selectedCourse = _allClasses.FirstOrDefault(c => c.Id == courseId);

                if (selectedCourse != null)
                {
                    if (_userRole == "Teacher")
                    {
                        var mainWin = Window.GetWindow(this) as MainWindow;
                        if (mainWin != null)
                        {
                            mainWin.MainContentArea.Content = new CourseDetailView(_dbManager, selectedCourse);
                        }
                    }
                    else if (_userRole == "Student")
                    {
                        var studentWin = Window.GetWindow(this) as StudentMainWindow;
                        if (studentWin != null)
                        {
                            studentWin.StudentContentArea.Content = new StudentCourseView(_dbManager, selectedCourse);
                        }
                    }
                }
            }
        }

        private void BtnAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) MessageBox.Show($"Điểm danh ID: {btn.Tag}");
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

            body.Children.Add(new TextBlock { Text = c.Title, FontSize = 17, FontWeight = FontWeights.Bold });
            body.Children.Add(new TextBlock { Text = $"Lớp {c.ClassName}  •  {c.Semester}", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 15) });

            var stats = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 15) };
            stats.Children.Add(CreateStat(c.StudentCount.ToString(), "Học sinh"));
            stats.Children.Add(CreateStat($"{c.AttendanceRate}%", "Đi học"));
            body.Children.Add(stats);
            body.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 15) });

            var btns = new System.Windows.Controls.Primitives.UniformGrid { Columns = _userRole == "Teacher" ? 2 : 1 };
            string enterClassText = _userRole == "Teacher" ? "Vào lớp" : "Vào học";
            btns.Children.Add(CreateBtn(enterClassText, accent, Brushes.White, c.Id, BtnEnterClass_Click));
            
            if (_userRole == "Teacher")
            {
                btns.Children.Add(CreateBtn("Điểm danh", new SolidColorBrush(Color.FromRgb(248, 250, 252)), Brushes.SlateGray, c.Id, BtnAttendance_Click));
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
            var btn = new Button { Content = t, Background = b, Foreground = f, Height = 36, Margin = new Thickness(4, 0, 4, 0), Tag = id, FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0) };
            btn.Click += h;
            btn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='10'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            return btn;
        }
    }
}