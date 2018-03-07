using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EonSharp;

namespace myEonClient
{
    interface IMyEonClientInterface
    {
        
        void Start();

        
        Wallet CreateAccount(string name, string password);

        bool Wallets_Add(EonSharp.Wallet wal);
        bool Wallets_Backup(string filePath);
        bool Wallets_Restore(string filePath);
        bool Wallets_Reset();
        int Wallets_GetCount();

        //float Wallet_GetBalance(int index);
        //string Wallet_GetBalanceString(int index);
        //float Wallet_GetDeposit(int index);
        //string Wallet_GetDepositString(int index);

        Task<RpcResponseClass> Transaction_Register(Wallet newWallet, string senderPassword);



    }
}
