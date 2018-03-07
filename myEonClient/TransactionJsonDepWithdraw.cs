using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace myEonClient.Transaction_DepWithdraw_JsonTypes
{

    public class Attachment
    {
        [JsonProperty("amount")]
        public long Amount { get; set; }
    }

}

namespace myEonClient
{

    public class TransactionJsonDepWithdraw
    {

        [JsonProperty("sender")]
        public string Sender { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("deadline")]
        public long Deadline { get; set; }

        [JsonProperty("attachment")]
        public Transaction_DepWithdraw_JsonTypes.Attachment Attachment { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("fee")]
        public long Fee { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }
    }

}
