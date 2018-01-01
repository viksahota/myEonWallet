using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myEonClient
{
    public class WalletManagerClass
    {
        //collection of wallet objects which will be maintained and used
        private List<WalletClass> WalletCollection;

        //constructor
        public WalletManagerClass()
        {
            //default to debug & errors OFF
            DebugEventEnable = false;
            ErrorEventEnable = false;

            //init the Wallet Collection list
            WalletCollection = new List<WalletClass>();
            

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
        
        #region GET WalletCollection information
        //returns a censored copy of the WalletCollection (without SEED values)
        public List<WalletClass> GetWalletCollection()
        {
            List<WalletClass> censoredCollection = new List<WalletClass>();

            try
            {
                foreach (WalletClass wal in WalletCollection)
                {
                    WalletClass censoredWallet = new WalletClass();
                    censoredWallet.AccountID = wal.AccountID;
                    censoredWallet.AccountNumber = wal.AccountNumber;
                    censoredWallet.Balance = wal.Balance;
                    censoredWallet.Deposit = wal.Deposit;
                    censoredWallet.NickName = wal.NickName;
                    censoredWallet.PublicKey = wal.PublicKey;
                    censoredWallet.Seed = "";
                    censoredCollection.Add(censoredWallet);                    
                }
                DebugMsg("GetWalletCollection() OK");
            }
            catch(Exception ex)
            {
                ErrorMsg("GetWalletCollection() - Exception : " + ex);
            }
            
            return censoredCollection;
        }

        //returns the number of wallets in WalletCollection
        public int GetWalletsCount()
        {
            return (WalletCollection.Count);
        }
        #endregion

        #region ADD/REMOVE Wallet entries

        //add a wallet to the collection
        public bool AddWallet(WalletClass wal)
        {
            bool res = false;
            try
            {
                WalletCollection.Add(wal);
                DebugMsg("AddWallet() Added account " + wal.AccountID + " @ index " + WalletCollection.Count );
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
        
        #region LOAD/SAVE/BACKUP/RESTORE the WalletCollection
        //load the wallets from resource file object
        public bool LoadWallets()
        {
            return true;
        }

        //store the wallets to resource file object
        public bool SaveWallets()
        {
            return true;
        }

        //backup the wallets to a file
        public bool BackupWalletList()
        {
            return true;
        }

        //restore the wallets from a file
        public bool RestoreWalletList()
        {
            return true;
        }
        #endregion


        // ---------------------------------------


    }
}
