using e_learning_app;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app.Views
{
    public partial class SemesterSettingsView : UserControl
    {
        // -- Model ----------------------------------------------------
        public class SemesterConfig
        {
            public string SemesterName { get; set; } = "Học kỳ 2 – Nam học 2024-2025";
            public string SchoolName { get; set; } = "THPT Nguyễn Thị Minh Khai – TP.HCM";
            public string AcademicYear { get; set; } = "2024 – 2025";
            public string StartDate { get; set; } = "01/01/2025";
            public string EndDate { get; set; } = "31/05/2025";
            public string GradeScale { get; set; } = "Thang 10 (0 – 10)";
            public double MinPassScore { get; set; } = 5.0;
            public int WeightOral { get; set; } = 15;
            public int Weight15Min { get; set; } = 15;
            public int WeightMidterm { get; set; } = 30;
            public int WeightFinal { get; set; } = 40;
            public int MaxAbsences { get; set; } = 3;
            public string AbsenceAction { get; set; } = "Thông báo + cảnh báo học sinh";
            public bool EmailParent { get; set; } = true;
            public bool QRAttendance { get; set; } = true;
            public bool OnlineExcuse { get; set; } = false;
            public bool RoundHalf { get; set; } = true;
            public bool ShowGradeLabel { get; set; } = true;
            public bool AllowStudentView { get; set; } = true;
            public bool WeeklyEmail { get; set; } = true;
            public bool GoogleClassroom { get; set; } = false;
            public bool GoogleCalendar { get; set; } = true;
            public bool MobilePush { get; set; } = true;
            public string Language { get; set; } = "🇻🇳  Tiếng Việt";
        }

        // -- State ----------------------------------------------------
        private SemesterConfig _cfg = new();
        private SemesterConfig _saved = new();
        private bool _dirty = false;
        private bool _loading = false;   // suppress TextChanged during init

        // -- Constructor ----------------------------------------------
        public SemesterSettingsView()
        {
            InitializeComponent();
            InitComboBoxes();
            LoadConfig();
            PushToUI();
        }

        // -- Init comboboxes ------------------------------------------
        private void InitComboBoxes()
        {
            CbGradeScale.ItemsSource = new[] { "Thang 10 (0 – 10)", "Thang 4 (GPA)", "Thang 100" };
            CbAbsenceAction.ItemsSource = new[]
            {
                "Thông báo + cảnh báo học sinh",
                "Gửi email phụ huynh ngay lập tức",
                "Đình chỉ thi cuối kỳ",
                "Chỉ ghi nhận, không hành động"
            };
            CbLanguage.ItemsSource = new[] { "🇻🇳  Tiếng Việt", "🇬🇧  English", "🇨🇳  中文" };
        }

        // -- Load / Save ----------------------------------------------
        private void LoadConfig()
        {
            const string path = "semester_config.json";
            if (File.Exists(path))
            {
                try { _cfg = JsonSerializer.Deserialize<SemesterConfig>(File.ReadAllText(path)) ?? new(); }
                catch { _cfg = new(); }
            }
            Clone(_cfg, _saved);
        }

        private void SaveConfig()
        {
            const string path = "semester_config.json";
            File.WriteAllText(path, JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true }));
            Clone(_cfg, _saved);
        }

        private static void Clone(SemesterConfig src, SemesterConfig dst)
        {
            dst.SemesterName = src.SemesterName; dst.SchoolName = src.SchoolName;
            dst.AcademicYear = src.AcademicYear; dst.StartDate = src.StartDate;
            dst.EndDate = src.EndDate; dst.GradeScale = src.GradeScale;
            dst.MinPassScore = src.MinPassScore; dst.WeightOral = src.WeightOral;
            dst.Weight15Min = src.Weight15Min; dst.WeightMidterm = src.WeightMidterm;
            dst.WeightFinal = src.WeightFinal; dst.MaxAbsences = src.MaxAbsences;
            dst.AbsenceAction = src.AbsenceAction; dst.EmailParent = src.EmailParent;
            dst.QRAttendance = src.QRAttendance; dst.OnlineExcuse = src.OnlineExcuse;
            dst.RoundHalf = src.RoundHalf; dst.ShowGradeLabel = src.ShowGradeLabel;
            dst.AllowStudentView = src.AllowStudentView; dst.WeeklyEmail = src.WeeklyEmail;
            dst.GoogleClassroom = src.GoogleClassroom; dst.GoogleCalendar = src.GoogleCalendar;
            dst.MobilePush = src.MobilePush; dst.Language = src.Language;
        }

        // -- Push model → UI ------------------------------------------
        private void PushToUI()
        {
            _loading = true;
            TxtSemesterName.Text = _cfg.SemesterName;
            TxtSchoolName.Text = _cfg.SchoolName;
            TxtAcademicYear.Text = _cfg.AcademicYear;
            TxtStartDate.Text = _cfg.StartDate;
            TxtEndDate.Text = _cfg.EndDate;
            TxtMinPass.Text = _cfg.MinPassScore.ToString("0.0");
            TxtMaxAbsences.Text = _cfg.MaxAbsences.ToString();
            TxtWeightOral.Text = _cfg.WeightOral.ToString();
            TxtWeight15.Text = _cfg.Weight15Min.ToString();
            TxtWeightMid.Text = _cfg.WeightMidterm.ToString();
            TxtWeightFinal.Text = _cfg.WeightFinal.ToString();
            CbGradeScale.SelectedItem = _cfg.GradeScale;
            CbAbsenceAction.SelectedItem = _cfg.AbsenceAction;
            CbLanguage.SelectedItem = _cfg.Language;
            ChkEmailParent.IsChecked = _cfg.EmailParent;
            ChkQRAttend.IsChecked = _cfg.QRAttendance;
            ChkOnlineExcuse.IsChecked = _cfg.OnlineExcuse;
            ChkRoundHalf.IsChecked = _cfg.RoundHalf;
            ChkGradeLabel.IsChecked = _cfg.ShowGradeLabel;
            ChkStudentView.IsChecked = _cfg.AllowStudentView;
            ChkWeeklyEmail.IsChecked = _cfg.WeeklyEmail;
            ChkGoogleClass.IsChecked = _cfg.GoogleClassroom;
            ChkGoogleCal.IsChecked = _cfg.GoogleCalendar;
            ChkMobilePush.IsChecked = _cfg.MobilePush;
            _loading = false;
            UpdateWeightSum();
            _dirty = false;
        }

        // -- Collect UI → model ---------------------------------------
        private void CollectFromUI()
        {
            _cfg.SemesterName = TxtSemesterName.Text.Trim();
            _cfg.SchoolName = TxtSchoolName.Text.Trim();
            _cfg.AcademicYear = TxtAcademicYear.Text.Trim();
            _cfg.StartDate = TxtStartDate.Text.Trim();
            _cfg.EndDate = TxtEndDate.Text.Trim();
            _cfg.GradeScale = CbGradeScale.SelectedItem?.ToString() ?? _cfg.GradeScale;
            _cfg.AbsenceAction = CbAbsenceAction.SelectedItem?.ToString() ?? _cfg.AbsenceAction;
            _cfg.Language = CbLanguage.SelectedItem?.ToString() ?? _cfg.Language;
            if (double.TryParse(TxtMinPass.Text, out double mp)) _cfg.MinPassScore = mp;
            if (int.TryParse(TxtMaxAbsences.Text, out int ma)) _cfg.MaxAbsences = ma;
        }

        // -- Weight sum display ----------------------------------------
        private void UpdateWeightSum()
        {
            int sum = _cfg.WeightOral + _cfg.Weight15Min + _cfg.WeightMidterm + _cfg.WeightFinal;
            bool ok = sum == 100;
            TxtWeightSumMsg.Text = ok ? $"✅  Tổng trọng số: {sum}% (hợp lệ)"
                                           : $"⚠️  Tổng trọng số: {sum}% (phải bằng 100%)";
            TxtWeightSumMsg.Foreground = ok ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                                           : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            WeightSumBar.Background = ok ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7))
                                           : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
        }

        // -- Validation -----------------------------------------------
        private (bool ok, string err) Validate()
        {
            if (string.IsNullOrWhiteSpace(_cfg.SemesterName))
                return (false, "Tên học kỳ không được để trống.");
            int sum = _cfg.WeightOral + _cfg.Weight15Min + _cfg.WeightMidterm + _cfg.WeightFinal;
            if (sum != 100)
                return (false, $"Tổng trọng số diểm phải bằng 100% (hiện tại {sum}%).");
            if (_cfg.MinPassScore < 0 || _cfg.MinPassScore > 10)
                return (false, "Điểm đạt tối thiểu phải trong khoảng 0 – 10.");
            return (true, "");
        }

        // -- Event handlers -------------------------------------------
        private void Field_Changed(object sender, EventArgs e)
        {
            if (_loading) return;
            _dirty = true;
        }

        private void WeightBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            if (sender is not TextBox tb) return;
            bool valid = int.TryParse(tb.Text, out int val) && val >= 0 && val <= 100;
            tb.Foreground = valid
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
                : Brushes.Red;
            if (valid)
            {
                switch (tb.Tag?.ToString())
                {
                    case "oral": _cfg.WeightOral = val; break;
                    case "15min": _cfg.Weight15Min = val; break;
                    case "midterm": _cfg.WeightMidterm = val; break;
                    case "final": _cfg.WeightFinal = val; break;
                }
                UpdateWeightSum();
            }
            _dirty = true;
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || sender is not CheckBox cb) return;
            bool v = cb.IsChecked == true;
            switch (cb.Tag?.ToString())
            {
                case "emailParent": _cfg.EmailParent = v; break;
                case "qrAttendance": _cfg.QRAttendance = v; break;
                case "onlineExcuse": _cfg.OnlineExcuse = v; break;
                case "roundHalf": _cfg.RoundHalf = v; break;
                case "gradeLabel": _cfg.ShowGradeLabel = v; break;
                case "studentView": _cfg.AllowStudentView = v; break;
                case "weeklyEmail": _cfg.WeeklyEmail = v; break;
                case "googleClassroom": _cfg.GoogleClassroom = v; break;
                case "googleCalendar": _cfg.GoogleCalendar = v; break;
                case "mobilePush": _cfg.MobilePush = v; break;
            }
            _dirty = true;
        }

        private void CbAbsenceAction_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            _cfg.AbsenceAction = CbAbsenceAction.SelectedItem?.ToString() ?? _cfg.AbsenceAction;
            _dirty = true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            CollectFromUI();
            var (ok, err) = Validate();
            if (!ok) { CustomDialog.Show(err, "Lỗi dữ liệu", DialogType.Warning); return; }
            SaveConfig();
            _dirty = false;
            CustomDialog.Show("Đã luu cài đặt học kỳ thành công! ✅", "Thành công", DialogType.Success);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_dirty) return;
            var confirmed = CustomDialog.Confirm("Hủy tất cả thay đổi chua luu?", "Xác nhận", "Hủy thay đổi", "Quay lại", DialogType.Question);
            if (confirmed)
            {
                Clone(_saved, _cfg);
                PushToUI();
            }
        }

        private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = CustomDialog.Confirm("Đặt lại tất cả về mặc định?", "Xác nhận", "Đặt lại", "Hủy", DialogType.Warning);
            if (!confirmed) return;
            _cfg = new SemesterConfig();
            PushToUI();
            _dirty = true;
        }

        // -- Public API -----------------------------------------------
        public bool CanLeave()
        {
            if (!_dirty) return true;
            return CustomDialog.Confirm("Có thay đổi chua luu. Rời di?", "Cảnh báo", "Rời di", "Ở lại", DialogType.Warning);
        }

        public SemesterConfig CurrentConfig => _cfg;
    }
}
