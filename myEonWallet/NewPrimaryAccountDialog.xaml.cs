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
using EonSharp;
using myEonClient;

namespace myEonWallet
{
    /// <summary>
    /// Interaction logic for NewPrimaryAccountDialog.xaml
    /// </summary>
    public partial class NewPrimaryAccountDialog : Window
    {
        Wallet newWallet;

        MyEonClient eonClient;

        public NewPrimaryAccountDialog(MyEonClient client)
        {
            InitializeComponent();

            eonClient = client;

            //keep this window on top
            this.Topmost = true;
        }

        private void OpenRegLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://testnet.eontechnology.org/");
            AddButton.Visibility = Visibility.Visible;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = "Account Name : Primary\r\n";
            iText += "Seed : " + Seed_TB.Text + "\r\n";
            iText += "Public-Key : " + PublicKey_TB.Text + "\r\n";
            iText += "AccountID : " + AccountID_TB.Text + "\r\n";
            Clipboard.SetText(iText);
        }

        private void GenerateAccount_Click(object sender, RoutedEventArgs e)
        {
            if ((pwBox1.Password.Length >= 5) && (pwBox1.Password == pwBox2.Password))
            {
                ImportSeedButton.IsEnabled = false;
                ImportSeedButton.Visibility = Visibility.Hidden;
                OpenRegLink.IsEnabled = true;
                newWallet = new Wallet("Primary", pwBox1.Password);
                //newWallet.UnlockAccountDetails(pwBox1.Password);

                var seed = EonSharp.Helpers.HexHelper.ArrayToHexString(newWallet.GetPrivateKey(pwBox1.Password));


                Seed_TB.Text = seed;
                AccountID_TB.Text = newWallet.AccountDetails.AccountId;
                PublicKey_TB.Text = newWallet.AccountDetails.PublicKey;

                AccountID_TB.IsEnabled = true;
                PublicKey_TB.IsEnabled = true;
                RegistrationNoticeLbl.Visibility = Visibility.Visible;
                OpenRegLink.Visibility = Visibility.Visible;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await newWallet.RefreshAsync(eonClient.eonSharpClient);
            }
            catch
            {
                newWallet.Information = new EonSharp.Api.Info();
                newWallet.Information.Amount = 0;
                newWallet.Information.Deposit = 0;
            }
            eonClient.WalletManager.AddWallet(newWallet);
            this.Close();
        }

        
        private void ImportSeedButton_Click(object sender, RoutedEventArgs e)
        {
            if ((Seed_TB.Text.Length == 64)&&(pwBox1.Password.Length >=5)&&(pwBox1.Password==pwBox2.Password))
            {
                GenerateAccountButton.IsEnabled = false;
                GenerateAccountButton.Visibility = Visibility.Hidden;
                ImportSeedButton.IsEnabled = false;
                OpenRegLink.IsEnabled = false;
                AddButton.Visibility = Visibility.Visible;

                var seed = EonSharp.Helpers.HexHelper.HexStringToByteArray(Seed_TB.Text);
                newWallet = new Wallet("Primary", seed, pwBox1.Password);

                AccountID_TB.Text = newWallet.AccountDetails.AccountId;
                PublicKey_TB.Text = newWallet.AccountDetails.PublicKey;

                AccountID_TB.IsEnabled = true;
                PublicKey_TB.IsEnabled = true;
            }
            
            
        }

        private void pwBox2_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if ((pwBox1.Password==pwBox2.Password)&&(pwBox1.Password.Length>=5))
            {
                GenerateAccountButton.IsEnabled = true;
                ImportSeedButton.IsEnabled = true;
                Seed_TB.IsEnabled = true;
            }
        }
    }
}
