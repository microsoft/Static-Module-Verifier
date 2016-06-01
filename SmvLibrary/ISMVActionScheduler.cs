using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace SmvLibrary
{
    /// <summary>
    /// Delegate used by the scheduler to indicate that an action and all its children have completed.
    /// </summary>
    /// <param name="results">The results of all the actions that have been executed as a result of this action.</param>
    /// <param name="context">Context object passed to the callback.</param>
    public delegate void SMVActionCompleteCallBack(IEnumerable<SMVActionResult> results, object context);

    /// <summary>
    /// Defines an interface for classes that want to support scheduling SMVActions.
    /// </summary>
    public interface ISMVActionScheduler : IDisposable
    {
        /// <summary>
        /// Adds an action to be scheduled. Once the action has completed, <paramref name="callback"/> will be called with
        /// <paramref name="context"/> as the argument.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="callback">Delegate that will be called once the action has been performed.</param>
        /// <param name="context">Object passed to the delegate when the action has been performed.</param>
        void AddAction(SMVAction action, SMVActionCompleteCallBack callback, object context);
    }
}
