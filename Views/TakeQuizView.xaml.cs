using System.Windows.Controls;

namespace e_learning_app.Views
{
    public partial class TakeQuizView : UserControl
    {
        private Class.Exam _exam;
        private DatabaseManager _dbManager;

        public TakeQuizView(DatabaseManager dbManager, Class.Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
        }
    }
}
