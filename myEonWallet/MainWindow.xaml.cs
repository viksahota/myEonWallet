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
using EonSharp.Api;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;

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

        private Transaction ImportedMSMTX;

        private Queue<DebugViewItem> debugViewQueue;

        public bool Debug_Checked
        { get; set; }

        public bool ErrorEnable_Checked
        { get; set; }

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
            EnableErrorsCheckBox.DataContext = eonClient;

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


            ColorCoinControlBoxes_HideAll();

            MessageBoxResult result = new MessageBoxResult();

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
            AccountListView.ItemsSource = eonClient.WalletManager.WalletCollection;
            TransactionsListView.ItemsSource = eonClient.TransactionHistory.ConfirmedTransactionCollection;
            TransactionSummaryListView.ItemsSource = eonClient.TransactionHistory.SummaryTransactionCollection;
            AccountListView_SelectionChanged(null, null);

            ColorCoinListView.SelectedIndex = 0;
            //ColorCoinListView.ItemsSource = eonClient.WalletManager.WalletCollection


        }

        private void PeerValidateHandler(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (msg == "VALID") Config_PeerAddressTB.Foreground = new SolidColorBrush(Colors.LightGreen);
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

        //updates the main balance and deposit labels and the color coin status
        private async void UpdateBalanceDeposit(int accountIndex)
        {
            if (accountIndex != -1)
            {

                if (eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information != null)
                {

                    //update the balances on the main page
                    BalanceLBL.Content = (decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Amount / 1000000 + " EON";
                    DepositLBL.Content = (decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit / 1000000 + " EON";
                    TotalEON_LBL.Content = (decimal)(eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Amount + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit) / 1000000 + " EON";


                    //update color coin balances
                    if (eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin != null)
                    {
                        ColorCoinTypeID_LBL.Content = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin;
                        var ccInfo = await eonClient.eonSharpClient.Bot.ColoredCoin.GetInfo(eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin);

                        ColorCoinStatusStatus_LBL.Content = ccInfo.State.Name;
                        if (ccInfo.State.Name == "OK")
                        {
                            ColorCoinStatusEmission_LBL.Content = ccInfo.Supply;
                            ColorCoinStatusDecimals_LBL.Content = ccInfo.Decimal;
                            
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();

                            EonSharp.Api.Balance balanceObject = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Balance;

                            foreach (var ccoin in balanceObject.ColoredCoins)
                            {
                                //if this color coin is created by this account, then show the balance in the creator balance
                                if (ccoin.Key == eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin)
                                {
                                    ColorCoinStatusBalance_LBL.Content = ccoin.Value;
                                }
                            }

                            var ccoins = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Balance.ColoredCoins;
                            if (ccoins.Count>0) ColorCoinListView.ItemsSource = ccoins;


                        }
                        else
                        {

                        }

                        //ccInfo.State

                    }
                    else
                    {
                        ColorCoinTypeID_LBL.Content = "--- color coin undefined for this account yet ---";

                        ColorCoinStatusEmission_LBL.Content = "-";
                        ColorCoinStatusDecimals_LBL.Content = "-";
                        ColorCoinStatusBalance_LBL.Content = "-";
                        ColorCoinStatusStatus_LBL.Content = "-";

                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCreateCoinButton();

                    }


                    //display multisig votingrights info
                    VotingRights vr = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.VotingRights;
                    if (vr != null)
                    {
                        VotingListView.ItemsSource = vr.Delegates;
                    }
                    else VotingListView.ItemsSource = null;

                    /*
                    //display quorum
                    Quorum qr = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Quorum;
                    if( qr!=null )
                    {
                        int quorumAll = qr.quorum;

                        //set all quorum types to ALL value
                        QuorumAll_Slider.Value = qr.quorum;
                        QuorumAccountReg_Slider.Value = qr.quorum;
                        QuorumPayment_Slider.Value = qr.quorum;
                        QuorumDepositChange_Slider.Value = qr.quorum;
                        QuorumDelegate_Slider.Value = qr.quorum;
                        QuorumQuorum_Slider.Value = qr.quorum;
                        QuorumRejection_Slider.Value = qr.quorum;
                        QuorumAccountPublication_Slider.Value = qr.quorum;

                        //set individual quorums
                        if (qr.QuorumByTypes != null)
                        {
                            foreach (var qt in qr.QuorumByTypes)
                            {
                                switch (qt.Key)
                                {
                                    case (100)://account registration
                                        QuorumAccountReg_Slider.Value = qt.Value;
                                        break;
                                    case (200)://ordinary payment
                                        QuorumPayment_Slider.Value = qt.Value;
                                        break;
                                    case (300)://Deposit change
                                        QuorumDepositChange_Slider.Value = qt.Value;
                                        break;
                                    case (400)://Quorum
                                        QuorumQuorum_Slider.Value = qt.Value;
                                        break;
                                    case (425)://Delegate
                                        QuorumDelegate_Slider.Value = qt.Value;
                                        break;
                                    case (450)://Rejection
                                        QuorumRejection_Slider.Value = qt.Value;
                                        break;
                                    case (475)://Account Publication
                                        QuorumAccountPublication_Slider.Value = qt.Value;
                                        break;
                                }
                            }
                        }
                    }*/


                    VoterListView.ItemsSource = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Voter;


                }
            }
            else
            {
                BalanceLBL.Content = "-";
                DepositLBL.Content = "-";
                TotalEON_LBL.Content = "-";
            }
        }

        private void ColorCoinControl_ShowCreateCoinDialog()
        {
            ColorCoinCreate_GroupBox.Margin = new Thickness(529, 17, 0, 0);
            ColorCoinCreate_GroupBox.IsEnabled = true;
            ColorCoinCreate_GroupBox.Visibility = Visibility.Visible;
        }

        private void ColorCoinControl_ShowCoinControls()
        {
            ColorCoinControls_GroupBox.IsEnabled = true;
            ColorCoinControls_GroupBox.Visibility = Visibility.Visible;
        }

        private void ColorCoinControl_ShowCreateCoinButton()
        {
            ColorCoinControlsCreate_GroupBox.Margin = new Thickness(529, 17, 0, 0);
            ColorCoinControlsCreate_GroupBox.IsEnabled = true;
            ColorCoinControlsCreate_GroupBox.Visibility = Visibility.Visible;
        }

        private void ColorCoinControl_ShowSendCoinDialog()
        {
            ColorCoinSend_GroupBox.Margin = new Thickness(529, 17, 0, 0);
            ColorCoinSend_GroupBox.IsEnabled = true;
            ColorCoinSend_GroupBox.Visibility = Visibility.Visible;
        }

        private void ColorCoinControl_ShowModifyDialog()
        {
            ColorCoinSupply_GroupBox.Margin = new Thickness(529, 17, 0, 0);
            ColorCoinSupply_GroupBox.IsEnabled = true;
            ColorCoinSupply_GroupBox.Visibility = Visibility.Visible;
        }


        private void ColorCoinControlBoxes_HideAll()
        {
            ColorCoinCreate_GroupBox.IsEnabled = false;
            ColorCoinCreate_GroupBox.Visibility = Visibility.Hidden;
            ColorCoinSend_GroupBox.IsEnabled = false;
            ColorCoinSend_GroupBox.Visibility = Visibility.Hidden;
            ColorCoinSupply_GroupBox.IsEnabled = false;
            ColorCoinSupply_GroupBox.Visibility = Visibility.Hidden;
            ColorCoinControls_GroupBox.IsEnabled = false;
            ColorCoinControls_GroupBox.Visibility = Visibility.Hidden;
            ColorCoinControlsCreate_GroupBox.IsEnabled = false;
            ColorCoinControlsCreate_GroupBox.Visibility = Visibility.Hidden;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            decimal amount = 0;

            //regex check the destination address
            string pattern = @"(EON)-([^-]{5})-([^-]{5})-([^-]{5})";
            Match accountMatch = Regex.Match(RecipientTB.Text, pattern);

            if (decimal.TryParse(SendAmountTB.Text, out amount) && (accountMatch.Groups.Count==5))
            {


                DepositConfirm dConfirm = new DepositConfirm("Send " + amount + " EON from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to account " + RecipientTB.Text +" ?\r\n\r\n1. (optional) Add a private note to this transaction\r\n2. Supply the password for your encrypted wallet\r\n3. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {

                        RpcResponseClass RpcResult = await eonClient.Transaction_SendPayment(AccountListView.SelectedIndex, RecipientTB.Text, 1000000 * amount, dConfirm.walletPasswordBox.Password, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);
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

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);


                    }

                }
                else
                {
                    //cancelled
                }

            }
            else
            {
                ErrorMsg("Error parsing the Amount or Recipient AccountID. Correct before retrying.");
            }
        }

        private void MSMTransactionCheckAndExport(Exception ex)
        {
            //detect multisig send the doesnt reach quorum - we need to export the transaction
            string response = ((EonSharp.Protocol.ProtocolException)ex).JsonRpcResponse;

            if (response.Contains("The quorum is not exist"))
            {
                string request = ((EonSharp.Protocol.ProtocolException)ex).JsonRpcRequest;
                string txRequestPattern = @"params"":\[([^]]*)";
                Match requestMatch = Regex.Match(request, txRequestPattern);

                string transaction = "";

                if (requestMatch.Groups.Count == 2) transaction = requestMatch.Groups[1].Value;


                DebugMsg("MSM transaction did not reach quorum - exporting...");

                MSMTXDialog msmDialog = new MSMTXDialog("MSM Transaction detected\r\n\r\nThis transaction needs to be signed by more delegates to acheive quorem. Export this transaction to a file now ?", "");
                if ((bool)msmDialog.ShowDialog())
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Title = "Export your signed multisig transaction to file to share with delegates....";
                    if (saveFileDialog.ShowDialog() == true) File.WriteAllText(saveFileDialog.FileName, transaction);
                }

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
                if (AccountListView.SelectedIndex < eonClient.WalletManager.WalletCollection.Count) eonClient.UpdateTransactionSummary(AccountListView.SelectedIndex);
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
                TransactionDetailGroupbox.BeginAnimation(OpacityProperty, da);
            }


        }

        private async void ShowDebugMessage()
        {
            DebugViewBusy = true;

            while (debugViewQueue.Count > 0)
            {
                DebugViewItem newItem = debugViewQueue.Dequeue();

                DebugViewerTB.Text = newItem.Message;

                if (!newItem.Redgreen) { DebugViewGroupBox.Foreground = Brushes.Red; DebugViewerTB.Foreground = Brushes.OrangeRed; }
                else { DebugViewGroupBox.Foreground = Brushes.Green; DebugViewerTB.Foreground = Brushes.LightGreen; }

                //fade in
                DebugViewGroupBox.Visibility = Visibility.Visible;
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
                DebugViewGroupBox.Visibility = Visibility.Hidden;
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

                    AccountListView.SelectedIndex = 0;

                }
                catch (Exception ex)
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
                NewPrimaryAccountDialog nW = new NewPrimaryAccountDialog(eonClient);
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
                eonClient.Wallets_Add(wal);

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
            if (AccountListView.SelectedIndex != -1)
            {
                decimal amount = 0;

                DepositConfirm dConfirm = new DepositConfirm("Adjust the deposit balance of account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " ?\r\n\r\n1. Enter the new deposit amount below\r\n2. Supply the password for your encrypted wallet\r\n3. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.DepositAmountTB.Text = ((decimal)eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Deposit / 1000000).ToString();

                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        amount = decimal.Parse(dConfirm.DepositAmountTB.Text);
                        RpcResponseClass RpcResult = await eonClient.Transaction_SetDeposit(AccountListView.SelectedIndex, amount, dConfirm.walletPasswordBox.Password, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

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

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }
            }

        }

        private void TransactionsListViewRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            //DebugMsg("Transactions view refreshed");
            eonClient.GetTransactions(AccountListView.SelectedIndex, Properties.Settings.Default.TransactionHistoryMax / 20);
        }

        //copy the rx address to clipboard
        private void AddressCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string iText = (string)SelectedAccountAddress_LBL.Content;
            Clipboard.SetText(iText);
        }

        private void TestnetPeerButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            eonClient.coreConfig.Peer = "https://peer.testnet.eontechnology.org:9443";
        }

        private void MainnetPeerButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            eonClient.coreConfig.Peer = "https://peer.eontechnology.org:9443";
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

        //user changed the slider for colored coin decimal places
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                int decVal = (int)ColoredCoinDecimalsSlider.Value;
                ColorCoinDecimalPlaces_LBL.Content = decVal;
                ColorCoinAtomicValue_LBL.Content = (decimal)(1 / (Math.Pow(10, (double)decVal)));
                if (decVal > 4) ColorCoinAtomicValue_LBL.Content += "  (" + (1 / (Math.Pow(10, (double)decVal))) + " )";
            }
            catch { }
        }

        private async void CreateColorCoin_BTN_Click(object sender, RoutedEventArgs e)
        {
            long amount = 0;
            if ((long.TryParse(ColorCoinEmissionAmount_TB.Text, out amount)) && ((AccountListView.SelectedIndex != -1)))
            {
                DepositConfirm dConfirm = new DepositConfirm("Create color coin from: " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);

                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_ColorCoinRegistration(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, amount, (int)ColoredCoinDecimalsSlider.Value, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS -  Account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " created color coin with " + (int)ColoredCoinDecimalsSlider.Value + " decimals and emission of " + amount + ")");

                            ColorCoinControlBoxes_HideAll();

                            ColorCoinTypeID_LBL.Content = "--- WAIT FOR TRANSACTION TO CONFIRM!! ---";
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Color coin registration failure : " + RpcResult.Message);

                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during color coin registration : " + ex.Message);

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }

            }
            else
            {

            }
        }

        //send a color coin payment
        private async void ColorCoinPayment_BTN_Click(object sender, RoutedEventArgs e)
        {
            long amount = 0;

            //regex check the destination address input
            string pattern = @"(EON)-([^-]{5})-([^-]{5})-([^-]{5})";
            Match accountMatch = Regex.Match(ColorCoinSendRecipient_TB.Text, pattern);

            //regex check the color coin type input
            string pattern2 = @"(EON-C)-([^-]{5})-([^-]{5})-([^-]{5})";
            Match coinIdentMatch = Regex.Match(ColorCoinTypeID_TB.Text, pattern2);


            if ((long.TryParse(ColorCoinSendAmount_TB.Text, out amount)) && ((AccountListView.SelectedIndex != -1)) && (accountMatch.Groups.Count==5)  && (coinIdentMatch.Groups.Count==5))
            {
                DepositConfirm dConfirm = new DepositConfirm("Send color coin [ " + amount + " of " + ColorCoinTypeID_TB.Text + " ] from: " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to " + ColorCoinSendRecipient_TB.Text + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_ColorCoinPayment(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, amount, ColorCoinSendRecipient_TB.Text, ColorCoinTypeID_TB.Text, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS -  Sent " + amount + " " + ColorCoinTypeID_TB.Text + " color coins from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to " + ColorCoinSendRecipient_TB.Text);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Color coin send failure : " + RpcResult.Message);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during color coin send : " + ex.Message);
                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCoinControls();

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }

            }
            else
            {
                ErrorMsg("Color Coin Send error : invalid format detected in amount, recipient or coin ident. Correct before retrying");
            }
        }

        //change the color coin supply
        private async void ColorCoinSupplyModify_BTN_Click(object sender, RoutedEventArgs e)
        {
            long amount = 0;

            if ((long.TryParse(ColorCoinSupplyAmount_TB.Text, out amount)) && ((AccountListView.SelectedIndex != -1)))
            {
                DepositConfirm dConfirm = new DepositConfirm("Change the total supply of color coin " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin + " to " + amount + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_ColorCoinSupply(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, amount, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS -  Changed color coin " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin + " total supply to " + amount);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Color coin supply change : " + RpcResult.Message);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during color coin supply change : " + ex.Message);
                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCoinControls();

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }

            }
            else
            {
            }

        }

        private void CreateColorCoinMenuButton_Click(object sender, RoutedEventArgs e)
        {
            //show the create color coin dialog
            ColorCoinControlBoxes_HideAll();
            ColorCoinControl_ShowCreateCoinDialog();
        }


        //send creators color coin
        private void SendColorCoinMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ColorCoinControlBoxes_HideAll();
            ColorCoinControl_ShowSendCoinDialog();
        }

        //modify color coin total supply
        private void ModifyColorCoinMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ColorCoinControlBoxes_HideAll();
            ColorCoinControl_ShowModifyDialog();
        }

        //destroy color coin
        private async void DestroyColorCoinMenuButton_Click(object sender, RoutedEventArgs e)
        {
            DepositConfirm dConfirm = new DepositConfirm("Destroy color coin " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.ColoredCoin + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
            dConfirm.ShowDepositFields(false);
            if ((bool)dConfirm.ShowDialog())
            {
                try
                {
                    RpcResponseClass RpcResult = await eonClient.Transaction_ColorCoinDestroy(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                    if (RpcResult.Result)
                    {
                        DebugMsg("Transaction SUCCESS -  Destroyed Color Coin");
                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCoinControls();
                    }
                    else
                    {
                        ErrorMsg("Transaction FAILED - Color Coin destroy failed : " + RpcResult.Message);
                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCoinControls();
                    }
                }
                catch (Exception ex)
                {
                    ErrorMsg("Transaction FAILED - Exception during Color Coin destroy : " + ex.Message);
                    ColorCoinControlBoxes_HideAll();
                    ColorCoinControl_ShowCoinControls();

                    //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                    MSMTransactionCheckAndExport(ex);

                }

                ColorCoinControlBoxes_HideAll();
            }
        }

        //cancel out of create color coin dialog
        private void CreateColorCoinCancel_BTN_Click(object sender, RoutedEventArgs e)
        {
            UpdateBalanceDeposit(AccountListView.SelectedIndex);
        }

        //cancel out of color coin supply adjustment dialog
        private void ColorCoinSupplyModifyCancel_BTN_Click(object sender, RoutedEventArgs e)
        {
            UpdateBalanceDeposit(AccountListView.SelectedIndex);
        }

        //cancel out of sending a color coin
        private void ColorCoinPaymentCancel_BTN_Click(object sender, RoutedEventArgs e)
        {
            UpdateBalanceDeposit(AccountListView.SelectedIndex);
        }

        //user changes selected item in the color coin list view
        private void ColorCoinListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        //send color coins from selected color coin balance
        private async void SendColorCoinsButton_Click(object sender, RoutedEventArgs e)
        {
            long amount = 0;

            //regex check the destination address
            string pattern = @"(EON)-([^-]{5})-([^-]{5})-([^-]{5})";
            Match accountMatch = Regex.Match(CCRecipientTB.Text, pattern);

            if ((long.TryParse(CCSendAmountTB.Text, out amount)) && (AccountListView.SelectedIndex != -1) && (ColorCoinListView.SelectedIndex != -1) && (accountMatch.Groups.Count==5))
            {

                var coins = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Balance.ColoredCoins;
                var coinIdent = coins.ToArray()[ColorCoinListView.SelectedIndex].Key;


                DepositConfirm dConfirm = new DepositConfirm("Send color coin [ " + amount + " of " + coinIdent + " ] from: " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to " + CCRecipientTB.Text + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_ColorCoinPayment(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, amount, CCRecipientTB.Text, coinIdent, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS -  Sent " + amount + " " + coinIdent + " color coins from " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to " + CCRecipientTB.Text);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Color coin send failure : " + RpcResult.Message);
                            ColorCoinControlBoxes_HideAll();
                            ColorCoinControl_ShowCoinControls();
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during color coin send : " + ex.Message);
                        ColorCoinControlBoxes_HideAll();
                        ColorCoinControl_ShowCoinControls();

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }

            }
            else
            {
                ErrorMsg("Send failure : Error in input - check the amount and recipient address are valid");
            }

        }

        private void DelegateSelf_BTN_Click(object sender, RoutedEventArgs e)
        {
            DelagationRecipientTB.Text = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId;
        }

        private async void DelegateBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int amount = int.Parse(DelegationAmountTB.Text);
                string address = DelagationRecipientTB.Text;

                DepositConfirm dConfirm = new DepositConfirm("Delegate " + amount + "%  of priveleges of " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + "  TO : " + address + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {

                    RpcResponseClass RpcResult = await eonClient.Transaction_MultiSigDelegate(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, address, amount, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                    if (RpcResult.Result)
                    {
                        DebugMsg("Transaction SUCCESS -  Delegated " + amount + "%  of priveleges of " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + "  TO : " + address);
                    }
                    else
                    {
                        ErrorMsg("Transaction FAILED - Set Delegate failure : " + RpcResult.Message);
                    }
                }
                else
                {
                }
            }

            catch (Exception ex)
            {
                ErrorMsg("Transaction FAILED - Exception during Set Delegate : " + ex.Message);

                //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                MSMTransactionCheckAndExport(ex);
            }

        }

        private async void QuorumBTN_Click(object sender, RoutedEventArgs e)
        {
            //get the current quorum object
            Quorum qr = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Quorum;

            //set quorum all if enabled
            int quorumAll = 100;
            if ((bool)CheckBox_QuorumAll.IsChecked) quorumAll = (int)QuorumAll_Slider.Value;

            IDictionary<int, int> qTypes = new Dictionary<int, int>();

            //if sliders are in positions other than = ALL, then add them to the qTypes Dictionary
            if ((bool)CheckBox_QuorumAccountReg.IsChecked)
            {
                qTypes.Add(100, (int)QuorumAccountReg_Slider.Value);
            }
            if ((bool)CheckBox_QuorumPayment.IsChecked)
            {
                qTypes.Add(200, (int)QuorumPayment_Slider.Value);
            }
            if ((bool)CheckBox_QuorumDeposit.IsChecked)
            {
                qTypes.Add(300, (int)QuorumDepositChange_Slider.Value);
            }
            if ((bool)CheckBox_QuorumQuorum.IsChecked)
            {
                qTypes.Add(400, (int)QuorumQuorum_Slider.Value);
            }

            if ((bool)CheckBox_QuorumDelegate.IsChecked)
            {
                qTypes.Add(425, (int)QuorumDelegate_Slider.Value);
            }
            if ((bool)CheckBox_QuorumRejection.IsChecked)
            {
                qTypes.Add(450, (int)QuorumRejection_Slider.Value);
            }
            if ((bool)CheckBox_QuorumAccountPublication.IsChecked)
            {
                qTypes.Add(475, (int)QuorumAccountPublication_Slider.Value);
            }



            DepositConfirm dConfirm = new DepositConfirm("Set Quorum to defined values ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
            dConfirm.ShowDepositFields(false);
            if ((bool)dConfirm.ShowDialog())
            {
                try
                {
                    RpcResponseClass RpcResult = await eonClient.Transaction_MultiSigQuorum(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, quorumAll, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked, qTypes);

                    if (RpcResult.Result)
                    {
                        DebugMsg("Transaction SUCCESS -  Quorum configured for account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId);
                    }
                    else
                    {
                        ErrorMsg("Transaction FAILED - Quorum configuration failure : " + RpcResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    ErrorMsg("Transaction FAILED - Exception during Set Quorum : " + ex.Message);

                    //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                    MSMTransactionCheckAndExport(ex);

                }
            }
            else
            {
            }

        }

        private async void MultiSigRejectBTN_Click(object sender, RoutedEventArgs e)
        {
            KeyValuePair<string, int> item;
            string rejectedAddress = "";
            int weight;

            try
            {
                item = (KeyValuePair<string, int>)VoterListView.SelectedItem;
                rejectedAddress = item.Key;
                weight = item.Value;

                DepositConfirm dConfirm = new DepositConfirm("Reject all voting-rights to the account " + rejectedAddress + " ?\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_MultiSigRejection(AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password, rejectedAddress, dConfirm.TransactionNoteTB.Text, (bool)dConfirm.NoteEncryptionCheckBox.IsChecked);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS -  Rejected voting-rights of user account " + eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].AccountDetails.AccountId + " to remote account : " + rejectedAddress);
                        }
                        else
                        {
                            ErrorMsg("Transaction FAILED - Multisig-Rejection failed : " + RpcResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during Multisig-Rejection : " + ex.Message);

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }
                }
                else
                {
                }


            }
            catch(Exception ex)
            {
                ErrorMsg("Multisig-Rejection error : Voter item not selected!");
            }

           


        }

        private void MSMImportTransactionBTN_Click(object sender, RoutedEventArgs e)
        {
            
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string importedTX = File.ReadAllText(openFileDialog.FileName);

                //show a summary of the transaction for review prior to signing


                var tx = (Transaction)importedTX;

                MSMSummaryTB.Text = GetSummaryTX(tx);

                ImportedMSMTX = tx;
                
                MultiSigSignBTN.Visibility = Visibility.Visible;
                MultiSigSignCancelBTN.Visibility = Visibility.Visible;
                MSMSendLBL1.Visibility = Visibility.Visible;
                MultiSigImportFileBTN.Visibility = Visibility.Hidden;


            }

        }
    

    private string GetSummaryTX(Transaction tx)
    {

            string summary = "";

            switch(tx.Attachment.GetType().Name)
            {
                case ("PaymentAttachment"):
                    EonSharp.Api.Transactions.Attachments.PaymentAttachment txPay = (EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment;
                    summary = "Payment of " + txPay.Amount / 1000000 + " EON from Account ID:" + tx.Sender + " to AccountID: " + txPay.Recipient;
                    break;

                case ("DepositAttachment"):
                    EonSharp.Api.Transactions.Attachments.DepositAttachment txDep = (EonSharp.Api.Transactions.Attachments.DepositAttachment)tx.Attachment;
                    summary = "Account ID:" + tx.Sender + " change Deposit to " + txDep.Amount / 1000000 + " EON";
                    break;

                case ("RegistrationAttachment"):
                    EonSharp.Api.Transactions.Attachments.RegistrationAttachment txReg = (EonSharp.Api.Transactions.Attachments.RegistrationAttachment)tx.Attachment;
                    summary = "Account ID:" + tx.Sender + " register a new EON account : " + txReg.AccountId;
                    break;

                case ("ColoredCoinRegistrationAttachment"):
                    EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment txCCReg = (EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment)tx.Attachment;
                    summary = "Account ID:" + tx.Sender + " register Color-Coin with emission:" + txCCReg.Emission + " and " + txCCReg.Decimal + " decimals";
                    break;

                case ("ColoredCoinPaymentAttachment"):
                    EonSharp.Api.Transactions.Attachments.ColoredCoinPaymentAttachment txCCPay = (EonSharp.Api.Transactions.Attachments.ColoredCoinPaymentAttachment)tx.Attachment;
                    summary = "Pay " + txCCPay.Amount + " color coins [" + txCCPay.Color + "] from Account ID:" + tx.Sender + " to AccountID: " + txCCPay.Recipient;
                    break;

                case ("DelegateAttachment"):
                    EonSharp.Api.Transactions.Attachments.DelegateAttachment txDel = (EonSharp.Api.Transactions.Attachments.DelegateAttachment)tx.Attachment;
                    summary = "Delegation of " + txDel.Weight + "% voting rights of Account ID:" + tx.Sender + "to Account ID:" + txDel.Account;
                    break;

                case ("QuorumAttachment"):
                    EonSharp.Api.Transactions.Attachments.QuorumAttachment txQor = (EonSharp.Api.Transactions.Attachments.QuorumAttachment)tx.Attachment;

                    string qSummary = "Account ID :" + tx.Sender + " : set Account-Quorum to [";
                    qSummary += " All:" + txQor.All;
                    foreach (System.Collections.Generic.KeyValuePair<int,int> iD in txQor.Types)
                    {
                        qSummary += (" " + iD.Key + ":" + iD.Value);
                    }
                    qSummary += "]";
                    summary = qSummary;
                    break;

                case ("RejectionAttachment"):
                    EonSharp.Api.Transactions.Attachments.RejectionAttachment txRej = (EonSharp.Api.Transactions.Attachments.RejectionAttachment)tx.Attachment;
                    summary = "Account ID:" + tx.Sender + " : reject voting rights to EON account : " + txRej.Account;
                    break;

                case ("ColoredCoinSupplyAttachment"):
                    EonSharp.Api.Transactions.Attachments.ColoredCoinSupplyAttachment txCCSupply = (EonSharp.Api.Transactions.Attachments.ColoredCoinSupplyAttachment)tx.Attachment;
                    summary = "Account ID:" + tx.Sender + " : change color coin supply to " + txCCSupply.Supply;
                    break;

                default:
                    break;

            }



            return summary;
    }


        //cancel signing a multisig transaction
        private void MultiSigSignCancelBTN_Click(object sender, RoutedEventArgs e)
        {
            MultiSigSignBTN.Visibility = Visibility.Hidden;
            MultiSigSignCancelBTN.Visibility = Visibility.Hidden;
            MSMSendLBL1.Visibility = Visibility.Hidden;
            MultiSigImportFileBTN.Visibility = Visibility.Visible;
            ImportedMSMTX = null;
            MSMSummaryTB.Text = "";
        }

        //sign and send the selected multisig transaction
        private async void MultiSigSignBTN_Click(object sender, RoutedEventArgs e)
        {

            try
            {


                Wallet wal = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex];

            if (ImportedMSMTX!=null)
            {

                DepositConfirm dConfirm = new DepositConfirm(MSMSummaryTB.Text + "\r\n\r\n1. Supply the password for your encrypted wallet\r\n2. Press YES to confirm and place this transaction on the EON blockchain");
                dConfirm.ShowDepositFields(false);
                if ((bool)dConfirm.ShowDialog())
                {
                    try
                    {
                        RpcResponseClass RpcResult = await eonClient.Transaction_SignMSM(ImportedMSMTX, AccountListView.SelectedIndex, dConfirm.walletPasswordBox.Password);

                        if (RpcResult.Result)
                        {
                            DebugMsg("Transaction SUCCESS - " + MSMSummaryTB.Text);

                                MultiSigSignBTN.Visibility = Visibility.Hidden;
                                MultiSigSignCancelBTN.Visibility = Visibility.Hidden;
                                MSMSendLBL1.Visibility = Visibility.Hidden;
                                MultiSigImportFileBTN.Visibility = Visibility.Visible;
                                MSMSummaryTB.Text = "";



                            }
                        else
                        {
                            ErrorMsg("Transaction FAILED - MultiSign Sign & Send : " + RpcResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMsg("Transaction FAILED - Exception during Multisig Sign & Send : " + ex.Message);

                        //check if its an MSM transaction that didnt reach quorem threshold (allow to exoprt)
                        MSMTransactionCheckAndExport(ex);

                    }



                }
                else
                {

                }                






            }

            }
            catch (Exception ex)
            {
                ErrorMsg("Multisig-Sign&Send error : " + ex.Message);
            }
            


        }

        private void QuorumGetBTN_Click(object sender, RoutedEventArgs e)
        {
            //display quorum
            Quorum qr = eonClient.WalletManager.WalletCollection[AccountListView.SelectedIndex].Information.Quorum;
            if (qr != null)
            {
                int quorumAll = qr.quorum;
                CheckBox_QuorumAll.IsChecked = true;
                QuorumAll_Slider.IsEnabled = true;
                QuorumAll_Slider.Value = qr.quorum;


                QuorumAccountReg_Slider.Value = qr.quorum;
                QuorumAccountReg_Slider.IsEnabled = false;
                CheckBox_QuorumAccountReg.IsChecked = false;
                
                QuorumPayment_Slider.Value = qr.quorum;
                QuorumPayment_Slider.IsEnabled = false;
                CheckBox_QuorumPayment.IsChecked = false;


                QuorumDepositChange_Slider.Value = qr.quorum;
                QuorumDepositChange_Slider.IsEnabled = false;
                CheckBox_QuorumDeposit.IsChecked = false;

                QuorumDelegate_Slider.Value = qr.quorum;
                QuorumDelegate_Slider.IsEnabled = false;
                CheckBox_QuorumDelegate.IsChecked = false;

                QuorumQuorum_Slider.Value = qr.quorum;
                QuorumQuorum_Slider.IsEnabled = false;
                CheckBox_QuorumQuorum.IsChecked = false;


                QuorumRejection_Slider.Value = qr.quorum;
                QuorumRejection_Slider.IsEnabled = false;
                CheckBox_QuorumRejection.IsChecked = false;


                QuorumAccountPublication_Slider.Value = qr.quorum;
                QuorumAccountPublication_Slider.IsEnabled = false;
                CheckBox_QuorumAccountPublication.IsChecked = false;

                //set individual quorums
                if (qr.QuorumByTypes != null)
                {
                    foreach (var qt in qr.QuorumByTypes)
                    {
                        switch (qt.Key)
                        {
                            case (100)://account registration
                                QuorumAccountReg_Slider.Value = qt.Value;
                                QuorumAccountReg_Slider.IsEnabled = true;
                                CheckBox_QuorumAccountReg.IsChecked = true;

                                break;
                            case (200)://ordinary payment
                                QuorumPayment_Slider.Value = qt.Value;
                                QuorumPayment_Slider.IsEnabled = true;
                                CheckBox_QuorumPayment.IsChecked = true;
                                break;
                            case (300)://Deposit change
                                QuorumDepositChange_Slider.Value = qt.Value;
                                QuorumDepositChange_Slider.IsEnabled = true;
                                CheckBox_QuorumDeposit.IsChecked = true;
                                break;
                            case (400)://Quorum
                                QuorumQuorum_Slider.Value = qt.Value;
                                QuorumQuorum_Slider.IsEnabled = true;
                                CheckBox_QuorumQuorum.IsChecked = true;
                                break;
                            case (425)://Delegate
                                QuorumDelegate_Slider.Value = qt.Value;
                                QuorumDelegate_Slider.IsEnabled = true;
                                CheckBox_QuorumDelegate.IsChecked = true;
                                break;
                            case (450)://Rejection
                                QuorumRejection_Slider.Value = qt.Value;
                                QuorumRejection_Slider.IsEnabled = true;
                                CheckBox_QuorumRejection.IsChecked = true;
                                break;
                            case (475)://Account Publication
                                QuorumAccountPublication_Slider.Value = qt.Value;
                                QuorumAccountPublication_Slider.IsEnabled = true;
                                CheckBox_QuorumAccountPublication.IsChecked = true;
                                break;
                        }
                    }
                }



            }
            else  //if quorum is null
            {
                //set all quorum types to ALL value
                CheckBox_QuorumAll.IsChecked = false;
                QuorumAll_Slider.IsEnabled = false;
                QuorumAll_Slider.Value = 0;


                QuorumAccountReg_Slider.Value = 0;
                QuorumAccountReg_Slider.IsEnabled = false;
                CheckBox_QuorumAccountReg.IsChecked = false;

                QuorumPayment_Slider.Value = 0;
                QuorumPayment_Slider.IsEnabled = false;
                CheckBox_QuorumPayment.IsChecked = false;


                QuorumDepositChange_Slider.Value = 0;
                QuorumDepositChange_Slider.IsEnabled = false;
                CheckBox_QuorumDeposit.IsChecked = false;

                QuorumDelegate_Slider.Value = 0;
                QuorumDelegate_Slider.IsEnabled = false;
                CheckBox_QuorumDelegate.IsChecked = false;

                QuorumQuorum_Slider.Value = 0;
                QuorumQuorum_Slider.IsEnabled = false;
                CheckBox_QuorumQuorum.IsChecked = false;


                QuorumRejection_Slider.Value = 0;
                QuorumRejection_Slider.IsEnabled = false;
                CheckBox_QuorumRejection.IsChecked = false;


                QuorumAccountPublication_Slider.Value = 0;
                QuorumAccountPublication_Slider.IsEnabled = false;
                CheckBox_QuorumAccountPublication.IsChecked = false;
            }
        }

        private void CheckBox_QuorumAccountReg_Click(object sender, RoutedEventArgs e)
        {
            QuorumAccountReg_Slider.IsEnabled = (bool)CheckBox_QuorumAccountReg.IsChecked;
        }

        private void CheckBox_QuorumPayment_Click(object sender, RoutedEventArgs e)
        {
            QuorumPayment_Slider.IsEnabled = (bool)CheckBox_QuorumPayment.IsChecked;
        }

        private void CheckBox_QuorumDeposit_Click(object sender, RoutedEventArgs e)
        {
            QuorumDepositChange_Slider.IsEnabled = (bool)CheckBox_QuorumDeposit.IsChecked;
        }
        
        private void CheckBox_QuorumQuorum_Click(object sender, RoutedEventArgs e)
        {
            QuorumQuorum_Slider.IsEnabled = (bool)CheckBox_QuorumQuorum.IsChecked;
        }

        private void CheckBox_QuorumDelegate_Click(object sender, RoutedEventArgs e)
        {
            QuorumDelegate_Slider.IsEnabled = (bool)CheckBox_QuorumDelegate.IsChecked;
        }

        private void CheckBox_QuorumRejection_Click(object sender, RoutedEventArgs e)
        {
            QuorumRejection_Slider.IsEnabled = (bool)CheckBox_QuorumRejection.IsChecked;
        }

        private void CheckBox_QuorumAccountPublication_Click(object sender, RoutedEventArgs e)
        {
            QuorumAccountPublication_Slider.IsEnabled = (bool)CheckBox_QuorumAccountPublication.IsChecked;
        }

        private void CheckBox_QuorumAll_Click(object sender, RoutedEventArgs e)
        {
            QuorumAll_Slider.IsEnabled = (bool)CheckBox_QuorumAll.IsChecked;
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





