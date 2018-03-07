using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Numerics;
using BencodeNET.Objects;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Sodium;
using System.Globalization;

namespace myEonClient
{
    public class MyEonClientCore
    {
        //we increase this value with each rpc call.
        private int currentTXID;

        public MyEonClientCoreConfig coreConfig;

        //consumers can attach to the debug event.
        public event EventHandler<string> DebugEvent;

        //consumers can attach to the debug event.
        public event EventHandler<string> ErrorEvent;


        public MyEonClientCore()
        {
            currentTXID = 1;

            string path = Environment.GetEnvironmentVariable("PATH");
            string binDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bin");
            Environment.SetEnvironmentVariable("PATH", path + ";" + binDir);

            coreConfig = new MyEonClientCoreConfig();
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

        private void DebugMsg(string msg)
        {
            DebugEvent?.Invoke(this, msg);
        }

        private void ErrorMsg(string msg)
        {
            ErrorEvent?.Invoke(this, msg);
        }

        public WalletClass CreateAccount(string wallet_name)
        {
            string privKeyS = "",
                pubKeyS = "",
                accountString = "";

            WalletClass newWal = new WalletClass();

            try
            {
                //create a random seed value
                byte[] seedBytes = SodiumCore.GetRandomBytes(32);

                //generate a keypair
                KeyPair kp = Sodium.PublicKeyAuth.GenerateKeyPair(seedBytes);
                privKeyS = ByteArrayToString(seedBytes);
                pubKeyS = ByteArrayToString(kp.PublicKey);

                long accountNumber = GetAccountNumber(kp.PublicKey);
                accountString = GetAccountID(accountNumber, "EON");

                newWal.Seed = privKeyS;
                newWal.PublicKey = pubKeyS;
                newWal.AccountID = accountString;
                newWal.NickName = wallet_name;


                DebugMsg("CreateAccount() - " + accountString + " generated OK");

            }
            catch (Exception ex)
            {
                ErrorMsg("CreateAccount() - Exception : " + ex.Message);
            }



            return newWal;
        }

        // encodes a numerical account number to its string representation , eg EON-xxxx-xxxx-xxxx
        private String GetAccountID(long accountID_long, String prefix)
        {
            StringBuilder idStr = new StringBuilder();

            try
            {

                String alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
                BigInteger id = new BigInteger(accountID_long);

                if (accountID_long < 0)
                {
                    BigInteger two64 = BigInteger.Parse("18446744073709551616");
                    id = BigInteger.Add(id, two64);
                }

                BigInteger chs = BigInteger.Zero;
                BigInteger andVal = 0x3FF;
                BigInteger tmp = id;

                while (tmp > BigInteger.Zero)
                {
                    chs = chs ^ (tmp & andVal);
                    tmp = tmp >> 10;
                }

                id = id | (chs << 64);

                //mask to set bit74
                BigInteger b74 = BigInteger.Parse("18889465931478580854784");
                id = id | b74;

                idStr = new StringBuilder(prefix);
                BigInteger andVal2 = 0x1F;

                for (int i = 0; i < 15; i++)
                {

                    if ((i % 5) == 0)
                    {

                        idStr.Append('-');

                    }

                    idStr.Append(alphabet[(int)(id & andVal2)]);

                    id = id >> 5;

                }
            }
            catch (Exception ex)
            {
                ErrorMsg("GetAccountID() - Exception : " + ex.Message);
            }

            return idStr.ToString();
        }

        //get the EON account number (numerical) from a public key
        public long GetAccountNumber(byte[] publicKey)
        {
            BigInteger bigInteger = BigInteger.Zero;

            try
            {
                //create a SHA512 hash using the public key
                byte[] hashBytes = Sodium.CryptoHash.Sha512(publicKey);

                //swap to big endian
                Array.Reverse(hashBytes);

                for (int i = 0; i < hashBytes.Length; i += 8)
                {
                    BigInteger bi = new BigInteger(new byte[] { hashBytes[i + 7], hashBytes[i + 6], hashBytes[i + 5], hashBytes[i + 4], hashBytes[i + 3], hashBytes[i + 2], hashBytes[i + 1], hashBytes[i] });
                    bigInteger = bigInteger ^ bi;
                }


            }
            catch (Exception ex)
            {
                ErrorMsg("GetAccountNumber() - Exception : " + ex.Message);
            }

            return (long)bigInteger;
        }

        //Process a GetInformation RPC call 
        public AccountInfoResponseClass GetInformation(string AccountID)
        {
            AccountInfoResponseClass ar = new AccountInfoResponseClass();

            try
            {
                //getInformation
                string res = RequestRPC("{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"method\": \"accounts.getInformation\" ,\r\n\"params\":[\r\n\"" + AccountID + "\"\r\n],\r\n\"id\": " + currentTXID + "\r\n}", coreConfig.Peer + "/bot");
                string pattern = @"code"":([\d]*),""name"":""([^""]*)""},""public_key"":""([^""]*)"",""amount"":([^,]*),""deposit"":([^,]*),""sign_type"":""([^""]*)";
                Match infoMatch = Regex.Match(res, pattern);

                ar = new AccountInfoResponseClass();
                ar.CodeNumber = infoMatch.Groups[1].ToString();
                ar.CodeName = infoMatch.Groups[2].ToString();
                ar.PublicKey = infoMatch.Groups[3].ToString();
                ar.Amount = infoMatch.Groups[4].ToString();
                ar.Deposit = infoMatch.Groups[5].ToString();
                ar.SignType = infoMatch.Groups[6].ToString();
            }
            catch (Exception ex)
            {
                ErrorMsg("GetInformation() - Exception : " + ex.Message);
            }

            return (ar);
        }

        //send a new user registration transaction.  Returns the response object or throws exception
        public RpcResponseClass Transaction_RegisterNewUser(WalletClass PrimaryWallet, string NewAccountID, string NewAccountPublicKey )
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                TransactionJsonRegistrationAttachment regAttachment = new TransactionJsonRegistrationAttachment();
                regAttachment.UserID = NewAccountID;
                regAttachment.PublicKey = NewAccountPublicKey;
                RpcResult = SendTransaction(PrimaryWallet, TxType.AccountRegistration, regAttachment, long.Parse(coreConfig.Fee), long.Parse(coreConfig.Deadline), coreConfig.NetworkID, PrimaryWallet.AccountID, 0);

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return (RpcResult);
        }

