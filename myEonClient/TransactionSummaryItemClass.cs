using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace myEonClient
{
    public class TransactionSummaryItemClass : INotifyPropertyChanged
    {

        public TransactionSummaryItemClass()
        {
            type = "";
            timestamp = "";
            sender = "";
            attachedAmount = 0;
            attachedRecipient = "";
            status = "";
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

        private string status;
        public string Status
        {
            get { return this.status; }
            set
            {
                if (this.status != value)
                {
                    this.status = value;
                    this.NotifyPropertyChanged("Status");
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
