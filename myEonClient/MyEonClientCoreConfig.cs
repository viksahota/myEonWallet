using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace myEonClient
{
    public class MyEonClientCoreConfig : INotifyPropertyChanged
    {
        public event EventHandler<string> PeerValidatedAddressEvent;

        public MyEonClientCoreConfig()
        {
            coin = "eon";
            //peer = "https://peer.testnet.eontechnology.org:9443";
            peer = Properties.Settings.Default.CoreConfig_Peer;
            deadline = "60";
            fee = "10";
            networkID = "EON-B-2CMBX-669EY-TWFBK";
        }

        //trigger event to signal the address is valid and was accepted
        private void PeerValidatedAddressMsg(string msg)
        {
            PeerValidatedAddressEvent?.Invoke(this, msg);
        }


        private string coin;
        public string Coin
        {
            get { return this.coin; }
            set
            {
                if (this.coin != value)
                {
                    this.coin = value;
                    this.NotifyPropertyChanged("Coin");
                }
            }
        }

        private string networkID;
        public string NetworkID
        {
            get { return this.networkID; }
            set
            {
                if (this.networkID != value)
                {
                        this.networkID = value;
                        this.NotifyPropertyChanged("NetworkID");
                }
            }
        }

        private string peer;
        public string Peer
        {
            get { return this.peer; }
            set
            {
                if (this.peer != value)
                {
                    //validate the peer address.
                    //expected : domain-name : port 
                    // or IP address : port
                    string pattern = @"(http:\/\/|https:\/\/)((([0-9]\.){4})|[a-z0-9\-_\.][a-z0-9\-_\.]+)(\:)([0-9]{1,5})";
                    Match urlMatch = Regex.Match(value, pattern);

                    if (urlMatch.Groups.Count == 7)
                    {
                        this.peer = value;
                        this.NotifyPropertyChanged("Peer");
                        PeerValidatedAddressMsg("VALID");
                        Properties.Settings.Default.CoreConfig_Peer = value;
                        Properties.Settings.Default.Save();
                    }
                    else
                    {
                        this.peer = value;
                        PeerValidatedAddressMsg("INVALID");
                    }


                }
            }
        }

        private string deadline;
        public string Deadline
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

        private string fee;
        public string Fee
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


        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

    }
}
