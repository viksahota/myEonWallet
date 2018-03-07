using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using myEonClient;
using System.ComponentModel;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media.Animation;
using EonSharp;
using System.Globalization;

namespace myEonWallet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool DebugViewBusy;

        public static MyEonClient eonClient;

        private NewAttachedAccountDialog nAC;

        private volatile bool ShutDownFlag = false;

        private Queue<DebugViewItem> debugViewQueue;

        public bool Debug_Checked
        { get; set; }

        public bool ErrorEnable_Checked
        { get; set; }

        //ICollectionView view;

        //ObservableCollection<MyEonClient.walletDetails> Wallet_items;

        //private int currentAccountIndex = 0;

        public MainWindow()
        {
            InitializeComponent();

            //init objects
            eonClient = new MyEonClient();
            eonClient.DebugEvent += (sender, msg) => { DebugMsg(msg); };
            eonClient.ErrorEvent += (sender, msg) => { ErrorMsg(msg); };

            //hide the debug viewer
            DebugViewGroupBox.Opacity = 0;
            DebugViewBusy = false;
            debugViewQueue = new Queue<DebugViewItem>();


        eonClient.BalanceUpdateEvent += (sender, msg) => { UpdatedBalance_Handler(msg); };
            eonClient.BalanceUpdatePeriod = Properties.Settings.Default.BalanceSyncPeriod;
            

            //set the datacontext of the config items
            EnableDebugCheckBox.DataContext = eonClient;
            EnableErrorCheckBox.DataContext = eonClient;

            Config_TransactionHistoryMax_Adjustor.DataContext = eonClient;
            Config_TransactionHistoryMax_Adjustor.Setup(Properties.Settings.Default.TransactionHistoryMax, 20, 2000000, 20, "TXHistoryMax");
            Config_TransactionHistoryMax_Adjustor.ConfigChangeEvent += (sender, msg) => { ConfigChangeHandler(msg); };


            Config_BalanceSyncPeriod_Adjustor.Setup(Properties.Settings.Default.BalanceSyncPeriod, 5, 60, 1, "BalanceSyncPeriod");
            Config_BalanceSyncPeriod_Adjustor.ConfigChangeEvent += (sender, msg) => { ConfigChangeHandler(msg); };


            Config_TransactionRecentConfirmedMax.DataContext = eonClient;
            Config_TransactionRecentConfirmedMax.Setup(eonClient.RecentTransactions_ConfirmedMax, 3, 100, 1, "RecentConfirmedMax");   
            Config_TransactionRecentConfirmedMax.ConfigChangeEvent += (sender, msg) => { ConfigChangeHandler(msg); };

            Config_PeerAddressTB.DataContext = eonClient.coreConfig;
            eonClient.coreConfig.PeerValidatedAddressEvent += (sender, msg) => { PeerValidateHandler(msg); };

            MessageBoxResult result = new MessageBoxResult();

            //eonClient.WalletManager.ResetWalletList();
            //byte[] privateKey = EonSharp.Helpers.HexHelper.HexStringToByteArray("67244C84627AC20DA7197F0B972894F85CA2528174F45870F2EEE369F9DCF59C");
            //Wallet primaryWallet = new Wallet("Primary", privateKey, "gassman");
            //eonClient.WalletManager.AddWallet(primaryWallet);

            try
            {
                eonClient.WalletManager.LoadWallets(eonClient);
            }
            catch (Exception ex)
            {
                result = MessageBox.Show("Wallet data in user settings could not be loaded, [ " + ex.Message + " ]- do you want to start afresh? (will delete all existing wallets!)", "myEonWallet : wallet data issue", MessageBoxButton.YesNo);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        eonClient.WalletManager.ResetWalletList();
                        this.Close();
                        break;
                    case MessageBoxResult.No:
                        this.Close();
                        break;
                }
            }
            


            eonClient.Start();
            
            //select the first account in list
            AccountListView.SelectedIndex = 0;
            //AccountListView.Focus();
            //AccountListView.ItemsSource = Wallet_items;
            AccountListView.ItemsSource = eonClient.WalletManager.WalletCollection;

            TransactionsListView.ItemsSource = eonClient.TransactionHistory.ConfirmedTransactionCollection;

            TransactionSummaryListView.ItemsSource = eonClient.TransactionHistory.SummaryTransactionCollection;

            //debugTB.DataContext = eonClient.DebugInfo.DebugString;
                        
            AccountListView_SelectionChanged(null, null);


            //this.Config_BalanceSyncPeriod_Adjustor.DataContext = Properties.Settings.Default.BalanceSyncPeriod;
            //this.Config_TransactionHistoryMax_Adjustor.DataContext = Properties.Settings.Default.TransactionHistoryMax;

            }

        /*public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }*/

        /*public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }*/

        private void PeerValidateHandler(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (msg=="VALID") Config_PeerAddressTB.Foreground = new SolidColorBrush(Colors.LightGreen);
            else if (msg == "INVALID") Config_PeerAddressTB.Foreground = new SolidColorBrush(Colors.Red);
            }));
        }
                
        //debug message handler
        private void DebugMsg(string line)
        {
            if (!ShutDownFlag)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    if (debugTB.Text.Length > 10000)
                    {
                        debugTB.Text = debugTB.Text.Remove(0, 500);
                    }

                    string newline = (DateTime.Now.ToLongTimeString() + ": " + line + "\r\n");
                    debugTB.Text += newline;
                    debugTB.CaretIndex = debugTB.Text.Length;
                    debugTB.ScrollToEnd();

                    //update the debug viewer on main tab
                    debugViewQueue.Enqueue(new DebugViewItem(newline, true));
                    if (!DebugViewBusy) ShowDebugMessage();

                }));
            }


        }

        //redirect errors to debug for now
        private void ErrorMsg(string line)
        {
            if (!ShutDownFlag)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    if (errorTB.Text.Length > 10000)
                    {
                        errorTB.Text = errorTB.Text.Remove(0, 500);
                    }

                    string newline = (DateTime.Now.ToLongTimeString() + ": " + line + "\r\n");
                    errorTB.Text += newline;
                    errorTB.CaretIndex = errorTB.Text.Length;
                    errorTB.ScrollToEnd();

                    //update the debug viewer on main tab
                    debugViewQueue.Enqueue(new DebugViewItem(newline, false));
                    if (!DebugViewBusy) ShowDebugMessage();
                }));
            }
        }

        //config change message handler, coming from numerical up/down usercontrol
        private void ConfigChangeHandler(string msg)
        {
            if (!ShutDownFlag)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    //balance sync change needs sending to eon client
                    if (msg == "BalanceSyncPeriod") eonClient.BalanceUpdatePeriod = Properties.Settings.Default.BalanceSyncPeriod;
                    if (msg == "RecentConfirmedMax") eonClient.RecentTransactions_ConfirmedMax = int.Parse(Config_TransactionRecentConfirmedMax.NUDTextBox.Text);


                }));
            }


        }

        //updates the account list with account list  or balance/deposit changes
        private void UpdatedBalance_Handler(object updateObject)
        {
            if (!ShutDownFlag)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    UpdateBalanceDeposit(AccountListView.SelectedIndex);
                }));

            }
        }

        //updates the main balance and deposit labels
        private void UpdateBalanceDeposit(int accountIndex)
        {
            if (accountIndex != -1)
            {
                if (eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information != null)
                {
                    BalanceLBL.Content = (decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Amount / 1000000 + " EON";
                    DepositLBL.Content = (decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit / 1000000 + " EON";
                    TotalEON_LBL.Content = (decimal)(eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Amount + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit) / 1000000 + " EON";
                }
            }
            else
            {
                BalanceLBL.Content = "-";
                DepositLBL.Content = "-";
                TotalEON_LBL.Content = "-";
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            decimal amount = 0;
            
            if (decimal.TryParse(SendAmountTB.Text, out amount))
            {
                MsgBoxYesNo msgbox = new MsgBoxYesNo("Send " + amount + " EON from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to account " + RecipientTB.Text + " ?\r\n\r\n  - Enter your wallet encryption password then press YES to confirm and place this transaction on the EON blockchain.");
                if ((bool)msgbox.ShowDialog())
                {

                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_SendPayment(AccountListView.SelectedIndex, RecipientTB.Text, 1000000 * amount, msgbox.walletPasswordBox.Password);
                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS : Sent " + amount + " EON  from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to " + RecipientTB.Text);
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Sending of " + amount + " EON from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to account " + RecipientTB.Text + " - " + RpcResult.Message);

                        }
                    }
                    catch (Exception ex)
                    {

                        ErrorMsg("Transaction FAILED - Sending of " + amount + " EON from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to account " + RecipientTB.Text + " [Exception] - " + ex.Message);

                    }
                }
                else
                {
                    //cancelled
                }
            }
            else
            {
                ErrorMsg("Error parsing the AMOUNT. Correct before retrying.");
            }
        }

        //keep track when the user selects another account
        private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (eonClient.WalletManager.WalletCollection.Count > 0)
            {
                SelectedAccountAddress_LBL.Content = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId;
                UpdateBalanceDeposit(AccountListView.SelectedIndex);
                eonClient.selectedIndex = AccountListView.SelectedIndex;
                eonClient.UpdateTransactionSummary(AccountListView.SelectedIndex);
            }
        }

        private void TransactionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TransactionsListView.SelectedIndex != -1)
            {
                //fade out if needed
                DoubleAnimation da = new DoubleAnimation();
                da.From = 1;
                da.To = 0;
                da.Duration = new Duration(TimeSpan.FromSeconds(1));
                da.AutoReverse = false;

                //da.RepeatBehavior=new RepeatBehavior(3);
                TransactionDetailGroupbox.BeginAnimation(OpacityProperty, da);
                /*if(TransactionDetailGroupbox.Opacity == 100)
                {
                    while(TransactionDetailGroupbox.Opacity!=0)
                    {
                        TransactionDetailGroupbox.Opacity--;
                        Thread.Sleep(50);
                    }
                }*/




                //update the dialog
                string txID = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Id;
                TransactionLink.Inlines.Clear();
                TransactionLink.Inlines.Add(txID);
                DetailTimestampLBL.Content = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Timestamp;
                SenderLink.Inlines.Clear();
                SenderLink.Inlines.Add(eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Sender);
                RecipientLink.Inlines.Clear();
                RecipientLink.Inlines.Add(eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].AttachedRecipient);
                DetailAmountLbl.Content = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].AttachedAmount;
                DetailAttachLbl.Content = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Attachment;
                DetailTxType.Content = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Type;

                //fade in
                da = new DoubleAnimation();
                da.From = 0;
                da.To = 1;
                da.Duration = new Duration(TimeSpan.FromSeconds(1));
                da.AutoReverse = false;

                //da.RepeatBehavior=new RepeatBehavior(3);
                TransactionDetailGroupbox.BeginAnimation(OpacityProperty, da);
                /*if (TransactionDetailGroupbox.Opacity == 0)
                {
                    while (TransactionDetailGroupbox.Opacity != 100)
                    {
                        TransactionDetailGroupbox.Opacity++;
                        Thread.Sleep(50);
                    }
                }*/

            }


        }

        private async void ShowDebugMessage()
        {
            DebugViewBusy = true;

            while(debugViewQueue.Count>0)
            {
                DebugViewItem newItem = debugViewQueue.Dequeue();

                DebugViewerTB.Text = newItem.Message;

                if (!newItem.Redgreen) { DebugViewGroupBox.Foreground = Brushes.Red; DebugViewerTB.Foreground = Brushes.OrangeRed; }
                else { DebugViewGroupBox.Foreground = Brushes.Green; DebugViewerTB.Foreground = Brushes.LightGreen; }

                //fade in
                DoubleAnimation da = new DoubleAnimation();
                da.From = 0;
                da.To = 1;
                da.Duration = new Duration(TimeSpan.FromSeconds(1));
                da.AutoReverse = false;
                DebugViewGroupBox.BeginAnimation(OpacityProperty, da);

                await Task.Delay(2500);


                //fade out
                DoubleAnimation dc = new DoubleAnimation();
                dc.From = 1;
                dc.To = 0;
                dc.Duration = new Duration(TimeSpan.FromSeconds(1));
                dc.AutoReverse = false;
                DebugViewGroupBox.BeginAnimation(OpacityProperty, dc);

            }
            DebugViewBusy = false;

        }

        private void myEonWallet_Closing(object sender, CancelEventArgs e)
        {
            

            eonClient._eonThreadRun = false;
            eonClient.ShutDownFlag = true;
            ShutDownFlag = true;


        }

        private void CreateBackupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DebugMsg("Backup the wallet collection");

            //export the json file somewhere.
            SaveFileDialog jsonDg = new SaveFileDialog
            {
                Filter = "myEonWallet_backups.json files (*.json)|*.json",
                FileName = "myEonWallet_backup.json",
                Title = "Select a file to save a backup to...",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            try
            {
                if (jsonDg.ShowDialog() == true)
                {
                    string filePath = jsonDg.FileName;

                    try
                    {
                        eonClient.Wallets_Backup(filePath);
                    }
                    catch (Exception ex)
                    {
                        DebugMsg("Exception creating backup file: " + ex.Message);
                    }

                }
            }
            catch (Exception ex)
            {
                DebugMsg("Error exporting config file  : " + ex.Message + "\r\n");

            }


        }

        private void RestoreBackupMenuItem_Click(object sender, RoutedEventArgs e)
        {


            OpenFileDialog importDg = new OpenFileDialog
            {
                InitialDirectory = "c:\\",
                Title = "Select myEonWallet backup file to restore...",
                Filter = "myEonWallet_backup.json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };


            if (importDg.ShowDialog() == true)
            {
                try
                {
                    string filePath = importDg.FileName;

                    //restore the walletlist
                    eonClient.Wallets_Restore(filePath);

                }
                catch(Exception ex)
                {
                    DebugMsg("Exception during restore" + ex.Message);
                }
            }

        }

        private void ResetConfigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DebugMsg("Reset the wallet collection");

            MessageBoxResult result = MessageBox.Show("WARNING - you are about to DELETE all the stored wallet information. Make sure you have a backup before you proceed", "Reset wallet data", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            switch (result)
            {
                case MessageBoxResult.OK:

                    eonClient.Wallets_Reset();
                    eonClient.TransactionHistory.SummaryTransactionCollection.Clear();
                    SelectedAccountAddress_LBL.Content = "-";
                    break;

                case MessageBoxResult.Cancel:
                    
                    break;
            }

            



        }

        private void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {

            //if primary needs setting up
            if (eonClient.Wallets_GetCount() < 1)
            {

                //create new primary wallet
                //WalletClass primaryWallet = eonClient.CreateAccount("Primary");

                //eonClient.Wallets_Add(primaryWallet);
                //SaveWalletList();

                //alert user to register the new primary account
                NewPrimaryAccountDialog nW = new NewPrimaryAccountDialog(eonClient);
                //nW.AccountID_TB.Text = primaryWallet.AccountID;
                //nW.PublicKey_TB.Text = primaryWallet.PublicKey;
                //nW.Seed_TB.Text = primaryWallet.Seed;
                nW.ShowDialog();

            }
            //else set up a new secondary wallet
            else if (eonClient.Wallets_GetCount() >= 1)
            {
                //show the dialog
                nAC = new NewAttachedAccountDialog(eonClient);
                nAC.NewWalletEvent += (eventsender, wal) =>
                {
                    Dispatcher.Invoke(() => AddNewWallet_Callback(wal));
                };
                nAC.ShowDialog();
            }

        }

        private void AddNewWallet_Callback(EonSharp.Wallet wal)
        {
            //add this new wallet to the wallet list and update the display
            try
            {
                eonClient.Wallets_Add (wal);

                DebugMsg("New Account " + wal.AccountDetails.AccountId + " stored to walletlist");
                nAC.Topmost = true;
            }
            catch (Exception ex)
            {
                DebugMsg("Exception saving new user wallet : " + ex.Message);
            }

        }

        private async void DepositButton_Click(object sender, RoutedEventArgs e)
        {
            decimal amount = 0;

            DepositConfirm dConfirm = new DepositConfirm("Adjust the deposit balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + "?\r\n\r\n1. Enter the new deposit amount below\r\n2. Supply the password for your encrypted wallet\r\n3. Press YES to confirm and place this transaction on the EON blockchain");
            dConfirm.DepositAmountTB.Text = ((decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit / 1000000).ToString();

            if ((bool)dConfirm.ShowDialog())
            {

                try
                {
                    amount = decimal.Parse(dConfirm.DepositAmountTB.Text);
                    RpcResponseClass RpcResult = await eonClient.Transaction_SetDeposit(AccountListView.SelectedIndex, amount, dConfirm.walletPasswordBox.Password);

                    if (RpcResult.Result)
                    {
                        DebugMsg("Transaction SUCCESS -  Allocate " + amount + " EON to the deposit balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId);
                    }
                    else
                    {
                        ErrorMsg("Transaction FAILED - Allocate " + amount + " EON to deposit account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " - " + RpcResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    ErrorMsg("Transaction FAILED - Change Deposit to " + amount + " EON failed for account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " [Exception] - " + ex.Message);

                }

            }

                /*if (decimal.TryParse(amountTB.Text, out amount))
                {
                    MsgBoxYesNo msgbox = new MsgBoxYesNo("Refill " + amount + " EON to the deposit balance of account "+ eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + "? Press YES to confirm and place this transaction on the EON blockchain");
                    if ((bool)msgbox.ShowDialog())
                    {
                        try
                        {
                            RpcResponseClass RpcResult = await eonClient.Transaction_Refill(AccountListView.SelectedIndex, 1000000 * amount, "DUMMYPASSWORD");
                            if (RpcResult.Result)
                            {
                                DebugMsg("Transaction SUCCESS -  Refilled " + amount + " EON to the deposit balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id);
                            }
                            else
                            {
                                ErrorMsg("Transaction FAILED - Refill of " + amount + " EON to deposit account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + " - " + RpcResult.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorMsg("Transaction FAILED - Refill of " + amount + " EON to deposit account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + " [Exception] - " + ex.Message);

                        }
                    }
                    else
                    {
                        //cancelled
                    }



                }
                else
                {
                    ErrorMsg("Error parsing the AMOUNT. Correct before retrying.");
                }*/

            }

        private async void WithdrawButton_Click(object sender, RoutedEventArgs e)
        {
            decimal amount = 0;

            /*if (decimal.TryParse(amountTB.Text, out amount))
            {

                MsgBoxYesNo msgbox = new MsgBoxYesNo("Withdraw " + amount + " EON from the deposit balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + "? Press YES to confirm and place this transaction on the EON blockchain");
                if ((bool)msgbox.ShowDialog())
                {


                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_Withdraw(AccountListView.SelectedIndex, 1000000 * amount, "DUMMYPASSWORD");
                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS - Withdrew " + amount + " EON from deposit balance to main balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id);
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Withdrawal of " + amount + " EON from deposit account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + " - " + RpcResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Withdrawal of " + amount + " EON from deposit account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Id + " [Exception] - " + ex.Message);
                    }
                }
                else
                {
                    //cancelled
                }
            }
            else
            {
                ErrorMsg("Error parsing the AMOUNT. Correct before retrying.");
            }*/

        }

        private void TransactionsListViewRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            DebugMsg("Transactions refresh!");
            eonClient.GetTransactions(AccountListView.SelectedIndex, Properties.Settings.Default.TransactionHistoryMax/20);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TransactionSummaryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        //copy the rx address to clipboard
        private void AddressCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = (string)SelectedAccountAddress_LBL.Content;

            Clipboard.SetText(iText);
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            //eonClient.WalletManager.RemoveWallet(2);
            //eonClient.eonClientCore.testTX();
            //eonClient.UpdateTransactionSummary(AccountListView.SelectedIndex);
            //eonClient.eonClientCore.GetInformation(eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountID);
            // DebugInfo.DebugString = "TEST!!!";
            // EonSharp.Api.Info accountInfo = await eonClient.Bot.Accounts.GetInformation("EON-QWZVC-W6HDQ-WQ4BU");
            //await getInfoAsync("EON-RTQJW-AND3F-GA8JR");

            // var accountInfo = await Bot.Accounts.GetStateAsync("EON-RTQJW-AND3F-GA8JR");

            MsgBoxYesNo msgbox = new MsgBoxYesNo("You are about to send 5 EON from EON-QWZVC-W6HDQ-WQ4BU to EON-QWZVC-W6HDQ-WQ4BU.  Press YES to confirm and place this transaction on the EON blockchain");
            if ((bool)msgbox.ShowDialog())
            {
                MessageBox.Show("Transaction send : Success");
            }
            else
            {
                MessageBox.Show("Transaction send : Fail");
            }


        }

        private void DefaultPeerButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            eonClient.coreConfig.Peer = "https://peer.testnet.eontechnology.org:9443";
        }

        //link click
        private void GridViewColumn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.google.com");
        }


        private void TransactionLink_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionsListView.SelectedIndex != -1)
            {
                string txID = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Id;

                string uri = "https://testnet.eontechnology.org/browser/#/transaction/" + txID;

                Process.Start(uri);
            }
        }

        private void SenderLink_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionsListView.SelectedIndex != -1)
            {
                string senderID = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].Sender;

                string uri = "https://testnet.eontechnology.org/browser/#/address/" + senderID;

                Process.Start(uri);
            }
        }

        private void RecipientLink_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionsListView.SelectedIndex != -1)
            {
                string recipientID = eonClient.TransactionHistory.ConfirmedTransactionCollection[TransactionsListView.SelectedIndex].AttachedRecipient;

                string uri = "https://testnet.eontechnology.org/browser/#/address/" + recipientID;

                Process.Start(uri);
            }
        }


        private class DebugViewItem
        {
            public string Message;
            public bool Redgreen;

            public DebugViewItem()
            {
                Message = "";
                Redgreen = false;
            }
            public DebugViewItem(string message, bool redgreen)
            {
                Message = message;
                Redgreen = redgreen;
            }
        }

        //Help-About menu item
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog aW = new AboutDialog();
            aW.ShowDialog();

        }

        //Help-Links menu item
        private void LinksMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LinkTestnetRegMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://testnet.eontechnology.org/");
        }

        private void LinkEonWikiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://eondev-wiki.info/doku.php");            
        }

        private void LinkExscudoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://exscudo.com/");
        }

        private void LinkEonTechMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://eontechnology.org/");
        }


        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        
    }

    //used to convert MEON to EON during xaml binding
    public class MEONToEONConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            decimal result = decimal.Parse(value.ToString()) / 1000000;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            long result = (long)((decimal)value * 1000000);
            return result;
        }
    }

}



