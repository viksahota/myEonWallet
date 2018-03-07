using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace myEonClient
{
    public class RpcResponseClass
    {
        //example of rpc call response
        //
        //  {\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32602,\"message\":\"Unknown recipient.\"},\"id\":null}
        //{"jsonrpc":"2.0","([^"]*)":{"code":([^,]*),"message":"([^ "]*)"}
        //{"jsonrpc":"2.0","id":"1324","result":"success"}


        public bool Result;
        public string ID, Message, RpcVersion, RpcRequest, RpcResponse;
        public long Code = 0;


        public RpcResponseClass()
        {
            RpcRequest = "";
            RpcResponse = "";
            Result = false;
            Code = 0;
            Message = "";
            ID = "";
        }
            public RpcResponseClass(string rpcRequestRaw, string rpcResponseRaw)
        {
            Result = false;
            Code = 0;
            Message = "";
            ID = "";

            try
            {
                //get the rpc version
                string rpcVersionPattern = @"""jsonrpc"":""([^""]*)";
                Match rpcVersionMatch = Regex.Match(rpcResponseRaw, rpcVersionPattern);
                RpcVersion = rpcVersionMatch.Groups[1].ToString();

                //get the ID
                string idPattern = @"""id"":""([^""]*)""";
                Match _idMatch = Regex.Match(rpcResponseRaw, idPattern);
                if (_idMatch.Groups.Count==2)
                {
                    ID = _idMatch.Groups[1].ToString();
                }

                //match success
                string resultPattern = @"""result"":""([^""]*)""";
                Match _resultMatch = Regex.Match(rpcResponseRaw, resultPattern);

                

                if (_resultMatch.Groups.Count == 2)
                {
                    if (_resultMatch.Groups[1].ToString() == "success")
                    {
                        Result = true;
                    }                    
                }
                else
                {
                    //match for error result
                    string errorPattern = @"""error"":{([^}]*)";
                    Match errorMatch = Regex.Match(rpcResponseRaw, errorPattern);

                    if (errorMatch.Groups.Count ==2)
                    {
                        //try get the code
                        string codePattern = @"""code"":([^,]*)";
                        Match codeMatch = Regex.Match(rpcResponseRaw, codePattern);
                        if (codeMatch.Groups.Count == 2) Code = long.Parse(codeMatch.Groups[1].ToString());

                        //try get the message
                        string messagePattern = @"""message"":""([^""]*)";
                        Match messageMatch = Regex.Match(rpcResponseRaw, messagePattern);
                        if (messageMatch.Groups.Count == 2) Message = messageMatch.Groups[1].ToString();

                        Result = false;
                    }

                }
                



                //string pattern = @"{""jsonrpc"":""([^""]*)"",""([^""]*)"":{""code"":([^,]*),""message"":""([^""]*)""},""id"":([^}]*)";
                //Match rpcMatch = Regex.Match(rpcResponseRaw, pattern);

                /*RpcVersion = rpcMatch.Groups[1].ToString();
                if (rpcMatch.Groups[2].ToString() == "success") Result = true;
                else Result = false;
                Code = long.Parse(rpcMatch.Groups[3].ToString());
                Message = rpcMatch.Groups[4].ToString();
                ID = rpcMatch.Groups[5].ToString();
                */            
                RpcResponse = rpcResponseRaw;
                RpcRequest = rpcRequestRaw;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
