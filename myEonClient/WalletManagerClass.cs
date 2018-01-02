using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
                    WalletClass censoredWallet = new WalletClass
                    {
                        AccountID = wal.AccountID,
                        AccountNumber = wal.AccountNumber,
                        Balance = wal.Balance,
                        Deposit = wal.Deposit,
                        NickName = wal.NickName,
                        PublicKey = wal.PublicKey,
                        Seed = ""
                    };
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

        #region LOAD/SAVE/BACKUP/RESTORE/SERIALISE/DESERIALISE the WalletCollection

        //load the wallets from ApplicationSettingsBase
        public bool LoadWallets()
        {
            DeserialiseWalletCollection(Properties.Settings.Default.WalletsJson);
            return true;
        }

        //store the wallets to ApplicationSettingsBase
        public bool SaveWallets()
        {
            Properties.Settings.Default.WalletsJson = SerialiseWalletCollection(); ;
            Properties.Settings.Default.Save();
            return true;
        }

        //backup the wallets to a file
        public bool BackupWalletList(string filePath)
        {
            bool res = false;
            String js = SerialiseWalletCollection();

            TextWriter writer = null;
            try
            {
                writer = new StreamWriter(filePath, false);
                writer.Write(js);
                res = true;
                DebugMsg("BackupWalletList() - Backed up to " + filePath);
            }
            catch(Exception ex)
            {
                ErrorMsg("BackupWalletList() - File write failed with exception : " + ex.Message);
            }
            finally
            {
                writer?.Close();
            }

            return res;
        }

        //restore the wallets from a file
        public bool RestoreWalletList(string filePath)
        {
            bool res = false;
            string fileContents = "";
            TextReader reader = null;

            try
            {
                reader = new StreamReader(filePath);
                fileContents = reader.ReadToEnd();

                DeserialiseWalletCollection(fileContents);

                DebugMsg("RestoreWalletList() - Restored from " + filePath);
                res = true;
            }
            catch(Exception ex)
            {
                ErrorMsg("RestoreWalletList() - Raised Exception :" + ex.Message);
            }
            finally
            {
                reader?.Close();
            }
                      

            return res;
        }

        //serialise the WalletCollection
        private string SerialiseWalletCollection()
        {
            String js = "[";

            foreach (WalletClass wal in WalletCollection)
            {
                js += "{\"NickName\":\"" + wal.NickName + "\",\"Seed\":\"" + wal.Seed + "\",\"AccountNumber\":\"" + wal.AccountNumber + "\",\"AccountID\":\"" + wal.AccountID + "\",\"PublicKey\":\"" + wal.PublicKey + "\"},";
            }
            js.Remove(js.Length - 1, 1);
            js += "]";

            return (js);
        }

        //deserialise string and write to the WalletCollection
        private void DeserialiseWalletCollection(string jsonString)
        {
            try
            {
                //regex to split into invididual json strings
                Match walletMatches = Regex.Match(jsonString, @"{[^}]*}");

                //parse each wallet into the WalletCollection
                WalletCollection.Clear();
                foreach (string walletJson in walletMatches.Groups)
                {
                    Match walletData = Regex.Match(walletJson, @"{""NickName"":""([^""]*)"",""Seed"":""([^""]*)"",""AccountNumber"":""([^""]*)"",""AccountID"":""([^""]*)"",""PublicKey"":""([^""]*)""}");
                    WalletClass wal = new WalletClass
                    {
                        NickName = walletData.Groups[1].ToString(),
                        Seed = walletData.Groups[2].ToString(),
                        AccountNumber = walletData.Groups[3].ToString(),
                        AccountID = walletData.Groups[4].ToString(),
                        PublicKey = walletData.Groups[5].ToString()
                    };
                    WalletCollection.Add(wal);
                }
            }
            catch (Exception ex)
            {
                ErrorMsg("DeserialiseWalletCollection() - Raised Exception :" + ex.Message);
            }
        }

        #endregion



        // ---------------------------------------


    }
}
