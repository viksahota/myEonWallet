using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EonSharp;


namespace myEonClient
{
    public class WalletManagerClass
    {
        //collection of wallet objects which will be maintained and used
        //public ObservableCollection<WalletClass> WalletCollection;
        public ObservableCollection<EonSharp.Wallet> WalletCollection;

        //constructor
        public WalletManagerClass()
        {
            //default to debug & errors OFF
            DebugEventEnable = false;
            ErrorEventEnable = false;

            //init the Wallet Collection list
            WalletCollection = new ObservableCollection<EonSharp.Wallet>();
            
        }

        #region Callback routines
        //declare debug and error events which will provide string output to consumers.  Must be set true after instantiating this class, to enable the debug or error events
        public event EventHandler<string> DebugEvent;
        public event EventHandler<string> ErrorEvent;
        public bool DebugEventEnable;
        public bool ErrorEventEnable;

        // Invoke the debug callback to provide feedback to consumer
        private void DebugMsg(string msg)
        {
            if (DebugEventEnable)
            {
                //add a custom tag to the message and despatch it
                msg = "[WalletManagerClass]." + msg;
                DebugEvent?.Invoke(this, msg);
            }
        }

        // Invoke the error callback to provide feedback to consumer
        private void ErrorMsg(string msg)
        {
            if (ErrorEventEnable)
            {
                //add a custom tag to the message and despatch it
                msg = "[WalletManagerClass]." + msg;
                ErrorEvent?.Invoke(this, msg);
            }
        }
        #endregion

        //returns the number of wallets in WalletCollection
        public int GetWalletsCount()
        {
            return (WalletCollection.Count);
        }


        #region ADD/REMOVE Wallet entries

        //add a wallet to the collection
        public bool AddWallet(EonSharp.Wallet wal)
        {
            bool res = false;
            try
            {
                WalletCollection.Add(wal);
                DebugMsg("AddWallet() Added account " + wal.Id + " @ index " + WalletCollection.Count);
                SaveWallets();
                res = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("AddWallet() - Exception : " + ex);
                res = false;
            }
            return res;
        }

        //remove a wallet from the collection (by index)
        public bool RemoveWallet(int WalletIndex)
        {
            bool res = false;
            try
            {
                WalletCollection.Remove(WalletCollection[WalletIndex]);
                DebugMsg("RemoveWallet() - Index " + WalletIndex + " removed");
                SaveWallets();
                res = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("RemoveWallet() - Exception : " + ex);
                res = false;
            }
            return res;
        }

        #endregion

        #region LOAD/SAVE/BACKUP/RESTORE/SERIALISE/DESERIALISE the WalletCollection


        //load the wallets from ApplicationSettingsBase , gives exception on failure
        public async void LoadWallets(myEonClient.MyEonClient eonClient)
        {
                var deswallets = Properties.Settings.Default.WalletsJson.FromJsonToWallets();

                WalletCollection.Clear();
                foreach (Wallet wal in deswallets)
                {
                try
                    {
                        await wal.RefreshAsync(eonClient.eonSharpClient);
                    }
                catch (Exception ex)
                    {
                    }
                WalletCollection.Add(wal);
                }

        }

        //store the wallets to ApplicationSettingsBase
        private bool SaveWallets()
        {
            Properties.Settings.Default.WalletsJson = WalletCollection.ToJson();
            Properties.Settings.Default.Save();
            return true;
        }

        //backup the wallets to a file
        public bool BackupWalletList(string filePath)
        {
            bool res = false;
            
            try
            {
                using (var file = System.IO.File.OpenWrite(filePath))
                {
                    WalletCollection.ToJson(file);
                }
                
                res = true;
                DebugMsg("BackupWalletList() - Backed up to " + filePath);
            }
            catch (Exception ex)
            {
                ErrorMsg("BackupWalletList() - File write failed with exception : " + ex.Message);
            }
            return res;
        }

        //restore the wallets from a file
        public bool RestoreWalletList(string filePath)
        {
            bool res = false;

            try
            {
                using (var file = System.IO.File.OpenRead(filePath))
                {
                    var deswallets = file.FromJsonToWallets();

                    WalletCollection.Clear();
                    foreach (Wallet wal in deswallets)
                    {
                        WalletCollection.Add(wal);
                    }
                }
                
                SaveWallets();
                DebugMsg("RestoreWalletList() - Restored from " + filePath);
                res = true;
            }
            catch (Exception ex)
            {
                ErrorMsg("RestoreWalletList() - Raised Exception :" + ex.Message);
            }



            return res;
        }

        //clears down the walletlist 
        public bool ResetWalletList()
        {
            bool res = false;
            WalletCollection.Clear();
            SaveWallets();
            res = true;

            return res;
        }

        #endregion



        // ---------------------------------------


    }
}
