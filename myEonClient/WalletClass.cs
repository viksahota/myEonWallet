using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myEonClient
{
    public class WalletClass
    {
        public WalletClass()
        {
            NickName = "";
            Seed = "";
            AccountNumber = "";
            AccountID = "";
            PublicKey = "";
            Balance = "0";
            Deposit = "0";

        }

        public string NickName { get; set; }
        public string Seed { get; set; }
        public string AccountNumber { get; set; }
        public string AccountID { get; set; }
        public string PublicKey { get; set; }
        public string Balance { get; set; }
        public string Deposit { get; set; }
    }
}
