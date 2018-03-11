using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace myEonClient
{
    public class TransactionItemClass : INotifyPropertyChanged
    {

        public TransactionItemClass()
        {
            version = -1 ;
            type = "";
            timestamp = "";
            deadline = 0;
            fee = 0;
            sender = "";
            signature = "";
            attachedAmount = 0;
            AttachedRecipient = "";
            AttachedNewUserID = "";
            AttachedNewUserPubKey = "";

        }

        private int version;
        public int Version
        {
            get { return this.version; }
            set
            {
                if (this.version != value)
                {
                    this.version = value;
                    this.NotifyPropertyChanged("Version");
                }
            }
        }

        private string type;
        public string Type
        {
            get { return this.type; }
            set
            {
                if (this.type != value)
                {
                    this.type = value;
                    this.NotifyPropertyChanged("Type");
                }
            }
        }

        private string timestamp;
        public string Timestamp
        {
            get { return this.timestamp; }
            set
            {
                if (this.timestamp != value)
                {
                    this.timestamp = value;
                    this.NotifyPropertyChanged("Timestamp");
                }
            }
        }

        private int deadline;
        public int Deadline
        {
            get { return this.deadline; }
            set
            {
                if (this.deadline != value)
                {
                    this.deadline = value;
                    this.NotifyPropertyChanged("Deadline");
                }
            }
        }

        private long fee;
        public long Fee
        {
            get { return this.fee; }
            set
            {
                if (this.fee != value)
                {
                    this.fee = value;
                    this.NotifyPropertyChanged("Fee");
                }
            }
        }

        private string id;
        public string Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value;
                    this.NotifyPropertyChanged("Id");
                }
            }
        }

        private string sender;
        public string Sender
        {
            get { return this.sender; }
            set
            {
                if (this.sender != value)
                {
                    this.sender = value;
                    this.NotifyPropertyChanged("Sender");
                }
            }
        }

        private string signature;
        public string Signature
        {
            get { return this.signature; }
            set
            {
                if (this.signature != value)
                {
                    this.signature = value;
                    this.NotifyPropertyChanged("Signature");
                }
            }
        }

        private decimal attachedAmount;
        public decimal AttachedAmount
        {
            get { return this.attachedAmount; }
            set
            {
                if (this.attachedAmount != value)
                {
                    this.attachedAmount = value;
                    this.NotifyPropertyChanged("AttachedAmount");
                }
            }
        }

        private string attachedRecipient;
        public string AttachedRecipient
        {
            get { return this.attachedRecipient; }
            set
            {
                if (this.attachedRecipient != value)
                {
                    this.attachedRecipient = value;
                    this.NotifyPropertyChanged("AttachedRecipient");
                }
            }
        }

        private string attachedNewUserID;
        public string AttachedNewUserID
        {
            get { return this.attachedNewUserID; }
            set
            {
                if (this.attachedNewUserID != value)
                {
                    this.attachedNewUserID = value;
                    this.NotifyPropertyChanged("AttachedNewUserID");
                }
            }
        }

        private string attachedNewUserPubKey;
        public string AttachedNewUserPubKey
        {
            get { return this.attachedNewUserPubKey; }
            set
            {
                if (this.attachedNewUserPubKey != value)
                {
                    this.attachedNewUserPubKey = value;
                    this.NotifyPropertyChanged("AttachedNewUserPubKey");
                }
            }
        }

        private long attachedColorCoinEmission;
        public long AttachedColorCoinEmission
        {
            get { return this.attachedColorCoinEmission; }
            set
            {
                if (this.attachedColorCoinEmission != value)
                {
                    this.attachedColorCoinEmission = value;
                    this.NotifyPropertyChanged("AttachedColorCoinEmission");
                }
            }
        }

        private int attachedColorCoinDecimals;
        public int AttachedColorCoinDecimals
        {
            get { return this.attachedColorCoinDecimals; }
            set
            {
                if (this.attachedColorCoinDecimals != value)
                {
                    this.attachedColorCoinDecimals = value;
                    this.NotifyPropertyChanged("AttachedColorCoinDecimals");
                }
            }
        }

        private string attachment;
        public string Attachment
        {
            get { return this.attachment; }
            set
            {
                if (this.attachment != value)
                {
                    this.attachment = value;
                    this.NotifyPropertyChanged("Attachment");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

    }
}
