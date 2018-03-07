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
using myEonClient;
using EonSharp;


namespace myEonWallet
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class NewAttachedAccountDialog : Window
    {

        MyEonClient eonClient;

        public Wallet primaryWallet;
        public Wallet newWallet;

        bool AddAfterImport = false;

        /* public NewAttachedAccountDialog()
         {
             InitializeComponent();
         }*/
        public NewAttachedAccountDialog(MyEonClient refClient)
        {
            InitializeComponent();

            eonClient = refClient;
            primaryWallet = eonClient.WalletManager.WalletCollection[0];
        }

        //consumers can attach to the debug event.
        public event EventHandler<Wallet> NewWalletEvent;

        private void Name_TB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Name_TB.Text.Length > 1)
            {
                pwBox1.IsEnabled = true;
                pwBox2.IsEnabled = true;
            }
            else
            {
                pwBox1.IsEnabled = false;
                pwBox2.IsEnabled = false;
            }

        }

        private async void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {

            //create new wallet
            if ((pwBox1.Password == pwBox2.Password) && (pwBox1.Password.Length >= 5))
            {
                ImportSeedButton.Visibility = Visibility.Hidden;

                newWallet = eonClient.CreateAccount(Name_TB.Text, pwBox1.Password);

                newWallet.UnlockAccountDetails(pwBox1.Password);

                newWallet.Information = new EonSharp.Api.Info();
                newWallet.Information.Amount = 0;
                newWallet.Information.Deposit = 0;
                
                var seed = EonSharp.Helpers.HexHelper.ArrayToHexString(newWallet.GetPrivateKey(pwBox1.Password));                
                Seed_TB.Text = seed;

                //update the display with generated account details
                AccountID_TB.Text = newWallet.AccountDetails.AccountId;
                PublicKey_TB.Text = newWallet.AccountDetails.PublicKey;

                AccountID_TB.IsEnabled = true;
                PublicKey_TB.IsEnabled = true;
                Seed_TB.IsEnabled = true;

                //enable the register and copy buttons
                RegisterAccountButton.IsEnabled = true;
                CopyButton.IsEnabled = true;

                primaryPasswordPWBOX.IsEnabled = true;
            }

        }


        private async void RegisterAccountButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = false;

            //register newWallet using the primary account.
            //TransactionJsonRegistrationAttachment newAccountInfo = new TransactionJsonRegistrationAttachment();
            //newAccountInfo.UserID = newWallet.AccountDetails.AccountId;
            //newAccountInfo.PublicKey = newWallet.AccountDetails.PublicKey;

            //bool result = eonUtil.SendTransaction(primaryWallet, EonUtil.TxType.AccountRegistration, newAccountInfo, 10, 60, "EON-B-2CMBX-669EY-TWFBK", primaryWallet.AccountID, 0);
            //*/
            try
            {
                await eonClient.Transaction_Register(newWallet, primaryPasswordPWBOX.Password);
                result = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Transaction Register() Exception : " + ex.Message);
                result = false;
            }

            if (result)
            {
                ResultLabel.Content = "Successfully registered " + newWallet.AccountDetails.AccountId;
                ResultLabel.Foreground = Brushes.Green;

                AddButton.Visibility = Visibility.Visible;

                //await newWallet.RefreshAsync(eonClient.eonSharpClient);

                //callback with new wallet details
                NewWalletEvent?.Invoke(this, newWallet);
            }
            else
            {
                ResultLabel.Content = "Account registeration failed";
                ResultLabel.Foreground = Brushes.Red;
            }


        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = "Account Name : " + Name_TB.Text + "\r\n";
            iText += "Seed : " + Seed_TB.Text + "\r\n";
            iText += "Publi-Key : " + PublicKey_TB.Text + "\r\n";
            iText += "AccountID : " + AccountID_TB.Text + "\r\n";
            Clipboard.SetText(iText);
        }

        private void pwBox2_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if ((pwBox1.Password == pwBox2.Password) && (pwBox1.Password.Length >= 5))
            {
                CreateAccountButton.IsEnabled = true;
                ImportSeedButton.IsEnabled = true;
                Seed_TB.IsEnabled = true;
            }
        }



        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (AddAfterImport) eonClient.WalletManager.AddWallet(newWallet);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ImportSeedButton_Click(object sender, RoutedEventArgs e)
        {
            if ((Seed_TB.Text.Length == 64) && (pwBox1.Password.Length >= 5) && (pwBox1.Password == pwBox2.Password))
            {
                CreateAccountButton.Visibility = Visibility.Hidden;

                var seed = EonSharp.Helpers.HexHelper.HexStringToByteArray(Seed_TB.Text);

                newWallet = new Wallet("Primary", seed, pwBox1.Password);

                AccountID_TB.Text = newWallet.AccountDetails.AccountId;
                PublicKey_TB.Text = newWallet.AccountDetails.PublicKey;

                Seed_TB.IsEnabled = true;
                AccountID_TB.IsEnabled = true;
                PublicKey_TB.IsEnabled = true;
                CopyButton.IsEnabled = true;
                AddButton.IsEnabled = true;
                AddButton.Visibility = Visibility.Visible;

                AddAfterImport = true;

            }
        }

        private void primaryPasswordPWBOX_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (primaryPasswordPWBOX.Password.Length > 1)
            {
                RegisterAccountButton.IsEnabled = true;
                CreateAccountButton.IsEnabled = true;

            }
        }
    }
}
