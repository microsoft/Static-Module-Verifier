using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    public class CloudMessage
    {
        public string schedulerInstanceGuid;
        public string actionGuid;
        public int maxDequeueCount;
        public Boolean useDb;
        public string taskId;
    }
}
