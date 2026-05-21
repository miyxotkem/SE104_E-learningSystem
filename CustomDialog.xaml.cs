using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app
{
    public enum DialogType { Info, Success, Warning, Error, Question }
    public enum CustomDialogResult { OK, Yes, No, Cancel, Logout, Exit }

    public partial class CustomDialog : Window
    {
        public CustomDialogResult Result { get; private set; } = CustomDialogResult.Cancel;

        private CustomDialog(string title, string message, DialogType type)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            ApplyTheme(type);
        }

        private void ApplyTheme(DialogType type)
        {
            var (color, icon) = type switch
            {
                DialogType.Success  => ("#16A34A", "✅"),
                DialogType.Warning  => ("#D97706", "⚠️"),
                DialogType.Error    => ("#DC2626", "❌"),
                DialogType.Question => ("#3B82F6", "❓"),
                _                  => ("#3B82F6", "ℹ️"), // Info
            };

            var mainBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            var lightBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)); 

            if (StatusAccent != null) StatusAccent.Background = mainBrush;
            if (IconContainer != null) IconContainer.Background = lightBrush;
            TxtIcon.Text = icon;
        }

        private void AddButton(string label, string styleKey, CustomDialogResult result)
        {
            var btn = new Button
            {
                Content = label,
                Style = (Style)FindResource(styleKey),
                Margin = new Thickness(10, 0, 0, 0)
            };
            btn.Click += (s, e) =>
            {
                Result = result;
                this.Close();
            };
            ButtonPanel.Children.Add(btn);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomDialogResult.Cancel;
            this.Close();
        }

        // Tìm cửa sổ dang hiển thị để làm Owner
        private static Window GetActiveWindow()
        {
            var active = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (active == null) active = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
            return active ?? Application.Current.MainWindow;
        }

        public static void Show(string message, string title = "Thông báo", DialogType type = DialogType.Info)
        {
            var dlg = new CustomDialog(title, message, type);
            dlg.Owner = GetActiveWindow();
            dlg.AddButton("Đã hiểu", "PrimaryBtn", CustomDialogResult.OK);
            dlg.ShowDialog();
        }

        public static bool Confirm(string message, string title = "Xác nhận", string yesText = "Đồng ý", string noText = "Hủy bỏ", DialogType type = DialogType.Question)
        {
            var dlg = new CustomDialog(title, message, type);
            dlg.Owner = GetActiveWindow();
            dlg.AddButton(noText, "SecondaryBtn", CustomDialogResult.No);
            dlg.AddButton(yesText, "PrimaryBtn",   CustomDialogResult.Yes);
            dlg.ShowDialog();
            return dlg.Result == CustomDialogResult.Yes;
        }

        public static CustomDialogResult ShowExit(string message = "Bạn muốn làm gì trước khi thoát?", string title = "Xác nhận thoát")
        {
            var dlg = new CustomDialog(title, message, DialogType.Question);
            dlg.Owner = GetActiveWindow();
            dlg.AddButton("Hủy",       "SecondaryBtn", CustomDialogResult.Cancel);
            dlg.AddButton("Thoát",     "SecondaryBtn", CustomDialogResult.Exit);
            dlg.AddButton("Đăng xuất", "DangerBtn",    CustomDialogResult.Logout);
            dlg.ShowDialog();
            return dlg.Result;
        }

        public static CustomDialogResult Custom(string title, string message, DialogType type, params (string label, string style, CustomDialogResult result)[] buttons)
        {
            var dlg = new CustomDialog(title, message, type);
            dlg.Owner = GetActiveWindow();
            foreach (var (label, style, result) in buttons)
                dlg.AddButton(label, style, result);
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}
