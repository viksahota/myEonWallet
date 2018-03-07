using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sodium;
using BencodeNET.Objects;
using System.Numerics;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace myEonClient
{
    public class EonUtil
    {
        public EonUtil()
        {
            currentTXID = 1;
        }

        //consumers can attach to the debug event.
        public event EventHandler<string> DebugEvent;

        //we increase this value with each rpc call.
        private int currentTXID;

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

        public WalletClass CreateAccount(string wallet_name)
        {

            //create a random seed value
            byte[] seedBytes = SodiumCore.GetRandomBytes(32);

            //generate a keypair
            KeyPair kp = Sodium.PublicKeyAuth.GenerateKeyPair(seedBytes);
            string privKeyS = ByteArrayToString(seedBytes);
            string pubKeyS = ByteArrayToString(kp.PublicKey);

            long accountNumber = GetAccountNumber(kp.PublicKey);
            string accountString = GetAccountID(accountNumber, "EON");

            WalletClass newWal = new WalletClass
            {
                Seed = privKeyS,
                PublicKey = pubKeyS,
                AccountID = accountString,
                NickName = wallet_name
            };

            DebugMsg("--------New account details ----------");
            DebugMsg("Priv Key: " + privKeyS);
            DebugMsg("Pub Key: " + pubKeyS);
            //DebugMsg("AccountNumber: " + accountNumber);
            DebugMsg("AccountID: " + accountString);
            DebugMsg("--------------------------------------");
            return newWal;
        }



        // encodes a numerical account number to its string representation , eg EON-xxxx-xxxx-xxxx
        private String GetAccountID(long accountID_long, String prefix)
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

            StringBuilder idStr = new StringBuilder(prefix);
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

            return idStr.ToString();
        }


        //get the EON account number (numerical) from a public key
        public long GetAccountNumber(byte[] publicKey)
        {
            //create a SHA512 hash using the public key
            byte[] hashBytes = Sodium.CryptoHash.Sha512(publicKey);

            //swap to big endian
            Array.Reverse(hashBytes);

            BigInteger bigInteger = BigInteger.Zero;

            try
            {

                //byte[] hash = MessageDigest.getInstance(MessageDigestAlgorithm).digest(bytes);


                for (int i = 0; i < hashBytes.Length; i += 8)
                {

                    BigInteger bi = new BigInteger(new byte[] { hashBytes[i + 7], hashBytes[i + 6], hashBytes[i + 5], hashBytes[i + 4], hashBytes[i + 3], hashBytes[i + 2], hashBytes[i + 1], hashBytes[i] });


                    bigInteger = bigInteger ^ bi;

                    DebugMsg("bi = " + bi + " , bigInteger =" + bigInteger);

                }




                return (long)bigInteger;

            }
            catch (Exception  ex)
            {
                return (long)bigInteger;
            }

        }

        private void GetInformation()
        {
            //getInformation
            string res = RequestRPC("{\r\n\"jsonrpc\": \"2.0\" ,\r\n\"method\": \"accounts.getInformation\" ,\r\n\"params\":[\r\n\"EON-RTQJW-AND3F-GA8JR\"\r\n],\r\n\"id\": 3\r\n}", "https://peer.testnet.eontechnology.org:9443/bot");
            DebugMsg(res);
        }


        public bool SendTransaction(WalletClass _wal, TxType _txType, object _txObject, long _fee, long _deadline, string _networkID, string _senderID, long _tStamp)
        {
            bool result = false;

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
            string res = "";

            //used to sign the transaction
            byte[] seedBytes = StringToByteArray(_wal.Seed + _wal.PublicKey);

            byte[] signatureBytes = { 0 };
            string signatureString;


            switch (_txType)
            {
                case (TxType.OrdinaryPayment): // -----------------------------------------------------------

                    txTypeCode = 200;
                    _amount = ((Transaction_Payment_JsonTypes.Attachment)_txObject).Amount;
                    _recipient = ((Transaction_Payment_JsonTypes.Attachment)_txObject).Recipient;

                    DebugMsg("Payment of " + _amount + " uEON to " + _recipient);

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

                    res = RequestRPC(requestString, "https://peer.testnet.eontechnology.org:9443/bot");
                    if (res.Contains("\"result\":\"success\"")) result = true;
                    else result = false;
                    DebugMsg("Transaction Result : " + res);

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
                    //DebugMsg("requestString = \r\n" + requestString);

                    res = RequestRPC(requestString, "https://peer.testnet.eontechnology.org:9443/bot");
                    if (res.Contains("\"result\":\"success\"")) result = true;
                    else result = false;
                    DebugMsg("Transaction Result : " + res);


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

                    res = RequestRPC(requestString, "https://peer.testnet.eontechnology.org:9443/bot");
                    if (res.Contains("\"result\":\"success\"")) result = true;
                    else result = false;
                    DebugMsg("Transaction Result : " + res);

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

                    res = RequestRPC(requestString, "https://peer.testnet.eontechnology.org:9443/bot");

                    if (res.Contains("\"result\":\"success\"")) result = true;
                    else result = false;

                    DebugMsg("Transaction Result : " + res);

                    break;



            }
            //================================================================================================================================================================================================================


            return (result);
        }


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
                DebugMsg("Exception during RequestRPC --- " + ex.Message);
                response = "-1";
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


    }
}