        //send a deposit refill transaction.  Returns the response object or throws exception
        public RpcResponseClass Transaction_Refill(WalletClass wal, decimal amount)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
            RpcResult = SendTransaction(wal, TxType.DepositRefill, (long)amount, long.Parse(coreConfig.Fee), long.Parse(coreConfig.Deadline), coreConfig.NetworkID, wal.AccountID, 0);
            }
            catch (Exception ex)
            {
                throw ex;
            }


            return (RpcResult);
        }

        //send a deposit withdraw transaction.  Returns the response object or throws exception
        public RpcResponseClass Transaction_Withdraw(WalletClass wal, decimal amount)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {
                RpcResult = SendTransaction(wal, TxType.DepositWithdraw, (long)amount, long.Parse(coreConfig.Fee), long.Parse(coreConfig.Deadline), coreConfig.NetworkID, wal.AccountID, 0);
            }
            catch(Exception ex)
            {
                throw ex;
            }


            return (RpcResult);
        }

        //send an ordinary payment. Returns the response object or throws an error
        public RpcResponseClass Transaction_SendPayment(WalletClass wal, string recipient, decimal amount)
        {
            RpcResponseClass RpcResult = new RpcResponseClass();

            try
            {                
                Transaction_Payment_JsonTypes.Attachment paymentAttachment = new myEonClient.Transaction_Payment_JsonTypes.Attachment();
                paymentAttachment.Amount = (long)amount;
                paymentAttachment.Recipient = recipient;

                RpcResult = SendTransaction(wal, TxType.OrdinaryPayment, paymentAttachment, long.Parse(coreConfig.Fee), long.Parse(coreConfig.Deadline), coreConfig.NetworkID, wal.AccountID, 0);
            }
            catch(Exception ex)
            {
                throw ex;
            }

            return RpcResult;
        }

        public void testTX()
        {
            string privateKey = "c374a14708bdbe9dce41048aa08c2ad8bf995db6277823219ff89336e7024088";
            string Sender = "EON-CUHB2-6T2LQ-UGAYR";
            string publicKey = "215744730c4a3b4b068b3bf70569e3d5813ca490d19f50d60a17d7673043beaf";

            WalletClass wal = new WalletClass();

            wal.Seed = privateKey;
            wal.PublicKey = publicKey;
            wal.AccountID = Sender;

            byte[] exKey2 = StringToByteArray(privateKey + publicKey);


            SendTransaction(wal, TxType.DepositWithdraw, (long)9100000, 10, 60, coreConfig.NetworkID, wal.AccountID, (long)1505915697);


        }

        //Send a transaction.  Returns an RPC response object containing parsed fields as well as the raw request/response (or throws an exception)
        public RpcResponseClass SendTransaction(WalletClass _wal, TxType _txType, object _txObject, long _fee, long _deadline, string _networkID, string _senderID, long _tStamp)
        {
            //the return object
            RpcResponseClass RpcResult = new RpcResponseClass();

            //get a timestamp in UNIX format , allow override when parameter _tStamp is set to anything other than zero.
            long _timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (_tStamp != 0) _timestamp = _tStamp;

            //extract the correct attachment according to the TX type========================================================================================================================================================
            int txTypeCode = 0;
            long _amount = 0;
            string _recipient = "";
            string _newuserID = "";
            string _newuserPubKey = "";
            BDictionary bd1;
            BDictionary bd2;
            string txBencode = "";
            string jsonTX = "";
            string requestString = "";


            //used to sign the transaction
            byte[] seedBytes = StringToByteArray(_wal.Seed + _wal.PublicKey);

            byte[] signatureBytes = { 0 };
            string signatureString;

        try { 
                switch (_txType)
                {
                    case (TxType.OrdinaryPayment): // -----------------------------------------------------------

                        txTypeCode = 200;
                        _amount = ((Transaction_Payment_JsonTypes.Attachment)_txObject).Amount;
                        _recipient = ((Transaction_Payment_JsonTypes.Attachment)_txObject).Recipient;

                        //DebugMsg("Payment of " + _amount + " uEON to " + _recipient);

                        //create the attachement structure in a bencode dictionary object 
                        bd1 = new BDictionary { { "amount", _amount }, { "recipient", _recipient } };
                        //create bencode dictionary for the transaction, including the attachement 
                        bd2 = new BDictionary { { "attachment", bd1 }, { "deadline", _deadline }, { "fee", _fee }, { "network", _networkID }, { "sender", _senderID }, { "timestamp", _timestamp }, { "type", txTypeCode } };

                        //serialise to get the complete BENCODE format request, and uppercase it.
                        txBencode = bd2.EncodeAsString();
                        txBencode = txBencode.ToUpper();

                        //get the signature for the BENCODE request
                        signatureBytes = Sodium.PublicKeyAuth.SignDetached(txBencode, seedBytes);
                        signatureString = ByteArrayToString(signatureBytes);

                        //create the transaction json
                        Transaction_Payment_JsonTypes.TransactionJsonPayment tx = new Transaction_Payment_JsonTypes.TransactionJsonPayment();
                        tx.Attachment = new Transaction_Payment_JsonTypes.Attachment();
                        tx.Attachment.Amount = _amount;
                        tx.Attachment.Recipient = _recipient;
                        tx.Sender = _senderID;
                        tx.Type = txTypeCode;
                        tx.Deadline = _deadline;
                        tx.Timestamp = _timestamp;
                        tx.Fee = _fee;
                        tx.Signature = signatureString;
                        tx.Version = 1;

                        jsonTX = JsonConvert.SerializeObject(tx);

                        //send the transaction 
                        requestString = "{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"id\":" + currentTXID + ",\r\n\"method\": \"transactions.putTransaction\" ,\"params\":" + jsonTX + "}";
                        currentTXID++;
                        //DebugMsg("requestString = \r\n" + requestString);

                       // res = RequestRPC(requestString, coreConfig.Peer + "/bot");

                        RpcResult = new RpcResponseClass( requestString , RequestRPC(requestString, coreConfig.Peer + "/bot") );


                        //DebugMsg("Transaction Result : " + res);

                        break;



                    case (TxType.DepositRefill): // ------------------------------------------------------------

                        txTypeCode = 310;
                        //object is type LONG for this tx type                    
                        _amount = (long)_txObject;

                        DebugMsg("Deposit Refill of " + _amount + " uEON");

                        //create the attachement structure 
                        bd1 = new BDictionary { { "amount", _amount } };
                        //create bencode dictionary for the transaction, including the attachement
                        bd2 = new BDictionary { { "attachment", bd1 }, { "deadline", _deadline }, { "fee", _fee }, { "network", _networkID }, { "sender", _senderID }, { "timestamp", _timestamp }, { "type", txTypeCode } };

                        //serialise to get the complete BENCODE format request, and uppercase it.
                        txBencode = bd2.EncodeAsString();
                        txBencode = txBencode.ToUpper();

                        //get the signature for the BENCODE request
                        signatureBytes = Sodium.PublicKeyAuth.SignDetached(txBencode, seedBytes);
                        signatureString = ByteArrayToString(signatureBytes);

                        //create the transaction json
                        TransactionJsonDepWithdraw tR = new TransactionJsonDepWithdraw();
                        tR.Attachment = new Transaction_DepWithdraw_JsonTypes.Attachment();
                        tR.Attachment.Amount = _amount;
                        tR.Sender = _senderID;
                        tR.Type = txTypeCode;
                        tR.Deadline = _deadline;
                        tR.Timestamp = _timestamp;
                        tR.Fee = _fee;
                        tR.Signature = signatureString;

                        jsonTX = JsonConvert.SerializeObject(tR);

                        //send the transaction 
                        requestString = "{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"id\":" + currentTXID + ",\r\n\"method\": \"transactions.putTransaction\" ,\"params\":" + jsonTX + "}";
                        currentTXID++;
                        DebugMsg("requestString = \r\n" + requestString);

                        string responseString = RequestRPC(requestString, coreConfig.Peer + "/bot");

                        RpcResult = new RpcResponseClass(requestString, responseString);


                        //res = RequestRPC(requestString, coreConfig.Peer + "/bot");
                        //if (res.Contains("\"result\":\"success\"")) result = true;
                        //else result = false;
                        //DebugMsg("Transaction Result : " + res);


                        break;



                    case (TxType.DepositWithdraw): // ----------------------------------------------------------

                        txTypeCode = 320;
                        //object is type LONG for this tx type
                        _amount = (long)_txObject;

                        DebugMsg("Deposit Withdraw  of " + _amount + " uEON");

                        //create the attachement structure 
                        bd1 = new BDictionary { { "amount", _amount } };
                        //create bencode dictionary for the transaction, including the attachement
                        bd2 = new BDictionary { { "attachment", bd1 }, { "deadline", _deadline }, { "fee", _fee }, { "network", _networkID }, { "sender", _senderID }, { "timestamp", _timestamp }, { "type", txTypeCode } };

                        //serialise to get the complete BENCODE format request, and uppercase it.
                        txBencode = bd2.EncodeAsString();
                        txBencode = txBencode.ToUpper();

                        //var bencodeBytes = Encoding.UTF8.GetBytes(txBencode);

                        //get the signature for the BENCODE request
                        signatureBytes = Sodium.PublicKeyAuth.SignDetached(txBencode, seedBytes);
                        signatureString = ByteArrayToString(signatureBytes);

                        //create the transaction json
                        TransactionJsonDepWithdraw tW = new TransactionJsonDepWithdraw();
                        tW.Attachment = new Transaction_DepWithdraw_JsonTypes.Attachment();
                        tW.Attachment.Amount = _amount;
                        tW.Sender = _senderID;
                        tW.Type = txTypeCode;
                        tW.Deadline = _deadline;
                        tW.Timestamp = _timestamp;
                        tW.Fee = _fee;
                        tW.Signature = signatureString;

                        jsonTX = JsonConvert.SerializeObject(tW);

                        //send the transaction 
                        requestString = "{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"id\":" + currentTXID + ",\r\n\"method\": \"transactions.putTransaction\" ,\"params\":" + jsonTX + "}";
                        currentTXID++;
                        //DebugMsg("requestString = \r\n" + requestString);

                        RpcResult = new RpcResponseClass(requestString, RequestRPC(requestString, coreConfig.Peer + "/bot"));

                        //res = RequestRPC(requestString, coreConfig.Peer + "/bot");
                        //if (res.Contains("\"result\":\"success\"")) result = true;
                        //else result = false;
                        //DebugMsg("Transaction Result : " + res);

                        break;



                    case (TxType.AccountRegistration): // -----------------------------------------------------
                        txTypeCode = 100;
                        _newuserID = ((TransactionJsonRegistrationAttachment)_txObject).UserID;
                        _newuserPubKey = ((TransactionJsonRegistrationAttachment)_txObject).PublicKey;

                        DebugMsg("Registration of new account :" + _newuserID + " : " + _newuserPubKey);

                        //create the attachement structure 
                        bd1 = new BDictionary { { _newuserID, _newuserPubKey } };
                        //bd1 = new BDictionary { { (_newuserID + ":" + _newuserPubKey), "" } };
                        //BString attachString = _newuserID + ":" + _newuserPubKey;
                        //create bencode dictionary for the transaction, including the attachement
                        bd2 = new BDictionary { { "attachment", bd1 }, { "deadline", _deadline }, { "fee", _fee }, { "network", _networkID }, { "sender", _senderID }, { "timestamp", _timestamp }, { "type", txTypeCode } };

                        //serialise to get the complete BENCODE format request, and uppercase it.
                        txBencode = bd2.EncodeAsString();
                        txBencode = txBencode.ToUpper();

                        //get the signature for the BENCODE request
                        signatureBytes = Sodium.PublicKeyAuth.SignDetached(txBencode, seedBytes);
                        signatureString = ByteArrayToString(signatureBytes);

                        //create the transaction json manually . We cant make use of newtonsoft since the attachment format for new account does not follow the usual tag:value standard and cant be serialised/deserialised properly :(
                        jsonTX = "{\"version\":" + 1;
                        jsonTX += ",\"type\":" + txTypeCode;
                        jsonTX += ",\"timestamp\":" + _timestamp;
                        jsonTX += ",\"deadline\":" + _deadline;
                        jsonTX += ",\"fee\":" + _fee;
                        jsonTX += ",\"sender\":\"" + _senderID;
                        jsonTX += "\",\"signature\":\"" + signatureString;
                        jsonTX += "\",\"attachment\":{\"" + _newuserID;
                        jsonTX += "\":\"" + _newuserPubKey + "\"}}";

                        //send the transaction 
                        requestString = "{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"id\":" + currentTXID + ",\r\n\"method\": \"transactions.putTransaction\" ,\"params\":" + jsonTX + "}";
                        currentTXID++;
                        //DebugMsg("requestString = \r\n" + requestString);


                        RpcResult = new RpcResponseClass(requestString, RequestRPC(requestString, coreConfig.Peer + "/bot"));

                        //res = RequestRPC(requestString, coreConfig.Peer + "/bot");


                        //if (res.Contains("\"result\":\"success\"")) result = true;
                        //else result = false;

                        //DebugMsg("Transaction Result : " + res);

                        break;



                }

            }
            catch(Exception ex)
            {
                //ErrorMsg("SendTransaction() - Exception : " + ex.Message);
                throw ex;
            }
            //================================================================================================================================================================================================================


            return (RpcResult);
        }

        public bool GetTransactionPage(TransactionHistoryClass txHistory, string AccountID, int pageNumber)
        {
            bool returnVal = false;

            try
            {

                string rpcResp = RequestRPC("{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"method\": \"history.getCommittedPage\" ,\r\n\"params\":[\r\n\"" + AccountID + "\"," + pageNumber + "],\r\n\"id\": " + currentTXID + "\r\n}", coreConfig.Peer + "/bot");

                //first strip away the RPC response header
                int startIndex = rpcResp.IndexOf("result\":[") + 9;
                int endIndex = rpcResp.LastIndexOf("]}");
                string jsonTransactionList = rpcResp.Substring(startIndex, endIndex - startIndex);

                //split the list into individual transactions
                string pattern = @"{""attachment.+?(?=version)version"":.}";
                MatchCollection txCollection = Regex.Matches(jsonTransactionList, pattern);


                //matches 10 groups
                string patternNewAccount = @"{""attachment"":{""([^""]*)"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";
                string patternOrdinaryPayment = @"{""attachment"":{""amount"":([^,]*),""recipient"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";

                //matches 9 groups
                string patternRefillWithdraw = @"{""attachment"":{""amount"":([^,]*)},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";
                //string patternRefillWithdraw2 = "{{\"attachment\":{\"amount\":([^,]*)},\"deadline\":([^,]*),\"fee\":([^,]*),\"id\":\"([^\"]*)\",\"sender\":\"([^\"]*)\",\"signature\":\"([^\"]*)\",\"timestamp\":([^,]*),\"type\":([^,]*),\"version\":([^}]*)}}";



                //for each in txCollection , create a new transaction and add it to the txHistory
                foreach (Match m in txCollection)
                {
                    string txString = m.Value;

                    TransactionItemClass nTX = new TransactionItemClass();


                    //save the attachment
                    Match attachMatch = Regex.Match(txString, @"{""attachment"":([^}]*})");
                    nTX.Attachment = attachMatch.Groups[1].ToString();


                    //read ordinary payment transaction
                    Match opTX = Regex.Match(txString, patternOrdinaryPayment);
                    if (opTX.Groups.Count == 11)
                    {
                        nTX.AttachedAmount = decimal.Parse(opTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                        nTX.AttachedRecipient = opTX.Groups[2].ToString();
                        nTX.Deadline = int.Parse(opTX.Groups[3].ToString());
                        nTX.Fee = long.Parse(opTX.Groups[4].ToString());
                        nTX.Id = opTX.Groups[5].ToString();
                        nTX.Sender = opTX.Groups[6].ToString();
                        nTX.Signature = opTX.Groups[7].ToString();
                        nTX.Timestamp = opTX.Groups[8].ToString();

                        var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(nTX.Timestamp));
                        DateTime dateTime = dateTimeOffset.DateTime;
                        nTX.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();

                        //nTX.Type = opTX.Groups[9].ToString();
                        nTX.Type = TransactionTypeCodeToString(opTX.Groups[9].ToString());
                        nTX.Version = int.Parse(opTX.Groups[10].ToString());
                    }
                    else
                    {
                        //read withdraw/refill transaction
                        Match wrTX = Regex.Match(txString, patternRefillWithdraw);
                        if (wrTX.Groups.Count == 10)
                        {
                            nTX.AttachedAmount = decimal.Parse(wrTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                            nTX.Deadline = int.Parse(wrTX.Groups[2].ToString());
                            nTX.Fee = long.Parse(wrTX.Groups[3].ToString());
                            nTX.Id = wrTX.Groups[4].ToString();
                            nTX.Sender = wrTX.Groups[5].ToString();
                            nTX.Signature = wrTX.Groups[6].ToString();
                            nTX.Timestamp = wrTX.Groups[7].ToString();

                            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(nTX.Timestamp));
                            DateTime dateTime = dateTimeOffset.DateTime;
                            nTX.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();

                            //nTX.Type = wrTX.Groups[8].ToString();
                            nTX.Type = TransactionTypeCodeToString(wrTX.Groups[8].ToString());
                            nTX.Version = int.Parse(wrTX.Groups[9].ToString());

                        }
                        else
                        {
                            //read new account transaction
                            Match naTX = Regex.Match(txString, patternNewAccount);

                            if (naTX.Groups.Count == 11)
                            {
                                nTX.AttachedNewUserID = naTX.Groups[1].ToString();
                                nTX.AttachedNewUserPubKey = naTX.Groups[2].ToString();
                                nTX.Deadline = int.Parse(naTX.Groups[3].ToString());
                                nTX.Fee = long.Parse(naTX.Groups[4].ToString());
                                nTX.Id = naTX.Groups[5].ToString();
                                nTX.Sender = naTX.Groups[6].ToString();
                                nTX.Signature = naTX.Groups[7].ToString();
                                nTX.Timestamp = naTX.Groups[8].ToString();

                                var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(nTX.Timestamp));
                                DateTime dateTime = dateTimeOffset.DateTime;
                                nTX.Timestamp = dateTime.ToShortTimeString() + "  " + dateTime.ToShortDateString();

                                nTX.Type = TransactionTypeCodeToString(naTX.Groups[9].ToString());
                                nTX.Version = int.Parse(naTX.Groups[10].ToString());
                            }
                        }
                    }

                    txHistory.ConfirmedTransactionCollection.Add(nTX);

                }

                returnVal = true;

                
            }
            catch(Exception ex)
            {
                ErrorMsg("GetTransactionPage() - Exception : " + ex.Message);
                returnVal = false;
            }

            return (returnVal);
        }

        public List<TransactionItemClass> GetRecentConfirmedTransactions(string AccountID)
        {
            List<TransactionItemClass> cList = new List<TransactionItemClass>();

            int pageNumber = 0;

            try
            {
                //request page 1
                string rpcResp = RequestRPC("{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"method\": \"history.getCommittedPage\" ,\r\n\"params\":[\r\n\"" + AccountID + "\"," + pageNumber + "],\r\n\"id\": " + currentTXID + "\r\n}", coreConfig.Peer + "/bot");

                //first strip away the RPC response header
                int startIndex = rpcResp.IndexOf("result\":[") + 9;
                int endIndex = rpcResp.LastIndexOf("]}");
                string jsonTransactionList = rpcResp.Substring(startIndex, endIndex - startIndex);

                //split the list into individual transactions
                string pattern = @"{""attachment.+?(?=version)version"":.}";
                MatchCollection txCollection = Regex.Matches(jsonTransactionList, pattern);

                //matches 10 groups
                string patternNewAccount = @"{""attachment"":{""([^""]*)"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";
                string patternOrdinaryPayment = @"{""attachment"":{""amount"":([^,]*),""recipient"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";

                //matches 9 groups
                string patternRefillWithdraw = @"{""attachment"":{""amount"":([^,]*)},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";

                //for each in txCollection , create a new transaction and add it to the txHistory
                foreach (Match m in txCollection)
                {
                    string txString = m.Value;

                    TransactionItemClass nTX = new TransactionItemClass();


                    //read ordinary payment transaction
                    Match opTX = Regex.Match(txString, patternOrdinaryPayment);
                    if (opTX.Groups.Count == 11)
                    {
                        nTX.AttachedAmount = decimal.Parse(opTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                        nTX.AttachedRecipient = opTX.Groups[2].ToString();
                        nTX.Deadline = int.Parse(opTX.Groups[3].ToString());
                        nTX.Fee = long.Parse(opTX.Groups[4].ToString());
                        nTX.Id = opTX.Groups[5].ToString();
                        nTX.Sender = opTX.Groups[6].ToString();
                        nTX.Signature = opTX.Groups[7].ToString();
                        nTX.Timestamp = opTX.Groups[8].ToString();
                        nTX.Type = opTX.Groups[9].ToString();
                        nTX.Version = int.Parse(opTX.Groups[10].ToString());
                    }
                    else
                    {
                        //read withdraw/refill transaction
                        Match wrTX = Regex.Match(txString, patternRefillWithdraw);
                        if (wrTX.Groups.Count == 10)
                        {
                            nTX.AttachedAmount = decimal.Parse(wrTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                            nTX.Deadline = int.Parse(wrTX.Groups[2].ToString());
                            nTX.Fee = long.Parse(wrTX.Groups[3].ToString());
                            nTX.Id = wrTX.Groups[4].ToString();
                            nTX.Sender = wrTX.Groups[5].ToString();
                            nTX.Signature = wrTX.Groups[6].ToString();
                            nTX.Timestamp = wrTX.Groups[7].ToString();
                            nTX.Type = wrTX.Groups[8].ToString();
                            nTX.Version = int.Parse(wrTX.Groups[9].ToString());

                        }
                        else
                        {
                            //read new account transaction
                            Match naTX = Regex.Match(txString, patternNewAccount);

                            if (naTX.Groups.Count == 11)
                            {
                                nTX.AttachedNewUserID = naTX.Groups[1].ToString();
                                nTX.AttachedNewUserPubKey = naTX.Groups[2].ToString();
                                nTX.Deadline = int.Parse(naTX.Groups[3].ToString());
                                nTX.Fee = long.Parse(naTX.Groups[4].ToString());
                                nTX.Id = naTX.Groups[5].ToString();
                                nTX.Sender = naTX.Groups[6].ToString();
                                nTX.Signature = naTX.Groups[7].ToString();
                                nTX.Timestamp = naTX.Groups[8].ToString();
                                nTX.Type = naTX.Groups[9].ToString();
                                nTX.Version = int.Parse(naTX.Groups[10].ToString());
                            }
                        }
                    }

                    cList.Add(nTX);

                }

            }
            catch (Exception ex)
            {
                ErrorMsg("GetRecentConfirmedTransactions(\""+ AccountID + "\") - Exception : " + ex.Message);
            }

            return cList;
        }

        private string TransactionTypeCodeToString(string TransactionTypeCode)
        {
            string res = "";

            switch(TransactionTypeCode)
            {
                case ("100"):
                    res = "Account Registration";
                    break;

                case ("200"):
                    res = "Payment";
                    break;

                case ("310"):
                    res = "Deposit Refill";
                    break;

                case ("320"):
                    res = "Deposit Withdraw";
                    break;

                default:
                    res = "";
                    break;
            }
            return res;
        }

        public List<TransactionItemClass> GetUnconfirmedTransactions(string AccountID)
        {
            List<TransactionItemClass> list = new List<TransactionItemClass>();

            try
            {
                string rpcResp = RequestRPC("{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"method\": \"history.getUncommitted\" ,\r\n\"params\":[\r\n\"" + AccountID + "\"],\r\n\"id\": " + currentTXID + "\r\n}", coreConfig.Peer + "/bot");

                //first strip away the RPC response header
                int startIndex = rpcResp.IndexOf("result\":[") + 9;
                int endIndex = rpcResp.LastIndexOf("]}");
                string jsonTransactionList = rpcResp.Substring(startIndex, endIndex - startIndex);

                //split the list into individual transactions
                string pattern = @"{""attachment.+?(?=version)version"":.}";
                MatchCollection txCollection = Regex.Matches(jsonTransactionList, pattern);

                if (txCollection.Count > 0)
                {
                    //matches 10 groups
                    string patternNewAccount = @"{""attachment"":{""([^""]*)"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";
                    string patternOrdinaryPayment = @"{""attachment"":{""amount"":([^,]*),""recipient"":""([^""]*)""},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";

                    //matches 9 groups
                    string patternRefillWithdraw = @"{""attachment"":{""amount"":([^,]*)},""deadline"":([^,]*),""fee"":([^,]*),""id"":""([^""]*)"",""sender"":""([^""]*)"",""signature"":""([^""]*)"",""timestamp"":([^,]*),""type"":([^,]*),""version"":([^}]*)}";


                    //for each in txCollection , create a new transaction and add it to the txHistory
                    foreach (Match m in txCollection)
                    {
                        string txString = m.Value;

                        TransactionItemClass nTX = new TransactionItemClass();


                        //read ordinary payment transaction
                        Match opTX = Regex.Match(txString, patternOrdinaryPayment);
                        if (opTX.Groups.Count == 11)
                        {
                            nTX.AttachedAmount = decimal.Parse(opTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                            //nTX.AttachedAmount = Decimal.Parse(opTX.Groups[1].ToString());
                            nTX.AttachedRecipient = opTX.Groups[2].ToString();
                            nTX.Deadline = int.Parse(opTX.Groups[3].ToString());
                            nTX.Fee = long.Parse(opTX.Groups[4].ToString());
                            nTX.Id = opTX.Groups[5].ToString();
                            nTX.Sender = opTX.Groups[6].ToString();
                            nTX.Signature = opTX.Groups[7].ToString();
                            nTX.Timestamp = opTX.Groups[8].ToString();
                            nTX.Type = opTX.Groups[9].ToString();
                            nTX.Version = int.Parse(opTX.Groups[10].ToString());
                        }
                        else
                        {
                            //read withdraw/refill transaction
                            Match wrTX = Regex.Match(txString, patternRefillWithdraw);
                            if (wrTX.Groups.Count == 10)
                            {
                                nTX.AttachedAmount = decimal.Parse(wrTX.Groups[1].ToString(), CultureInfo.InvariantCulture) / 1000000;
                                nTX.Deadline = int.Parse(wrTX.Groups[2].ToString());
                                nTX.Fee = long.Parse(wrTX.Groups[3].ToString());
                                nTX.Id = wrTX.Groups[4].ToString();
                                nTX.Sender = wrTX.Groups[5].ToString();
                                nTX.Signature = wrTX.Groups[6].ToString();
                                nTX.Timestamp = wrTX.Groups[7].ToString();
                                nTX.Type = wrTX.Groups[8].ToString();
                                nTX.Version = int.Parse(wrTX.Groups[9].ToString());

                            }
                            else
                            {
                                //read new account transaction
                                Match naTX = Regex.Match(txString, patternNewAccount);

                                if (naTX.Groups.Count == 11)
                                {
                                    nTX.AttachedNewUserID = naTX.Groups[1].ToString();
                                    nTX.AttachedNewUserPubKey = naTX.Groups[2].ToString();
                                    nTX.Deadline = int.Parse(naTX.Groups[3].ToString());
                                    nTX.Fee = long.Parse(naTX.Groups[4].ToString());
                                    nTX.Id = naTX.Groups[5].ToString();
                                    nTX.Sender = naTX.Groups[6].ToString();
                                    nTX.Signature = naTX.Groups[7].ToString();
                                    nTX.Timestamp = naTX.Groups[8].ToString();
                                    nTX.Type = naTX.Groups[9].ToString();
                                    nTX.Version = int.Parse(naTX.Groups[10].ToString());
                                }
                            }
                        }

                        list.Add(nTX);

                    }



                }

            }
            catch(Exception ex)
            {

            }

                return list;
        }

        //
        public GetAttributesResponseClass Peer_GetAttributes()
        {
            GetAttributesResponseClass responseObject = new GetAttributesResponseClass();

            try
            {

                string req = "{\n\"jsonrpc\": \"2.0\" ,\n\"id\":" + currentTXID +",\n\"method\": \"metadata.getAttributes\"\n}";
                currentTXID++;
                string rpcResp = RequestRPC(req, coreConfig.Peer + "/peer");

                string pattern = @"{""announced_address"":""([^""]*)"",""application"":""([^""]*)"",""version"":""([^""]*)"",""network_id"":""([^""]*)"",""fork"":([^,]*),""peer_id"":([^}]*)}";
                Match responseMatch = Regex.Match(rpcResp, pattern);

                responseObject.AnnouncedAddress = responseMatch.Groups[1].ToString();
                responseObject.Application = responseMatch.Groups[2].ToString();
                responseObject.Version = responseMatch.Groups[3].ToString();
                responseObject.NetworkID = responseMatch.Groups[4].ToString();
                responseObject.Fork = int.Parse(responseMatch.Groups[5].ToString());
                responseObject.PeerID = long.Parse(responseMatch.Groups[6].ToString());
                
            }
            catch(Exception ex)
            {
                ErrorMsg("Peer_GetAttributes exception : " + ex.Message);
            }

            return responseObject;
        }

        public void UpdateNetworkID()
        {
            try
            {

                GetAttributesResponseClass attributes = Peer_GetAttributes();
                coreConfig.NetworkID = attributes.NetworkID;

                string PeerID = GetAccountID(attributes.PeerID, "EON");

                DebugMsg("Peer ID : " + PeerID + "  |  Version : " + attributes.Version + "  |  Fork : " + attributes.Fork + "  |  NetworkID : " + attributes.NetworkID);

            }
            catch (Exception ex)
            {
                ErrorMsg("UpdateNetworkID() - Exception : " + ex.Message);
            }

        }

        //executes an RPC request against an EON peer server. returns the raw rpc response string (or throws an exception)
        private string RequestRPC(string jsonRequest, string serverURL)
        {
            string response = "";

            try
            {

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(serverURL);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";



                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(jsonRequest);
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    response = streamReader.ReadToEnd();
                }

            }
            catch (Exception ex)
            {
                response = "-1";
                throw ex;
                
            }

            return (response);
        }

        public enum TxType
        {
            AccountRegistration,
            OrdinaryPayment,
            DepositRefill,
            DepositWithdraw
        }

        public class AccountInfoResponseClass
        {
            public string CodeNumber = "";
            public string CodeName = "";
            public string PublicKey = "";
            public string Amount = "";
            public string Deposit = "";
            public string SignType = "";

        }

        public class GetAttributesResponseClass
        {
            public string AnnouncedAddress = "",
                Application = "",
                Version = "",
                NetworkID = "";
            public int Fork = 0;
            public long PeerID = 0;

        }

    }
}
