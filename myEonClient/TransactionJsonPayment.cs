using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace myEonClient.Transaction_Payment_JsonTypes
{

    public class Attachment
    {

        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("recipient")]
        public string Recipient { get; set; }
    }

    public class TransactionJsonPayment
    {

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("deadline")]
        public long Deadline { get; set; }

        [JsonProperty("fee")]
        public long Fee { get; set; }

        [JsonProperty("sender")]
        public string Sender { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonProperty("attachment")]
        public Attachment Attachment { get; set; }
    }

}
