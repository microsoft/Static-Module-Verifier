using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    /// <summary>
    /// Each instance of this class represents an entry in the ActionsQueue for the local and master action schedulers.
    /// </summary>
    public class ActionsQueueEntry
    {
        /// <summary>
        /// The action to be executed.
        /// </summary>
        public SMVAction Action { get; set; }

        /// <summary>
        /// This delegate is called after the action is complete.
        /// </summary>
        public SMVActionCompleteCallBack Callback { get; set; }

        /// <summary>
        /// A list of results, one for this action and one for each action that was a child of this action.
        /// </summary>
        public List<SMVActionResult> Results { get; set; }

        /// <summary>
        /// Context object passed to the callback.
        /// </summary>
        public object Context { get; set; }

        public ActionsQueueEntry(SMVAction action, SMVActionCompleteCallBack callback, object context)
        {
            this.Action = action;
            this.Callback = callback;
            this.Context = context;
            this.Results = new List<SMVActionResult>();
        }
    }
}
