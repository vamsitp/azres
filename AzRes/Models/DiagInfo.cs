namespace AzRes
{
    public class DiagInfo
    {
        public Value[] value { get; set; }
        public Error error { get; set; }
        public string code { get; set; }
        public string message { get; set; }
    }

    public class Value
    {
        public string id { get; set; }
        public object type { get; set; }
        public string name { get; set; }
        public object location { get; set; }
        public object kind { get; set; }
        public object tags { get; set; }
        public Properties properties { get; set; }
        public object identity { get; set; }
    }

    public class Properties
    {
        public object storageAccountId { get; set; }
        public object serviceBusRuleId { get; set; }
        public string workspaceId { get; set; }
        public object eventHubAuthorizationRuleId { get; set; }
        public object eventHubName { get; set; }
        public Metric[] metrics { get; set; }
        public Log[] logs { get; set; }
        public object logAnalyticsDestinationType { get; set; }
    }

    public class Metric
    {
        public string category { get; set; }
        public bool enabled { get; set; }
        public Retentionpolicy retentionPolicy { get; set; }
    }

    public class Retentionpolicy
    {
        public bool enabled { get; set; }
        public int days { get; set; }
    }

    public class Log
    {
        public string category { get; set; }
        public bool enabled { get; set; }
        public Retentionpolicy retentionPolicy { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string message { get; set; }
    }

}
