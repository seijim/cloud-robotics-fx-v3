using System;

namespace CloudRoboticsCommon
{
    [Serializable]
    public class RbAppMasterCache
    {
        public string AppId { set; get; } 
        public string QueueStorageAccount { set; get; }
        public string QueueStorageKey { set; get; }
        public string Status { set; get; }
        public string Description { set; get; }
        public DateTime Registered_DateTime { set; get; }
        public DateTime CacheExpiredDatetime { set; get; }
    }
}