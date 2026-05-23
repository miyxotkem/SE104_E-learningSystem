using e_learning_app;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app.Views
{
    public partial class QuestionBankView : UserControl
    {
        // -- Model ----------------------------------------------------
        public class Question
        {
            public int Id { get; set; }
            public string Subject { get; set; }
            public string Level { get; set; }
            public string Type { get; set; }
            public string Content { get; set; }
            public int UsedInExams { get; set; }
            public DateTime AddedDate { get; set; }
        }

        // -- State ----------------------------------------------------
        private List<Question> _all = new();
        private string _subject = "Tất cả";
        private HashSet<string> _levels = new() { "Dễ", "Trung bình", "Khó" };
        private HashSet<string> _types = new() { "Trắc nghiệm", "Tự luận", "Điền vào chỗ trống" };
        private string _search = "";

        // -- Color maps -----------------------------------------------
        static readonly Dictionary<string, (string bg, string fg)> _subjectClr = new()
        {
            {"Toán học",("#EFF6FF","#3B82F6")}, {"Vật lý",("#EDE9FE","#7C3AED")},
            {"Hóa học",("#ECFDF5","#059669")},  {"Sinh học",("#F0FDF4","#16A34A")},
            {"Ngữ van",("#FFF7ED","#D97706")},
        };
        static readonly Dictionary<string, (string bg, string fg)> _levelClr = new()
        {
            {"Dễ",("#DCFCE7","#16A34A")}, {"Trung bình",("#FEF3C7","#D97706")}, {"Khó",("#FEE2E2","#DC2626")},
        };

        // -- Constructor ----------------------------------------------
        public QuestionBankView() { InitializeComponent(); LoadSampleData(); }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TxtSearch.GotFocus += (_, _) => TxtSearchPH.Visibility = Visibility.Collapsed;
            TxtSearch.LostFocus += (_, _) =>
            {
                if (string.IsNullOrEmpty(TxtSearch.Text))
                    TxtSearchPH.Visibility = Visibility.Visible;
            };
            Refresh();
        }

        // -- Data -----------------------------------------------------
        private void LoadSampleData()
        {
            _all = new List<Question>
            {
                new() { Id=1, Subject="Toán học", Level="Trung bình", Type="Trắc nghiệm",
                        Content="Tìm giới hạn của hàm số f(x) = (x² - 4)/(x - 2) khi x tiến tới 2.",
                        UsedInExams=3, AddedDate=new DateTime(2025,3,14) },
                new() { Id=2, Subject="Vật lý",   Level="Khó",        Type="Tự luận",
                        Content="Một vật khối lượng 2kg chuyển động đều trên mặt phẳng nghiêng góc 30°. Tính lực ma sát. Cho g = 10 m/s².",
                        UsedInExams=5, AddedDate=new DateTime(2025,2,2) },
                new() { Id=3, Subject="Hóa học",  Level="Dễ",         Type="Trắc nghiệm",
                        Content="Phản ứng nào sau đây là oxi hóa – khử? A. NaOH + HCl  B. Fe + CuSO4 → FeSO4 + Cu",
                        UsedInExams=7, AddedDate=new DateTime(2025,1,10) },
                new() { Id=4, Subject="Toán học", Level="Khó",        Type="Tự luận",
                        Content="Chứng minh với mọi số nguyên n, biểu thức n³ - n luôn chia hết cho 6.",
                        UsedInExams=2, AddedDate=new DateTime(2025,3,20) },
                new() { Id=5, Subject="Sinh học", Level="Trung bình", Type="Trắc nghiệm",
                        Content="Quá trình nào xảy ra trong pha sáng của quang hợp? A. ATP  B. Cố định CO2  C. Glucose  D. Protein",
                        UsedInExams=4, AddedDate=new DateTime(2025,2,28) },
                new() { Id=6, Subject="Ngữ van",  Level="Dễ",         Type="Điền vào chỗ trống",
                        Content="Điền từ: \"Van học là ______ của cuộc sống.\" (guong / bức tranh / tiếng vang)",
                        UsedInExams=1, AddedDate=new DateTime(2025,3,5) },
            };
        }

        // -- Filter / Render ------------------------------------------
        private IEnumerable<Question> Filtered()
        {
            var q = _all.AsEnumerable();
            if (_subject != "Tất cả") q = q.Where(x => x.Subject == _subject);
            q = q.Where(x => _levels.Contains(x.Level) && _types.Contains(x.Type));
            if (!string.IsNullOrWhiteSpace(_search))
                q = q.Where(x => x.Content.Contains(_search, StringComparison.OrdinalIgnoreCase)
                               || x.Subject.Contains(_search, StringComparison.OrdinalIgnoreCase));
            return q;
        }

        private void Refresh()
        {
            // Stat cards
            TxtTotalCount.Text = _all.Count.ToString();
            TxtHardCount.Text = _all.Count(q => q.Level == "Khó").ToString();
            TxtMidCount.Text = _all.Count(q => q.Level == "Trung bình").ToString();
            TxtEasyCount.Text = _all.Count(q => q.Level == "Dễ").ToString();

            //var results = Filtered().ToList();
            ////TxtResultCount.Text = $"{results.Count} kết quả";

            //QuestionsPanel.Children.Clear();
            //foreach (var q in results)
            //    QuestionsPanel.Children.Add(BuildQuestionRow(q));
        }

        private Border BuildQuestionRow(Question q)
        {
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.05, Color = Colors.Black }
            };
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: tags + content + meta
            var left = new StackPanel();
            var tags = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            tags.Children.Add(MakePill(_subjectClr.GetValueOrDefault(q.Subject, ("#EFF6FF", "#3B82F6")), q.Subject));
            tags.Children.Add(MakePill(_levelClr.GetValueOrDefault(q.Level, ("#F1F5F9", "#475569")), q.Level, 6));
            tags.Children.Add(MakePill(("#F1F5F9", "#64748B"), q.Type, 6));
            left.Children.Add(tags);
            left.Children.Add(new TextBlock { Text = q.Content, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), TextWrapping = TextWrapping.Wrap });
            left.Children.Add(new TextBlock { Text = $"Dùng trong {q.UsedInExams} đề  ·  {q.AddedDate:dd/MM/yyyy}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), Margin = new Thickness(0, 6, 0, 0) });
            Grid.SetColumn(left, 0);

            // Right: edit + delete buttons
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var edit = MakeIconBtn("✏️", "#F8FAFC", q.Id);
            edit.Click += BtnEdit_Click;
            var del = MakeIconBtn("🗑️", "#FFF1F2", q.Id);
            del.Margin = new Thickness(6, 0, 0, 0);
            del.Click += BtnDelete_Click;
            btns.Children.Add(edit);
            btns.Children.Add(del);
            Grid.SetColumn(btns, 1);

            root.Children.Add(left);
            root.Children.Add(btns);
            card.Child = root;
            return card;
        }

        private static Border MakePill((string bg, string fg) clr, string text, double leftMargin = 0)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(10, 4, 10, 4),
                Background = (Brush)new BrushConverter().ConvertFromString(clr.bg)!,
                Margin = new Thickness(leftMargin, 0, 0, 0)
            };
            b.Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFromString(clr.fg)!
            };
            return b;
        }

        private static Button MakeIconBtn(string icon, string bg, int id)
        {
            var btn = new Button
            {
                Background = (Brush)new BrushConverter().ConvertFromString(bg)!,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };
            var tpl = new ControlTemplate(typeof(Button));
            var f = new FrameworkElementFactory(typeof(Border));
            f.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            f.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            f.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            f.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            tpl.VisualTree = f;
            btn.Template = tpl;
            btn.Content = icon;
            return btn;
        }

        // -- Event handlers -------------------------------------------
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = TxtSearch.Text;
            TxtSearchPH.Visibility = string.IsNullOrEmpty(_search) ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void SubjectRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
                _subject = rb.Tag?.ToString() ?? "Tất cả";
            Refresh();
        }

        private void LevelCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            string tag = cb.Tag?.ToString() ?? "";
            if (cb.IsChecked == true) _levels.Add(tag); else _levels.Remove(tag);
            Refresh();
        }

        private void TypeCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            string tag = cb.Tag?.ToString() ?? "";
            if (cb.IsChecked == true) _types.Add(tag); else _types.Remove(tag);
            Refresh();
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e) => Refresh();

        private void BtnAddQuestion_Click(object sender, RoutedEventArgs e)
            => ShowDialog(null);

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
                ShowDialog(_all.FirstOrDefault(q => q.Id == id));
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int id) return;
            var q = _all.FirstOrDefault(x => x.Id == id);
            if (q == null) return;
            var confirmed = CustomDialog.Confirm($"Xóa câu hỏi này?\n\"{q.Content[..Math.Min(60, q.Content.Length)]}\"",
                "Xác nhận xóa", "Xóa ngay", "Hủy", DialogType.Warning);
            
            if (confirmed)
            { 
                _all.Remove(q); 
                Refresh(); 
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", Title = "Chọn file câu hỏi" };
            if (dlg.ShowDialog() == true)
                CustomDialog.Show($"Import: {dlg.FileName}\n(Đang phát triển)", "Import", DialogType.Info);
        }

        // -- Add/Edit dialog ------------------------------------------
        private void ShowDialog(Question? existing)
        {
            bool isEdit = existing != null;
            var w = new Window
            {
                Title = isEdit ? "Sửa câu hỏi" : "Thêm câu hỏi",
                Width = 520,
                Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC))
            };
            var sp = new StackPanel { Margin = new Thickness(28) };

            sp.Children.Add(new TextBlock { Text = "Nội dung câu hỏi:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            var txt = new TextBox
            {
                Text = existing?.Content ?? "",
                Height = 90,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Padding = new Thickness(10, 8,10,8),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0))
            };
            sp.Children.Add(txt);

            var row = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            for (int i = 0; i < 5; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 1 ? new GridLength(12) : new GridLength(1, GridUnitType.Star) });
            ComboBox Cb(int col, string[] items, string? sel)
            {
                var c = new ComboBox { ItemsSource = items, SelectedItem = sel, Height = 36, FontSize = 13 };
                Grid.SetColumn(c, col); return c;
            }
            var cbSub = Cb(0, new[] { "Toán học", "Vật lý", "Hóa học", "Sinh học", "Ngữ van" }, existing?.Subject);
            var cbLvl = Cb(2, new[] { "Dễ", "Trung bình", "Khó" }, existing?.Level);
            var cbTyp = Cb(4, new[] { "Trắc nghiệm", "Tự luận", "Điền vào chỗ trống" }, existing?.Type);
            row.Children.Add(cbSub); row.Children.Add(cbLvl); row.Children.Add(cbTyp);
            sp.Children.Add(row);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            var cancel = new Button
            {
                Content = "Hủy",
                Width = 90,
                Height = 38,
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancel.Click += (_, _) => w.Close();
            var save = new Button
            {
                Content = isEdit ? "Luu" : "Thêm",
                Width = 120,
                Height = 38,
                Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(txt.Text)) { CustomDialog.Show("Vui lòng nhập nội dung.", "Thông báo", DialogType.Warning); return; }
                if (isEdit && existing != null)
                {
                    existing.Content = txt.Text;
                    existing.Subject = cbSub.SelectedItem?.ToString() ?? existing.Subject;
                    existing.Level = cbLvl.SelectedItem?.ToString() ?? existing.Level;
                    existing.Type = cbTyp.SelectedItem?.ToString() ?? existing.Type;
                }
                else
                    _all.Add(new Question
                    {
                        Id = _all.Count + 1,
                        Content = txt.Text,
                        Subject = cbSub.SelectedItem?.ToString() ?? "Toán học",
                        Level = cbLvl.SelectedItem?.ToString() ?? "Trung bình",
                        Type = cbTyp.SelectedItem?.ToString() ?? "Trắc nghiệm",
                        AddedDate = DateTime.Today
                    });
                Refresh(); w.Close();
            };
            btnRow.Children.Add(cancel); btnRow.Children.Add(save);
            sp.Children.Add(btnRow);
            w.Content = sp; w.ShowDialog();
        }
    }
}
