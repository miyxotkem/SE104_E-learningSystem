using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class StudentCourseView : UserControl
    {
        private DatabaseManager _dbManager;
        private Course _course;
        private Lesson _currentLesson;
        private DispatcherTimer _timer;
        private bool _isDraggingSlider = false;
        private bool _isPlaying = true;
        private bool _isMuted = false;
        private Window _fullScreenWindow;
        private TimeSpan _savedPosition;
        private bool _wasPlaying;
        private bool _isTransitioningFullscreen = false;



        public StudentCourseView()
        {
            InitializeComponent();
        }

        public  StudentCourseView(DatabaseManager dbManager, Course course, Lesson currentLesson)
        {

            InitializeComponent();
            _dbManager = dbManager;
            _course = course;
            _currentLesson = currentLesson;
            if (_course != null)
            {
                TxtCourseTitle.Text = _currentLesson != null ? _currentLesson.Title : _course.Title;
                TxtCourseSubtitle.Text = $"Khóa học: {_course.ClassName}";
            }

            SetupVideoPlayer();

            SetupLessonsListener();
            SetupCommentsListener();

            this.Unloaded += (s, e) =>
            {
                
                
                if (_timer != null) _timer.Stop();
            };
        }
        private void SetupVideoPlayer()
        {
            if (_currentLesson != null && !string.IsNullOrEmpty(_currentLesson.VideoUrl))
            {
                LessonVideoPlayer.Source = new Uri(_currentLesson.VideoUrl);
                LessonVideoPlayer.Play();
                _isPlaying = true;
                BtnPlayPause.Text = "⏸";

                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += Timer_Tick;
                _timer.Start();

                LessonVideoPlayer.MediaOpened += LessonVideoPlayer_MediaOpened;
                LessonVideoPlayer.MediaEnded += (s, e) => {
                    _isPlaying = false;
                    BtnPlayPause.Text = "▶";
                    ProgressSlider.Value = ProgressSlider.Maximum;
                    UpdateOverlayPlayButton();
                };
            }
        }

        private void LessonVideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (LessonVideoPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Maximum = LessonVideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }

            if (_isTransitioningFullscreen)
            {
                LessonVideoPlayer.Position = _savedPosition;
                if (_wasPlaying)
                {
                    LessonVideoPlayer.Play();
                    BtnPlayPause.Text = "⏸";
                    _isPlaying = true;
                }
                else
                {
                    LessonVideoPlayer.Pause();
                    BtnPlayPause.Text = "▶";
                    _isPlaying = false;
                }
                _isTransitioningFullscreen = false;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isDraggingSlider && LessonVideoPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = LessonVideoPlayer.Position.TotalSeconds;
                TxtTime.Text = $"{LessonVideoPlayer.Position.ToString(@"mm\:ss")} / {LessonVideoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss")}";
            }
        }

        private async void SetupLessonsListener()
        {
            if (_course == null) return;
            try
            {
                var response = await ApiService.GetAsync<List<LessonResponse>>($"courses/{_course.Id}/lessons");
                if (response != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var lessons = response.Select(r => {
                            var l = r.Data;
                            if (l != null) l.Id = r.Id;
                            return l;
                        }).Where(l => l != null).OrderBy(l => l.CreatedAt).ToList();

                        PlaylistItems.Items.Clear();
                        int index = 1;
                        foreach (var lesson in lessons)
                        {
                            var border = new Border
                            {
                                Background = (lesson.Id == _currentLesson?.Id) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E7FF")) : Brushes.Transparent,
                                CornerRadius = new CornerRadius(8),
                                Padding = new Thickness(10),
                                Margin = new Thickness(0, 0, 0, 5),
                                Cursor = System.Windows.Input.Cursors.Hand
                            };

                            border.MouseLeftButtonDown += (s, e) =>
                            {
                                var mainWin = Window.GetWindow(this) as MainWindow;
                                if (mainWin != null)
                                {
                                    mainWin.MainContentArea.Content = new StudentCourseView(_dbManager, _course, lesson);
                                    return;
                                }

                                var studentWin = Window.GetWindow(this) as StudentMainWindow;
                                if (studentWin != null)
                                {
                                    studentWin.StudentContentArea.Content = new StudentCourseView(_dbManager, _course, lesson);
                                }
                            };

                            var grid = new Grid();
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var txtIndex = new TextBlock
                            {
                                Text = index.ToString(),
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                                FontWeight = FontWeights.Bold,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center
                            };
                            Grid.SetColumn(txtIndex, 0);

                            var txtTitle = new TextBlock
                            {
                                Text = lesson.Title,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                                FontWeight = FontWeights.SemiBold,
                                TextWrapping = TextWrapping.Wrap,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(txtTitle, 1);

                            grid.Children.Add(txtIndex);
                            grid.Children.Add(txtTitle);

                            border.Child = grid;
                            PlaylistItems.Items.Add(border);
                            index++;
                        }
                    });
                }
            }
            catch (Exception ex) { }
        }

        private void BtnBack_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Stop video
            if (_timer != null) _timer.Stop();
            LessonVideoPlayer.Stop();

            var mainWin = Window.GetWindow(this) as MainWindow;
            if (mainWin != null)
            {
                mainWin.MainContentArea.Content = new CourseDetailView(_dbManager, _course);
                return;
            }

            var studentWin = Window.GetWindow(this) as StudentMainWindow;
            if (studentWin != null)
            {
                studentWin.StudentContentArea.Content = new CourseDetailView(_dbManager, _course);
            }
        }

        // --- VIDEO CONTROLS ---

        private void BtnPlayPause_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying)
            {
                LessonVideoPlayer.Pause();
                BtnPlayPause.Text = "▶";
            }
            else
            {
                LessonVideoPlayer.Play();
                BtnPlayPause.Text = "⏸";
            }
            _isPlaying = !_isPlaying;
            UpdateOverlayPlayButton();
        }

        private void UpdateOverlayPlayButton()
        {
            if (_isPlaying)
            {
                OverlayPlayPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                OverlayPlayPanel.Visibility = Visibility.Visible;
                if (_currentLesson != null)
                    OverlayLessonTitle.Text = _currentLesson.Title;
            }
        }

        private void BtnRewind_Click(object sender, MouseButtonEventArgs e)
        {
            LessonVideoPlayer.Position -= TimeSpan.FromSeconds(10);
        }

        private void BtnForward_Click(object sender, MouseButtonEventArgs e)
        {
            LessonVideoPlayer.Position += TimeSpan.FromSeconds(10);
        }

        private void BtnMute_Click(object sender, MouseButtonEventArgs e)
        {
            _isMuted = !_isMuted;
            LessonVideoPlayer.IsMuted = _isMuted;
            BtnMute.Text = _isMuted ? "🔇" : "🔊";
        }

        private void BtnFullscreen_Click(object sender, MouseButtonEventArgs e)
        {
            if (_fullScreenWindow == null)
            {
                _savedPosition = LessonVideoPlayer.Position;
                _wasPlaying = _isPlaying;
                _isTransitioningFullscreen = true;

                // Enter fullscreen
                _fullScreenWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    WindowState = WindowState.Maximized,
                    Topmost = true,
                    Background = Brushes.Black,
                    ShowInTaskbar = false
                };

                // Remove VideoGrid from its parent
                VideoContainerBorder.Child = null;

                // Add to Window
                _fullScreenWindow.Content = VideoGrid;

                _fullScreenWindow.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.Escape)
                    {
                        ExitFullScreen();
                    }
                };

                _fullScreenWindow.Closed += (s, args) =>
                {
                    if (_fullScreenWindow != null) ExitFullScreen();
                };

                _fullScreenWindow.Show();
            }
            else
            {
                ExitFullScreen();
            }
        }

        private void ExitFullScreen()
        {
            if (_fullScreenWindow != null)
            {
                _savedPosition = LessonVideoPlayer.Position;
                _wasPlaying = _isPlaying;
                _isTransitioningFullscreen = true;

                _fullScreenWindow.Content = null;
                _fullScreenWindow.Hide();
                
                // Put back
                VideoContainerBorder.Child = VideoGrid;
                
                var winToClose = _fullScreenWindow;
                _fullScreenWindow = null;
                winToClose.Close();
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            LessonVideoPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
            {
                // Update time text while dragging
                if (LessonVideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    TxtTime.Text = $"{TimeSpan.FromSeconds(ProgressSlider.Value).ToString(@"mm\:ss")} / {LessonVideoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss")}";
                }
            }
        }

        // --- COMMENTS LOGIC ---

        private async void SetupCommentsListener()
        {
            if (_currentLesson == null) return;
            try
            {
                var response = await ApiService.GetAsync<List<CommentResponse>>($"comments/lesson/{_currentLesson.Id}");
                if (response != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var allComments = response.Select(r => {
                            var c = r.Data;
                            if (c != null) 
                            {
                                c.Id = r.Id;
                                // CanDelete is true if current user is Instructor or Author
                                var currentUser = _dbManager.GetCurrentUser();
                                bool isInstructor = _course != null && currentUser != null && _course.InstructorId == currentUser.Id;
                                bool isAuthor = currentUser != null && currentUser.Id == c.UserId;
                                c.CanDelete = isInstructor || isAuthor;
                            }
                            return c;
                        }).Where(c => c != null).ToList();
                        
                        // Group into Tree
                        var rootComments = allComments.Where(c => string.IsNullOrEmpty(c.ParentId)).ToList();
                        var replies = allComments.Where(c => !string.IsNullOrEmpty(c.ParentId)).ToList();

                        foreach(var root in rootComments)
                        {
                            var children = replies.Where(r => r.ParentId == root.Id).OrderBy(r => r.CreatedAt).ToList();
                            root.Replies = new System.Collections.ObjectModel.ObservableCollection<Comment>(children);
                        }
                        
                        CommentsList.ItemsSource = rootComments;
                    });
                }
            }
            catch (Exception ex) { }
        }

        private void InputComment_GotFocus(object sender, RoutedEventArgs e)
        {
            InputCommentPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void InputComment_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputComment.Text))
            {
                InputCommentPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private async void BtnSendComment_Click(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputComment.Text) || _currentLesson == null) return;

            string content = InputComment.Text.Trim();
            InputComment.Text = string.Empty;
            InputCommentPlaceholder.Visibility = Visibility.Visible;
            BtnSendComment.IsEnabled = false;

            // Determine role and name from Application State or DB.
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            string userName = !string.IsNullOrEmpty(currentUser.FullName) ? currentUser.FullName : "Học viên ẩn danh";
            string role = _course.InstructorId == currentUser.Id ? "Instructor" : "Student";

            var newComment = new
            {
                LessonId = _currentLesson.Id,
                ParentId = "",
                Content = content,
                UserName = userName,
                UserRole = role
            };

            await ApiService.PostAsync("comments", newComment);
            SetupCommentsListener(); // Refresh list after posting
            
            BtnSendComment.IsEnabled = true;
        }

        private void BtnShowReply_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is string commentId)
            {
                var rootComments = CommentsList.ItemsSource as List<Comment>;
                if (rootComments == null) return;

                var comment = rootComments.FirstOrDefault(c => c.Id == commentId);
                if (comment != null)
                {
                    comment.IsReplying = !comment.IsReplying;
                }
            }
        }

        private async void BtnSubmitReply_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string parentId)
            {
                var rootComments = CommentsList.ItemsSource as List<Comment>;
                var parentComment = rootComments?.FirstOrDefault(c => c.Id == parentId);
                
                if (parentComment == null || string.IsNullOrWhiteSpace(parentComment.ReplyText)) return;

                string content = parentComment.ReplyText.Trim();
                parentComment.ReplyText = string.Empty;
                parentComment.IsReplying = false;

                var currentUser = _dbManager.GetCurrentUser();
                if (currentUser == null) return;

                string userName = !string.IsNullOrEmpty(currentUser.FullName) ? currentUser.FullName : "Học viên ẩn danh";
                string role = _course.InstructorId == currentUser.Id ? "Instructor" : "Student";

                var newComment = new
                {
                    LessonId = _currentLesson.Id,
                    ParentId = parentId,
                    Content = content,
                    UserName = userName,
                    UserRole = role
                };

                await ApiService.PostAsync("comments", newComment);
                SetupCommentsListener();
            }
        }

        private async void BtnDeleteComment_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is string commentId)
            {
                var confirm = CustomDialog.Confirm("Bạn có chắc chắn muốn xóa bình luận này không?", "Xác nhận xóa", "Xóa", "Hủy", DialogType.Warning);
                if (confirm)
                {
                    try
                    {
                        var response = await ApiService.DeleteAsync($"comments/{commentId}");
                        if (response)
                        {
                            SetupCommentsListener();
                        }
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show($"Lỗi khi xóa bình luận:\n{ex.Message}", "Lỗi", DialogType.Error);
                    }
                }
            }
        }
    }
}
