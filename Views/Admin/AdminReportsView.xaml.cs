using System.Windows.Controls;

namespace e_learning_app.Views.Admin
{
    public partial class AdminReportsView : UserControl
    {
        private readonly DatabaseManager _db;

        public AdminReportsView(DatabaseManager db)
        {
            InitializeComponent();
            _db = db;
        }
    }
}
