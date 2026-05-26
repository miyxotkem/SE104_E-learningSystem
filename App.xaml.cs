using System.Configuration;
using System.Data;
using System.Windows;

namespace e_learning_app
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            e_learning_app.FirebaseService.Initialize();
        }
    }
}
