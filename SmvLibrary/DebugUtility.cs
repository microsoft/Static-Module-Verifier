using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    class DebugUtility
    {
        public static void DumpVariables(IDictionary<string, string> dict, string message)
        {
            Log.LogDebug(message);
            foreach (KeyValuePair<string, string> kv in dict)
            {
                Log.LogDebug(kv.ToString());
            }
        }
    }
}
