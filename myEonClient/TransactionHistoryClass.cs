using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myEonClient
{
    public class TransactionHistoryClass
    {

        //collection of confirmed transactions
        public ObservableCollection<TransactionItemClass> ConfirmedTransactionCollection;

        //collection summary of unconfirmed and most recent confirmed transactions
        public ObservableCollection<TransactionSummaryItemClass> SummaryTransactionCollection;


        //constructor
        public TransactionHistoryClass()
        {
            ConfirmedTransactionCollection = new ObservableCollection<TransactionItemClass>();

            SummaryTransactionCollection = new ObservableCollection<TransactionSummaryItemClass>();

        }

           


    }
}
