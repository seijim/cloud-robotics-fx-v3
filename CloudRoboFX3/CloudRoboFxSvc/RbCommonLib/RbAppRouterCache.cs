using System;

namespace CloudRoboticsCommon
{
    [Serializable]
    public class RbAppRouterCache
    {
        public string AppId { set; get; }
        public string AppProcessingId { set; get; }
        public string HttpUri { set; get; }
        public string Status { set; get; }
        public string Description { set; get; }
        public DateTime Registered_DateTime { set; get; }
        public DateTime CacheExpiredDatetime { set; get; }
    }
}