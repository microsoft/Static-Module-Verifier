using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace SmvLibrary
{
    public class MasterSMVActionScheduler : ISMVActionScheduler
    {
        private ConcurrentQueue<ActionsQueueEntry> actionsQueue = new ConcurrentQueue<ActionsQueueEntry>();
        private IDictionary<string, ISMVActionScheduler> schedulers = new Dictionary<string, ISMVActionScheduler>();

        private bool disposed = false;
        private bool done = false;

        private ConcurrentDictionary<string, int> counters = new ConcurrentDictionary<string, int>();
        private ConcurrentBag<int> times = new ConcurrentBag<int>();

        private delegate void ExecuteDelegate();

        /// <summary>
        /// Start running a thread that handles dequeueing actions from the actionsQueue and sending them to the appropriate scheduler. 
        /// </summary>
        public MasterSMVActionScheduler()
        {
            try
            {
                new ExecuteDelegate(this.Execute).BeginInvoke(null, null);
            }
            catch(Exception e)
            {
                Log.LogFatalError("Exception in delegate!" + e.ToString());
            }
        }

        /// <summary>
        /// Add a new scheduler to the list of schedulers used by the master scheduler.
        /// </summary>
        /// <param name="type">The type of scheduler you're adding. E.g., "local", "cloud"</param>
        /// <param name="scheduler">The scheduler to be added.</param>
        public void AddScheduler(string type, ISMVActionScheduler scheduler)
        {
            schedulers[type] = scheduler;
        }
        public int Count()
        {
            return actionsQueue.Count;
        }

        public void AddAction(SMVAction action, SMVActionCompleteCallBack callback, object context)
        {
            //Log.LogInfo("Queuing action: " + action.GetFullName() + " on " + action.executeOn);
            var entry = new ActionsQueueEntry(action, callback, context);
            actionsQueue.Enqueue(entry);
            counters.AddOrUpdate("queued", 1, (k, v) => v + 1);
        }

        /// <summary>
        /// This function runs in its own thread, dequeueing actions from the actions queue and sending them to the
        /// appropriate scheduler.
        /// </summary>
        private void Execute()
        {
            while (!done)
            {
                ActionsQueueEntry entry;
                while (!done && actionsQueue.TryDequeue(out entry))
                {
                    SMVAction action = entry.Action;
                    string schedulerType = action.executeOn;
                    if (!schedulers.ContainsKey(schedulerType))
                    {
                        Log.LogFatalError("Could not find scheduler of type: " + schedulerType +
                            " while executing action " + action.name);
                    }
                    else
                    {

                        ISMVActionScheduler scheduler = schedulers[schedulerType];
                        lock (Utility.lockObject)
                        {
                            Utility.result.Add(action.GetFullName(), "Skipped");
                        }
                        scheduler.AddAction(action, new SMVActionCompleteCallBack(ActionComplete), entry);
                    }
                }
                //System.Threading.Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// This callback function is called once an action and all its children have executed.
        /// </summary>
        /// <param name="results">A list of results, one for each action (the action added to the queue and its children).</param>
        /// <param name="context">A context object.</param>
        private void ActionComplete(SMVAction a, IEnumerable<SMVActionResult> results, object context)
        {
            var entry = context as ActionsQueueEntry;
            SMVAction action = entry.Action;
            SMVActionCompleteCallBack callback = entry.Callback;
            entry.Results.AddRange(results);

            counters.AddOrUpdate("completed", 1, (k, v) => v + 1);
            times.Add(action.result.time);

            File.WriteAllText(Path.Combine(Utility.GetActionDirectory(action), "smvstats.txt"),
                string.Format("Time: {0}", action.result.time));

            string resultString = string.Format("{0} of {1} jobs remaining. Avg(s): {2}. Std.Dev(s): {3}",
                counters["queued"] - counters["completed"], counters["queued"],
                times.Average().ToString("F2"), Math.Sqrt(times.Average(v => Math.Pow(v - times.Average(), 2))).ToString("F2"));
            Console.Write("\r" + resultString);

            if (counters["queued"] == counters["completed"])
                Console.WriteLine();

            // Add result to our global result set.

            string result = "Failed";
            if(action.result != null && action.result.isSuccessful)
            {
                result = "Success";
            }
            lock (Utility.lockObject)
            {
                Utility.result[action.GetFullName()] = result;
            }

            // If there was an error, simply call the callback function with whatever results we have, the callback is
            // expected to handle the errors by looking at the list of results.
            if (action.result == null || action.result.breakExecution)
            {
                entry.Callback(action, entry.Results, entry.Context);
            }
            // Otherwise, add the next action to the queue, if any.
            else 
            {
                SMVAction nextAction = Utility.GetNextAction(action);

                if (nextAction != null)
                {
                    nextAction.analysisProperty = action.analysisProperty;

                    DebugUtility.DumpVariables(entry.Action.variables, "entry.action");
                    DebugUtility.DumpVariables(Utility.smvVars, "smvvars");

                    nextAction.variables = Utility.smvVars.Union(entry.Action.variables).ToDictionary(g => g.Key, g=> g.Value);
                    this.AddAction(nextAction, entry.Callback, entry.Context);
                }
                else
                {
                    entry.Callback(action, entry.Results, entry.Context);
                }
            }
        }

        private Dictionary<string, string> m(Dictionary<string, string> d1, Dictionary<string, string> d2)
        {
            Dictionary<string, string> r = new Dictionary<string, string>();



            return r;

        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // Clean up managed resources.
                    done = true;
                }
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
