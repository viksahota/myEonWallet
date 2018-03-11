using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EonSharp;
using System.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace myEonClient
{
    public class MyEonClient : IMyEonClientInterface , INotifyPropertyChanged
    {
        //defined private object instances
        public EonSharp.EonClient eonSharpClient;

        public MyEonClientCoreConfig coreConfig;

        //public MyEonClientCore eonClientCore;

        //public int RecentTransactions_ConfirmedMax;
        private int recentTransactions_ConfirmedMax;
        public int RecentTransactions_ConfirmedMax
        {
            get
            {
                return this.recentTransactions_ConfirmedMax;
            }
            set
            {
                if (this.recentTransactions_ConfirmedMax != value)
                {
                    this.recentTransactions_ConfirmedMax = value;
                    Properties.Settings.Default.RecentConfirmedMax = value;
                    Properties.Settings.Default.Save();

                    this.NotifyPropertyChanged("RecentTransactions_ConfirmedMax");
                }
            }
        }

        //defined public object instances
        public WalletManagerClass WalletManager;

        //define object which will hold the confirmed/unconfirmed transaction list after sync.
        public TransactionHistoryClass TransactionHistory;

        //store the UI thread context when it calls the constructor, in order to access recent transactions list from eon thread
        SynchronizationContext UIContext;

        //update period for balance checks and BalanceUpdate callbacks
        public int BalanceUpdatePeriod = 10;

        //define a thread which will process away from form thread.
        private Thread _eonThread;
        public bool _eonThreadRun = true;

        public bool ShutDownFlag = false;
        private volatile bool PauseBalanceUpdateFlag = false;

        //wallet app must update the eon client to update the selected account recent transaction list. eon thread will make a periodic update
        public int selectedIndex = 0;


        #region Callback routines
        //declare debug and error events which will provide string output to consumers.  Must be set true after instantiating this class, to enable the debug or error events
        public event EventHandler<string> DebugEvent;
        public event EventHandler<string> ErrorEvent;

        private bool debugEventEnable;
        private bool errorEventEnable;

        public bool DebugEventEnable
        {
            get { return this.debugEventEnable; }
            set
            {
                if (this.debugEventEnable != value)
                {
                    this.debugEventEnable = value;
                    Properties.Settings.Default.EnableDebugLog = value;
                    Properties.Settings.Default.Save();

                    this.NotifyPropertyChanged("DebugEventEnable");

                    
                }
            }
        }
        public bool ErrorEventEnable
        {
            get { return this.errorEventEnable; }
            set
        {
                if (this.errorEventEnable != value)
                {
                    this.errorEventEnable = value;
                    Properties.Settings.Default.EnableErrorLog = value;
                    Properties.Settings.Default.Save();
                    this.NotifyPropertyChanged("ErrorEventEnable");
                }
            }
        }
        

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public event EventHandler<string> BalanceUpdateEvent;
        
        // Invoke the debug callback to provide feedback to consumer
        private void DebugMsg(string msg)
        {
            if (DebugEventEnable)
            {
                //add a custom tag to the message and despatch it
                msg = "MyEONClient." + msg;
                DebugEvent?.Invoke(this, msg);

                //DebugInfo.DebugString += msg;                

            }
        }

        // Invoke the error callback to provide feedback to consumer
        private void ErrorMsg(string msg)
        {
            if (ErrorEventEnable)
            {
                //add a custom tag to the message and despatch it
                msg = "MyEonClient." + msg;
                ErrorEvent?.Invoke(this, msg);
            }
        }

        // Invoke the balance update callback to provide feedback to consumer when the balance is changed
        private void BalanceUpdateMsg(string msg)
        {
            BalanceUpdateEvent?.Invoke(this, msg);
        }

        #endregion

        //class constructor
        public MyEonClient()
        {
            coreConfig = new MyEonClientCoreConfig();
            eonSharpClient = new EonClient(coreConfig.Peer);
            InitEonSharp();

            //initialise objects
            WalletManager = new WalletManagerClass();
            WalletManager.DebugEvent += (sender, msg) => { DebugMsg(msg); };
            WalletManager.ErrorEvent += (sender, msg) => { ErrorMsg(msg); };

            TransactionHistory = new TransactionHistoryClass();
            
            //load the debug switches from settings
            DebugEventEnable = Properties.Settings.Default.EnableDebugLog;
            ErrorEventEnable = Properties.Settings.Default.EnableErrorLog;
            RecentTransactions_ConfirmedMax = Properties.Settings.Default.RecentConfirmedMax;

            //store the UI syncronisation context
            UIContext = SynchronizationContext.Current;
        }

      
        public void Start()
        {            
            //pass down the debug settings to walletlist, the load the wallet data from user settings
            WalletManager.DebugEventEnable = DebugEventEnable;
            WalletManager.ErrorEventEnable = ErrorEventEnable;

            //start the eon thread
            _eonThread = new Thread(EonThreadStart);
            _eonThread.SetApartmentState(ApartmentState.STA);
            _eonThread.Start();
        }

        public async void InitEonSharp()
        {
            try
            {
                //Usefull only during beta. Default is false. Afects all transport contexts for now.
                EonSharp.Configuration.IgnoreSslErrors = true;

                EonClient.ClassMapper[typeof(EonSharp.Network.ITransportContext)] = new ActivatorDescriptor[]
                    {
                        new ActivatorDescriptor(typeof(EonSharp.Network.Transports.HttpTransportClient)),
                        new ActivatorDescriptor(typeof(EonSharp.Logging.HttpTransportLogger), new object[]{ "[HTTP TRANSPORT] ", new string[]{ "getinformation" , "metadata.getAttributes", "history.getCommittedPage", "history.getUncommitted", "accounts.getBalance", "coloredCoin.getInfo" } })
                    };

                eonSharpClient = new EonClient(coreConfig.Peer);
                var logger = eonSharpClient.TransportContext as EonSharp.Logging.ILog;
                logger.LogChanged += (s, e) => DebugMsg(e.ToString());
                await eonSharpClient.UpdateBlockchainDetails();

            }
            catch(Exception ex)
            {
                ErrorMsg("InitEonSharp() Exception : " + ex.Message);
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }


        private void EonThreadStart()
        {
            //allow the form to open
            Thread.Sleep(50);
 
            // force update the account list and balances
            UpdateBalances(true);
            
            int counter = 0;
            DateTime lastBalancePollTime = DateTime.Now;
            
            while (_eonThreadRun)
            {
                Thread.Sleep(50);
                //every 5 seconds update the main account balance/deposit (if there are wallets present)
                TimeSpan elapsed = DateTime.Now - lastBalancePollTime;
                if ((elapsed.Seconds >= BalanceUpdatePeriod) && (WalletManager.WalletCollection.Count > 0))
                {
                    try
                    {
                        //run the BalanceUpdate process
                        if (_eonThreadRun && !PauseBalanceUpdateFlag) UpdateBalances(false);
                        lastBalancePollTime = DateTime.Now;

                        if (selectedIndex!=-1) UpdateTransactionSummary(selectedIndex);

                        //every 10th iteration garbage collect
                        counter++;
                        if (counter >= 10)
                        {
                            GC.Collect();
                            counter = 0;
                        }
                    }
                    catch(Exception ex)
                    {
                        ErrorMsg("EonThreadStart() - Exception : " + ex.Message);
                    }
                }

            }
            ErrorMsg("<eonThread ended>");
        }

        //update the balances of all accounts and despatch update to consumer
        public async void UpdateBalances(bool UpdateNow)
        {
            bool change = false;

            //get a balance update for each wallet
            foreach (Wallet wal in WalletManager.WalletCollection)
            {
                try
                {
                    long oldAmount = 0;
                    long oldDeposit = 0;

                    //update the balance & deposit values
                    if (wal.Information != null)
                    {
                        oldAmount = wal.Information.Amount;
                        oldDeposit = wal.Information.Deposit;
                    }

                    await wal.RefreshAsync(eonSharpClient);
                    


                    //check for a change in amount or deposit, inform the wallet to update the display if necessary.
                    if (oldAmount != wal.Information.Amount)
                    {
                        change = true;
                    }
                    if (oldDeposit != wal.Information.Deposit)
                    {
                        change = true;
                    }
                }
                catch (Exception ex)
                {
                    ErrorMsg("UpdateBalances() - Exception getting balance update for " + wal.AccountDetails.AccountId + " : " + ex.Message);
                }
            }

            //callback to the consumer to update the main balance/deposit display since an update occured
            if ((UpdateNow | change)&&(WalletManager.WalletCollection.Count>0)) BalanceUpdateMsg("");

        }

        public bool Wallets_Add(Wallet wal)
        {
            bool res = false;

            //stop the balance being updated during this
            PauseBalanceUpdateFlag = true;
            System.Threading.Thread.Sleep(250);

            WalletManager.AddWallet(wal);

            //force an account/balance update
            UpdateBalances(true);
            System.Threading.Thread.Sleep(250);

            //resume periodic balance updates
            PauseBalanceUpdateFlag = false;

            DebugMsg("Wallets_Add() - New Account " + wal.AccountDetails.AccountId + " stored to walletlist");
            return res;
        }


        //restore the wallet list from a file
        public bool Wallets_Restore(string filePath)
        {
            bool res = false;

            //stop the balance being updated during this
            PauseBalanceUpdateFlag = true;
            System.Threading.Thread.Sleep(250);

            res = WalletManager.RestoreWalletList(filePath);

            //force an account/balance update
            UpdateBalances(true);
            System.Threading.Thread.Sleep(250);

            //resume periodic balance updates
            PauseBalanceUpdateFlag = false;

            return res;
        }

        //reset the wallet list
        public bool Wallets_Reset()
        {
            bool res = false;

            //stop the balance being updated during this
            PauseBalanceUpdateFlag = true;
            System.Threading.Thread.Sleep(250);

            res = WalletManager.ResetWalletList();
            DebugMsg("Wallets_Reset() - Wallet configuration reset");

            //force an account/balance update
            UpdateBalances(true);
            System.Threading.Thread.Sleep(250);

            //resume balance updates
            PauseBalanceUpdateFlag = false;

            return res;
        }

        //backup the wallet list
        public bool Wallets_Backup(string filePath)
        {
            bool res = false;

            //stop the balance being updated during this
            PauseBalanceUpdateFlag = true;
            System.Threading.Thread.Sleep(250);

            res = WalletManager.BackupWalletList(filePath);
            System.Threading.Thread.Sleep(250);

            //resume balance updates
            PauseBalanceUpdateFlag = false;

            return res;
        }

        //return the number of wallets
        public int Wallets_GetCount()
        {
            int count = WalletManager.WalletCollection.Count;
            return count;
        }

        //return a particular wallet
        public Wallet Wallet_Get(int index)
        {
            return WalletManager.WalletCollection[index];
        }

        //register a new account
        public async Task<RpcResponseClass> Transaction_Register(Wallet newWallet, string senderPassword)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                    Wallet primaryWallet = WalletManager.WalletCollection[0];

                //EonSharp.Api.Transactions.Deposit depTX = new EonSharp.Api.Transactions.Deposit(primaryWallet.AccountDetails.AccountId, 5, 3600, 10, 2);
                //depTX.SignTransaction(primaryWallet.GetExpandedKey(senderPassword));
                //await eonSharpClient.Bot.Transactions.PutTransactionAsync(depTX);
                EonSharp.Api.Transactions.Registration accountRegistration = new EonSharp.Api.Transactions.Registration(primaryWallet.AccountDetails.AccountId, newWallet.AccountDetails.AccountId, newWallet.AccountDetails.PublicKey,3600,10,1);
                accountRegistration.SignTransaction(primaryWallet.GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(accountRegistration);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transaction_Register() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }            
            return RpcResult;
        }

        public async Task<RpcResponseClass> Transaction_SetDeposit(int index, decimal amountEON, string senderPassword)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();
            long amount = (long)(amountEON * 1000000);

            try
            {
                Wallet senderWallet = WalletManager.WalletCollection[index];
                EonSharp.Api.Transactions.Deposit dTX = new EonSharp.Api.Transactions.Deposit(senderWallet.AccountDetails.AccountId, amount, 3600, 10, 1);
                dTX.SignTransaction(WalletManager.WalletCollection[index].GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(dTX);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transactions_SetDeposit() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }

            return (RpcResult);
        }
        
        //Process an ordinary payment transaction
        public async Task<RpcResponseClass> Transaction_SendPayment(int index, string recipient, decimal amount, string senderPassword)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                Wallet senderWallet = WalletManager.WalletCollection[index];
                EonSharp.Api.Transactions.Payment payment = new EonSharp.Api.Transactions.Payment(senderWallet.AccountDetails.AccountId, (long)amount, recipient, 3600, 10, 1);
                payment.SignTransaction(senderWallet.GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(payment);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transaction_SendPayment() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }


            return RpcResult;
        }

        public async Task<RpcResponseClass> Transaction_ColorCoinRegistration(int senderAccountIndex, string senderPassword, long EmissionAmount, int DecimalPoints)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                Wallet senderWallet = WalletManager.WalletCollection[senderAccountIndex];
                EonSharp.Api.Transactions.ColoredCoinRegistration ccReg = new EonSharp.Api.Transactions.ColoredCoinRegistration(senderWallet.AccountDetails.AccountId, EmissionAmount, DecimalPoints);
                ccReg.SignTransaction(senderWallet.GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(ccReg);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transaction_ColorCoinRegistration() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }
            return RpcResult;
        }

        public async Task<RpcResponseClass> Transaction_ColorCoinPayment(int senderAccountIndex, string senderPassword, long Amount, string Recipient, string ColorCoinTypeID)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                Wallet senderWallet = WalletManager.WalletCollection[senderAccountIndex];
                EonSharp.Api.Transactions.ColoredCoinPayment ccPay = new EonSharp.Api.Transactions.ColoredCoinPayment(senderWallet.AccountDetails.AccountId, Amount, Recipient, ColorCoinTypeID);
                ccPay.SignTransaction(senderWallet.GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(ccPay);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transaction_ColorCoinPayment() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }
            return RpcResult;
        }

        public async Task<RpcResponseClass> Transaction_ColorCoinDestroy(int senderAccountIndex, string senderPassword)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            RpcResult = await Transaction_ColorCoinSupply(senderAccountIndex, senderPassword,(long)0);

            return RpcResult;
        }

            public async Task<RpcResponseClass> Transaction_ColorCoinSupply(int senderAccountIndex, string senderPassword, long MoneySupply)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                Wallet senderWallet = WalletManager.WalletCollection[senderAccountIndex];
                EonSharp.Api.Transactions.ColoredCoinSupply ccSupply = new EonSharp.Api.Transactions.ColoredCoinSupply(senderWallet.AccountDetails.AccountId, MoneySupply);
                ccSupply.SignTransaction(senderWallet.GetExpandedKey(senderPassword));
                await eonSharpClient.Bot.Transactions.PutTransactionAsync(ccSupply);
                RpcResult.Result = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("Transaction_ColorCoinSupply() - Exception : " + ex.Message);
                RpcResult.Result = false;
                throw ex;
            }
            return RpcResult;
        }

        public async void GetTransactions(int index, int maxPages)
        {
            if (index >= 0)
            {
                try
                {
                    TransactionHistory.ConfirmedTransactionCollection.Clear();
                    int pageNumber = 0;

                    //keeps track how many TX there are. When this number no longer increases, we have read all pages
                    int txCounter = 0;
                    int lastCount = -1;

                    while ((txCounter != lastCount) && (pageNumber < maxPages))
                    {
                        try
                        {
                            lastCount = txCounter;

                            await GetPage(index, pageNumber);

                            txCounter = TransactionHistory.ConfirmedTransactionCollection.Count;
                            pageNumber++;

                        }
                        catch (Exception ex)
                        {
                            ErrorMsg("GetTransactions() - Error getting transactions page: " + ex.Message);
                        }
                    }

                    DebugMsg("GetTransactions() - Retreived transactions OK");
                }
                catch (Exception ex)
                {
                    ErrorMsg("GetTransactions() - Error retrieving transactions: " + ex.Message);
                }
            }

        }
        
        public async Task GetPage(int accountIndex, int pageNumber)
        {
            try
            {
                Wallet currentWallet = WalletManager.WalletCollection[accountIndex];

                IEnumerable<EonSharp.Api.Transaction> res = await eonSharpClient.Bot.History.GetCommittedPageAsync(currentWallet.AccountDetails.AccountId, pageNumber);
                Array txArray = res.ToArray<EonSharp.Api.Transaction>();

                foreach (EonSharp.Api.Transaction tx in txArray)
                {
                    TransactionItemClass ntx = new TransactionItemClass();
                    ntx.Id = tx.Id;
                    ntx.Version = tx.Version;
                    ntx.Type = tx.Type.ToString();
                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp);
                    DateTime dateTime = dateTimeOffset.DateTime;
                    ntx.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();
                    //ntx.Timestamp = tx.Timestamp;
                    ntx.Signature = tx.Signature;
                    ntx.Sender = tx.Sender;
                    ntx.Fee = tx.Fee;
                    ntx.Deadline = tx.Deadline;

                    if (tx.Attachment.GetType().Name == "PaymentAttachment")
                    {
                        ntx.Type = "Send Payment";
                        ntx.AttachedRecipient = ((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Recipient;
                        ntx.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Amount / 1000000;
                    }
                    else if (tx.Attachment.GetType().Name == "DepositAttachment")
                    {
                        ntx.Type = "Deposit Change";
                        ntx.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.DepositAttachment)tx.Attachment).Amount / 1000000;
                    }
                    else if (tx.Attachment.GetType().Name == "RegistrationAttachment")
                    {
                        ntx.Type = "New Account Registration";
                        ntx.AttachedNewUserID = ((EonSharp.Api.Transactions.Attachments.RegistrationAttachment)tx.Attachment).AccountId;
                        ntx.AttachedNewUserPubKey = ((EonSharp.Api.Transactions.Attachments.RegistrationAttachment)tx.Attachment).PublicKey;
                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinRegistrationAttachment")
                    {
                        ntx.Type = "Color Coin Registration";
                        ntx.AttachedColorCoinEmission = ((EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment)tx.Attachment).Emission;
                        ntx.AttachedAmount = ntx.AttachedColorCoinEmission;
                        ntx.AttachedColorCoinDecimals = ((EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment)tx.Attachment).DecimalPoint;
                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinPaymentAttachment")
                    {
                        ntx.Type = "Color Coin Payment";
                        ntx.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.ColoredCoinPaymentAttachment)tx.Attachment).Amount;
                       
                    }

                    TransactionHistory.ConfirmedTransactionCollection.Add(ntx);
                }
            }
            catch(Exception ex)
            {
                ErrorMsg("GetPage() - Exception : " + ex.Message);
            }
        }


        public async void UpdateTransactionSummary(int index)
        {
            //get the uncommited transactions and the first page of commited transactions
            try
            {
                IEnumerable<EonSharp.Api.Transaction> uList = await eonSharpClient.Bot.History.GetUncommittedAsync(WalletManager.WalletCollection[index].AccountDetails.AccountId);
                IEnumerable<EonSharp.Api.Transaction> cList = await eonSharpClient.Bot.History.GetCommittedPageAsync(WalletManager.WalletCollection[index].AccountDetails.AccountId, 0);
                uList = uList.Reverse();

                int targetIndex = 0;

                foreach (EonSharp.Api.Transaction tx in uList)
                {
                    TransactionSummaryItemClass nT = new TransactionSummaryItemClass();
                    nT.Id = tx.Id;
                    nT.Sender = tx.Sender;
                    nT.Status = "Pending";

                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp);
                    DateTime dateTime = dateTimeOffset.DateTime;
                    nT.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();

                    if (tx.Attachment.GetType().Name == "PaymentAttachment")
                    {
                        nT.Type = "Payment";
                        nT.AttachedRecipient = ((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Recipient;
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Amount / 1000000;
                    }
                    else if (tx.Attachment.GetType().Name == "DepositAttachment")
                    {
                        nT.Type = "Deposit Change";
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.DepositAttachment)tx.Attachment).Amount / 1000000;
                    }

                    else if (tx.Attachment.GetType().Name == "RegistrationAttachment")
                    {
                        nT.Type = "Account Registration";
                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinRegistrationAttachment")
                    {
                        nT.Type = "Color Coin registration";
                        nT.AttachedAmount = ((EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment)tx.Attachment).Emission;

                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinPaymentAttachment")
                    {
                        nT.Type = "Color Coin Payment";
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.ColoredCoinPaymentAttachment)tx.Attachment).Amount;

                    }


                    //update if entry is different
                    if (TransactionHistory.SummaryTransactionCollection.Count < (targetIndex + 1)) UIContext.Send(x => TransactionHistory.SummaryTransactionCollection.Add(nT), null);
                    else if (nT != TransactionHistory.SummaryTransactionCollection[targetIndex]) UIContext.Send(x => TransactionHistory.SummaryTransactionCollection[targetIndex] = nT, null);
                    targetIndex++;
                }

                //limits the number of confirmed transactions shown
                int limitCounter = 0;

                foreach (EonSharp.Api.Transaction tx in cList)
                {
                    if (limitCounter >= RecentTransactions_ConfirmedMax) break;

                    TransactionSummaryItemClass nT = new TransactionSummaryItemClass();
                    nT.Id = tx.Id;
                    nT.Sender = tx.Sender;
                    nT.Status = "Confirmed";

                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp);
                    DateTime dateTime = dateTimeOffset.DateTime;
                    nT.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();

                    if (tx.Attachment.GetType().Name == "PaymentAttachment")
                    {
                        nT.Type = "Payment";
                        nT.AttachedRecipient = ((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Recipient;
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.PaymentAttachment)tx.Attachment).Amount / 1000000;
                    }
                    else if (tx.Attachment.GetType().Name == "DepositAttachment")
                    {
                        nT.Type = "Deposit Change";
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.DepositAttachment)tx.Attachment).Amount / 1000000;
                    }

                    else if (tx.Attachment.GetType().Name == "RegistrationAttachment")
                    {
                        nT.Type = "Account Registration";
                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinRegistrationAttachment")
                    {
                        nT.Type = "Color Coin registration";
                        nT.AttachedAmount = ((EonSharp.Api.Transactions.Attachments.ColoredCoinRegistrationAttachment)tx.Attachment).Emission;

                    }
                    else if (tx.Attachment.GetType().Name == "ColoredCoinPaymentAttachment")
                    {
                        nT.Type = "Color Coin Payment";
                        nT.AttachedAmount = (decimal)((EonSharp.Api.Transactions.Attachments.ColoredCoinPaymentAttachment)tx.Attachment).Amount;

                    }

                    //update if entry is different
                    if (TransactionHistory.SummaryTransactionCollection.Count < (targetIndex + 1)) UIContext.Send(x => TransactionHistory.SummaryTransactionCollection.Add(nT), null);
                    else if (nT != TransactionHistory.SummaryTransactionCollection[targetIndex]) UIContext.Send(x => TransactionHistory.SummaryTransactionCollection[targetIndex] = nT, null);
                    targetIndex++;

                    limitCounter++;
                }
                
                //remove any entries beyond this index in the SummaryTransactionCollection , stale data
                while (TransactionHistory.SummaryTransactionCollection.Count > targetIndex)
                {
                    //remove the last entry
                    UIContext.Send(x => TransactionHistory.SummaryTransactionCollection.RemoveAt(TransactionHistory.SummaryTransactionCollection.Count - 1), null);
                }
            }
            catch(Exception ex)
            {
                DebugMsg("UpdateTransactionSummary() raised exception : " + ex.Message);
            }
        }


        public Wallet CreateAccount(string name, string password)
        {
            Wallet newWal = new Wallet(name, password);
            string seed = EonSharp.Helpers.HexHelper.ArrayToHexString(newWal.GetPrivateKey(password));

            try
            {                
                newWal.UnlockAccountDetails(password);
                newWal.RefreshAsync(eonSharpClient);
                DebugMsg("CreateAccount() - New AccountID : " + newWal.AccountDetails.AccountId);
                DebugMsg("CreateAccount() -     Seed : " + seed);
                DebugMsg("CreateAccount() -     Public Key : " + newWal.AccountDetails.PublicKey);
            }
            catch(Exception ex)
            {
                ErrorMsg("CreateAccount() - Exception : " + ex.Message);
            }
            return newWal;
        }

      

    }
}
