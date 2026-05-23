using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace e_learning_app
{
    /// <summary>
    /// Interaction logic for ExamCard.xaml
    /// </summary>
    public partial class ExamCardView : UserControl
    {
        public event RoutedEventHandler ViewClicked
        {
            add { BtnView.Click += value; }
            remove { BtnView.Click -= value; }
        }

        public event RoutedEventHandler EditClicked
        {
            add { BtnEdit.Click += value; }
            remove { BtnEdit.Click -= value; }
        }

        public event RoutedEventHandler DeleteClicked
        {
            add { BtnDelete.Click += value; }
            remove { BtnDelete.Click -= value; }
        }

        public ExamCardView()
        {
            InitializeComponent();
        }
    }
}
