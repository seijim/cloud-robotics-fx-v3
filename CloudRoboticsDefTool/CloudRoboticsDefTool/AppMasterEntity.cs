using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudRoboticsDefTool
{
    public class AppMasterEntity : IComparable<AppMasterEntity>
    {
        public string AppId { set; get; }
        public string QueueStorageAccount { set; get; }
        public string QueueStorageKey { set; get; }
        public string Status { set; get; }
        public string Description { set; get; }
        public DateTime Registered_DateTime { set; get; }

        public int CompareTo(AppMasterEntity other)
        {
            return string.Compare(this.AppId, other.AppId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
