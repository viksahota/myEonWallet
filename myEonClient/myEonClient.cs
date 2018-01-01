using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myEonClient
{
    public class MyEonClient
    {

        public WalletManagerClass WalletManager;

        //class constructor
        public MyEonClient()
        {            
            WalletManager = new WalletManagerClass();

            //catch and forward Debug and error events from WalletManager and forward
            WalletManager.DebugEvent += (sender, msg) => {DebugMsg(msg);};
            WalletManager.ErrorEvent += (sender, msg) => {ErrorMsg(msg);};


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
                msg = "MyEONClient." + msg;
                DebugEvent?.Invoke(this, msg);
            }
        }

        // Invoke the error callback to provide feedback to consumer
        private void ErrorMsg(string msg)
        {
            if (ErrorEventEnable)
            {
                //add a custom tag to the message and despatch it
                msg = "MyEonClient." + msg;
                ErrorEvent?.Invoke(this, msg);
            }
        }
        #endregion
    }
}
