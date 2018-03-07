using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
        }


        private void BTCAddressCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = "3D5EiYjuit8b7yPveiCTAZxvn1j8rDhuxU";

            Clipboard.SetText(iText);
        }
        private void ETHAddressCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = "0x869Da5454d33394f457fbbb691038071363bA9BC";

            Clipboard.SetText(iText);
        }
        private void LTCAddressCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = "Lcd1bbpqKQTmyuJE1QKFaUwm3YkqikfaVA";

            Clipboard.SetText(iText);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void EonSharpLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Zof-R/EonSharp/");
        }

    }
}
