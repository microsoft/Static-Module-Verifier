using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SmvLibrary
{
    public class LocalSMVActionScheduler : ISMVActionScheduler
    {
        private ConcurrentQueue<ActionsQueueEntry> actionsQueue = new ConcurrentQueue<ActionsQueueEntry>();
        private List<Thread> executeThreads = new List<Thread>();

        private bool disposed = false;

        /// <summary>
        /// Starts <paramref name="numberOfThreads"/> threads, each of which will start dequeueing actions from the actions
        /// queue. The threads stop executing once the object is disposed of.
        /// </summary>
        /// <param name="numberOfThreads">The number of threads to start.</param>
        public LocalSMVActionScheduler(int numberOfThreads)
        {
            for(int i = 0; i < numberOfThreads; i++)
            {
                Thread t = new Thread(new ThreadStart(Execute));
                executeThreads.Add(t);
                t.Start();
            }
        }

        public void AddAction(SMVAction action, SMVActionCompleteCallBack callback, object context)
        {
            Log.LogInfo("Reached Add Action of Local " + action.GetFullName());
            var entry = new ActionsQueueEntry(action, callback, context);
            actionsQueue.Enqueue(entry);
        }

        public int Count()
        {
            return actionsQueue.Count;
        }

        private void Execute()
        {
            Log.LogInfo("Reached Execute of Local");
            try
            {
                while(true)
                {
                    ActionsQueueEntry entry;
                    while(actionsQueue.TryDequeue(out entry))
                    {
                        Log.LogDebug("Executing: " + entry.Action.GetFullName());
                        SMVActionResult result = Utility.ExecuteAction(entry.Action, false, false, null);
                        entry.Results.Add(result);
                        entry.Callback(entry.Action, entry.Results, entry.Context);
                    }
                    Thread.Sleep(1000);
                }
            }
            catch(ThreadAbortException)
            {
                // Do nothing here, we just need this so we can call Thread.Abort() to kill the thread.
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources.
                    foreach(Thread t in executeThreads)
                    {
                        t.Abort();
                    }
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
