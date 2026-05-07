using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace e_learning_app
{
    public partial class TeachingScheduleView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        public class ScheduleEvent
        {
            public int DayOfWeek { get; set; } // 1: Monday, ..., 7: Sunday
            public string Subject { get; set; }
            public string ClassName { get; set; }
            public string Room { get; set; }
            public string Time { get; set; }
            public string ColorHex { get; set; }
        }

        private List<ScheduleEvent> _currentSchedule = new();

        public TeachingScheduleView(DatabaseManager dbManager = null)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataFromFirebaseAsync();
            RenderTimetable();
        }

        private async Task LoadDataFromFirebaseAsync()
        {
            if (_dbManager == null) return;
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            try
            {
                var coursesSnap = await _dbManager.GetDb.Collection("Courses")
                    .WhereEqualTo("InstructorId", currentUser.Id)
                    .WhereEqualTo("IsActive", true)
                    .GetSnapshotAsync();

                _currentSchedule.Clear();

                foreach (var doc in coursesSnap.Documents)
                {
                    var c = doc.ConvertTo<Course>();
                    
                    int dayOfWeek = ConvertDayStringToNumber(c.DayOfWeek);
                    string timeStr = ConvertPeriodsToTime(c.StartPeriod, c.EndPeriod);
                    string colorHex = string.IsNullOrEmpty(c.AccentColor) ? "#DBEAFE" : c.AccentColor;

                    _currentSchedule.Add(new ScheduleEvent
                    {
                        DayOfWeek = dayOfWeek,
                        Subject = c.Title,
                        ClassName = c.ClassName,
                        Room = string.IsNullOrEmpty(c.Category) ? "Online" : c.Category,
                        Time = timeStr,
                        ColorHex = colorHex
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải lịch giảng dạy: {ex.Message}", "Lỗi");
            }
        }

        private int ConvertDayStringToNumber(string dayStr)
        {
            return dayStr switch
            {
                "Thứ 2" => 1,
                "Thứ 3" => 2,
                "Thứ 4" => 3,
                "Thứ 5" => 4,
                "Thứ 6" => 5,
                "Thứ 7" => 6,
                "Chủ nhật" => 7,
                _ => 1
            };
        }

        private string ConvertPeriodsToTime(int start, int end)
        {
            var periodStartTimes = new Dictionary<int, string>
            {
                {1, "07:30"}, {2, "08:15"}, {3, "09:00"}, {4, "10:00"}, {5, "10:45"},
                {6, "13:00"}, {7, "13:45"}, {8, "14:30"}, {9, "15:30"}, {10, "16:15"}
            };

            var periodEndTimes = new Dictionary<int, string>
            {
                {1, "08:15"}, {2, "09:00"}, {3, "09:45"}, {4, "10:45"}, {5, "11:30"},
                {6, "13:45"}, {7, "14:30"}, {8, "15:15"}, {9, "16:15"}, {10, "17:00"}
            };

            string startTime = periodStartTimes.ContainsKey(start) ? periodStartTimes[start] : "00:00";
            string endTime = periodEndTimes.ContainsKey(end) ? periodEndTimes[end] : "00:00";

            return $"{startTime} - {endTime}";
        }

        private void RenderTimetable()
        {
            // Clear current items
            CanvasMonday.Children.Clear();
            CanvasTuesday.Children.Clear();
            CanvasWednesday.Children.Clear();
            CanvasThursday.Children.Clear();
            CanvasFriday.Children.Clear();
            CanvasSaturday.Children.Clear();
            CanvasSunday.Children.Clear();
            GridLinesCanvas.Children.Clear();
            TimeLabelsCanvas.Children.Clear();

            // 1. Draw Time Axis and Grid Lines (07:00 to 17:00 -> 10 hours)
            for (int i = 0; i <= 10; i++)
            {
                double y = i * 60;
                
                // Draw Label
                var lbl = new TextBlock
                {
                    Text = $"{i + 7:D2}:00",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetTop(lbl, y - 8); // Offset to vertically center with line
                Canvas.SetRight(lbl, 10);
                TimeLabelsCanvas.Children.Add(lbl);

                // Draw Grid Line
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = 2000, // Arbitrarily large width
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                    StrokeThickness = 1
                };
                GridLinesCanvas.Children.Add(line);
            }

            var columns = new[] { CanvasMonday, CanvasTuesday, CanvasWednesday, CanvasThursday, CanvasFriday, CanvasSaturday, CanvasSunday };

            // 2. Draw Events
            foreach (var ev in _currentSchedule)
            {
                if (ev.DayOfWeek < 1 || ev.DayOfWeek > 7) continue;

                var card = BuildScheduleCard(ev, columns[ev.DayOfWeek - 1]);
                columns[ev.DayOfWeek - 1].Children.Add(card);
            }

            // 3. Draw Current Time Indicator
            var now = DateTime.Now;
            int currentMinutes = (now.Hour - 7) * 60 + now.Minute;
            
            if (currentMinutes >= 0 && currentMinutes <= 660)
            {
                CurrentTimeLine.Visibility = Visibility.Visible;
                CurrentTimeDot.Visibility = Visibility.Visible;
                Canvas.SetTop(CurrentTimeLine, currentMinutes);
                Canvas.SetTop(CurrentTimeDot, currentMinutes);
            }
            else
            {
                CurrentTimeLine.Visibility = Visibility.Collapsed;
                CurrentTimeDot.Visibility = Visibility.Collapsed;
            }
        }

        private UIElement BuildScheduleCard(ScheduleEvent ev, Canvas parentCanvas)
        {
            // Parse Time "07:30 - 09:00"
            double top = 0;
            double height = 60;
            try
            {
                var parts = ev.Time.Split('-');
                if (parts.Length == 2)
                {
                    var startParts = parts[0].Trim().Split(':');
                    var endParts = parts[1].Trim().Split(':');

                    int startHr = int.Parse(startParts[0]);
                    int startMin = int.Parse(startParts[1]);
                    int endHr = int.Parse(endParts[0]);
                    int endMin = int.Parse(endParts[1]);

                    top = (startHr - 7) * 60 + startMin;
                    height = (endHr - startHr) * 60 + endMin - startMin;
                }
            }
            catch { }

            var border = new Border
            {
                Background = GetColorWithFallback(ev.ColorHex, "#DBEAFE"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Height = height
            };

            // Bind width to parent canvas width
            border.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth") { Source = parentCanvas });
            Canvas.SetTop(border, top);

            // Add ToolTip
            border.ToolTip = new ToolTip 
            { 
                Content = $"Môn: {ev.Subject}\nLớp: {ev.ClassName}\nPhòng: {ev.Room}\nThời gian: {ev.Time}" 
            };

            var stack = new StackPanel();

            // Time
            var txtTime = new TextBlock
            {
                Text = ev.Time,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(txtTime);

            // Subject
            var txtSubject = new TextBlock
            {
                Text = ev.Subject,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2)
            };
            stack.Children.Add(txtSubject);

            // Details (Class & Room)
            var detailStack = new StackPanel { Orientation = Orientation.Horizontal };
            var txtClass = new TextBlock
            {
                Text = $"🎓 {ev.ClassName}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                Margin = new Thickness(0, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var txtRoom = new TextBlock
            {
                Text = $"📍 P.{ev.Room}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            detailStack.Children.Add(txtClass);
            detailStack.Children.Add(txtRoom);

            stack.Children.Add(detailStack);
            border.Child = stack;

            // Click event
            border.MouseLeftButtonUp += (s, e) =>
            {
                MessageBox.Show(
                    $"Thông tin lớp học:\n\nMôn: {ev.Subject}\nLớp: {ev.ClassName}\nPhòng: {ev.Room}\nGiờ học: {ev.Time}",
                    "Chi tiết Lịch Dạy", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            return border;
        }

        private void BtnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chức năng đang được phát triển...", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chức năng đang được phát triển...", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                pd.PrintVisual(this, "Thời khóa biểu giảng dạy");
            }
        }

        private Brush GetColorWithFallback(string hex, string defaultHex)
        {
            try { return (Brush)new BrushConverter().ConvertFromString(hex); }
            catch { return (Brush)new BrushConverter().ConvertFromString(defaultHex); }
        }
    }
}
