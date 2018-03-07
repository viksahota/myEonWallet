using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace myEonClient
{
    public class WalletClass : INotifyPropertyChanged
    {
        public WalletClass()
        {
            NickName = "";
            Seed = "";
            AccountID = "";
            PublicKey = "";
            Balance = "0";
            Deposit = "0";

        }

        private string nickName;
        public string NickName
        {
            get { return this.nickName; }
            set
            {
                if (this.nickName != value)
                {
                    this.nickName = value;
                    this.NotifyPropertyChanged("NickName");
                }
            }
        }

        private string seed;
        public string Seed
        {
            get { return this.seed; }
            set
            {
                if (this.seed != value)
                {
                    this.seed = value;
                    this.NotifyPropertyChanged("Seed");
                }
            }
        }

        private string accountID;
        public string AccountID
        {
            get { return this.accountID; }
            set
            {
                if (this.accountID != value)
                {
                    this.accountID = value;
                    this.NotifyPropertyChanged("AccountID");
                }
            }
        }

        private string publicKey;
        public string PublicKey
        {
            get { return this.publicKey; }
            set
            {
                if (this.publicKey != value)
                {
                    this.publicKey = value;
                    this.NotifyPropertyChanged("PublicKey");
                }
            }
        }

        private string balance;
        public string Balance
        {
            get { return this.balance; }
            set
            {
                if (this.balance != value)
                {
                    this.balance = value;
                    this.NotifyPropertyChanged("Balance");
                }
            }
        }

        private string deposit;
        public string Deposit
        {
            get { return this.deposit; }
            set
            {
                if (this.deposit != value)
                {
                    this.deposit = value;
                    this.NotifyPropertyChanged("Deposit");
                }
            }
        }


        //public string NickName { get; set; }
        //public string Seed { get; set; }
        //public string AccountID { get; set; }
        //public string PublicKey { get; set; }
        //public string Balance { get; set; }
        //public string Deposit { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

    }
}
