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
using System.Windows.Shapes;

namespace myEonWallet
{
    /// <summary>
    /// Interaction logic for DepositConfirm.xaml
    /// </summary>
    public partial class DepositConfirm : Window
    {
        public DepositConfirm(string message)
        {
            InitializeComponent();

            txtMessage.Text = message;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void walletPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if ( walletPasswordBox.Password.Length >= 5)
            {
                YesButton.Visibility = Visibility.Visible;
            }
        }

        public void ShowDepositFields(bool visible)
        {
            if (visible)
            {
                DepositAmountTB.Visibility = Visibility.Visible;
                DepositLabel1.Visibility = Visibility.Visible;
                DepositLabel2.Visibility = Visibility.Visible;
            }
            else
            {
                DepositAmountTB.Visibility = Visibility.Hidden;
                DepositLabel1.Visibility = Visibility.Hidden;
                DepositLabel2.Visibility = Visibility.Hidden;

            }
        }
    }
}
