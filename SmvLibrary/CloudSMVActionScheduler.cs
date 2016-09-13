using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using SMVActionsTable;

namespace SmvLibrary
{
    public class CloudSMVActionScheduler : ISMVActionScheduler
    {
        private bool disposed = false;                  /// Used for implementing IDisposable.
        private string schedulerInstanceGuid;           /// A unique identifier for this instance of the CloudSMVActionScheduler object.
        private string storageConnectionString;         /// Connection string to connect to Azure storage.
        private string serviceBusConnectionString;      /// Connection string to connect to Azure Service Bus.
        private CloudBlobContainer inputContainer;      /// Container where actions are uploaded to.
        private CloudBlobContainer outputContainer;     /// Container where results are downloaded from.
        private ActionsTableDataSource tableDataSource; /// Data source object used to query the actions table.
        private CloudQueue actionsQueue;                /// Cloud queue where each message is an action to be processed by a worker.
        private NamespaceManager namespaceManager;      /// Namespace manager used for working with service bus entities.
        private SubscriptionClient subscriptionClient;  /// Subscription client used to communicate with the service bus.

        private class CloudActionCompleteContext
        {
            public SMVAction action;
            public SMVActionCompleteCallBack callback;
            public object context;

            public CloudActionCompleteContext(SMVAction _action, SMVActionCompleteCallBack _callback, object _context)
            {
                action = _action;
                callback = _callback;
                context = _context;
            }
        }

        /// <summary>
        /// This variable maps action GUIDs to CloudActionCompleteContext objects so we can get some information about the
        /// action that completed when ActionComplete() is called.
        /// </summary>
        private Dictionary<string, CloudActionCompleteContext> contextDictionary = new Dictionary<string, CloudActionCompleteContext>();

        public CloudSMVActionScheduler(SMVCloudConfig config)
        {
            // Set the instance GUID.
            schedulerInstanceGuid = Guid.NewGuid().ToString();
            Log.LogInfo("Scheduler Instance GUID: " + schedulerInstanceGuid);

            // Check if the connection strings are set properly.
            storageConnectionString = config.StorageConnectionString.value;
            serviceBusConnectionString = config.ServiceBusConnectionString.value;
            if (string.IsNullOrEmpty(storageConnectionString))
            {
                throw new Exception("Connection string \"Microsoft.WindowsAzure.Storage.ConnectionString\" is not set. Please contact rahulku@microsoft.com for a " +
                    "valid connection string.");
            }
            if (string.IsNullOrEmpty(serviceBusConnectionString))
            {
                throw new Exception("Connection string \"Microsoft.ServiceBus.ConnectionString\" is not set. Please contact rahulku@microsoft.com for a " +
                    "valid connection string.");
            }

            int retriesLeft = CloudConstants.MaxRetries;
            while (true)
            {
                try
                {
                    // Connect to cloud storage.
                    var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                    var queueStorage = storageAccount.CreateCloudQueueClient();
                    var blobStorage = storageAccount.CreateCloudBlobClient();
                    var tableStorage = storageAccount.CreateCloudTableClient();

                    // Set up blob storage.
                    blobStorage.DefaultRequestOptions = new BlobRequestOptions();
                    blobStorage.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval), CloudConstants.MaxRetries);
                    inputContainer = blobStorage.GetContainerReference(CloudConstants.InputBlobContainerName);
                    inputContainer.CreateIfNotExists();
                    outputContainer = blobStorage.GetContainerReference(CloudConstants.OutputBlobContainerName);
                    outputContainer.CreateIfNotExists();

                    // Set up our queue.
                    queueStorage.DefaultRequestOptions = new QueueRequestOptions();
                    queueStorage.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval), CloudConstants.MaxRetries);
                    actionsQueue = queueStorage.GetQueueReference(CloudConstants.InputQueueName);
                    actionsQueue.CreateIfNotExists();

                    // Set up table storage.
                    tableStorage.DefaultRequestOptions = new TableRequestOptions();
                    tableStorage.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval), CloudConstants.MaxRetries);
                    CloudTable table = tableStorage.GetTableReference(CloudConstants.TableName);
                    tableDataSource = new ActionsTableDataSource(table);

                    // Set up the service bus subscription.
                    namespaceManager = NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
                    var filter = new SqlFilter(String.Format(CultureInfo.CurrentCulture, "(SchedulerInstanceGuid = '{0}')", schedulerInstanceGuid));
                    if (!namespaceManager.TopicExists(CloudConstants.ResultsTopicName))
                    {
                        namespaceManager.CreateTopic(CloudConstants.ResultsTopicName);
                    }
                    var subDesc = new SubscriptionDescription(CloudConstants.ResultsTopicName, schedulerInstanceGuid);
                    subDesc.AutoDeleteOnIdle = TimeSpan.FromDays(7.0);
                    if (!namespaceManager.SubscriptionExists(CloudConstants.ResultsTopicName, schedulerInstanceGuid))
                    {
                        namespaceManager.CreateSubscription(subDesc, filter);
                    }
                    subscriptionClient = SubscriptionClient.CreateFromConnectionString(serviceBusConnectionString,
                        CloudConstants.ResultsTopicName, schedulerInstanceGuid);
                    subscriptionClient.RetryPolicy = new RetryExponential(
                        TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval),
                        TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval * CloudConstants.MaxRetries), 
                        CloudConstants.MaxRetries);
                    subscriptionClient.OnMessage((msg) =>
                    {
                        try
                        {
                            ActionComplete(msg);
                        }
                        catch (Exception)
                        {
                            msg.Abandon();
                        }
                    });
                    // If we're here, we've successfully initialized everything and can break out of the retry loop.
                    break;
                }
                catch (Exception e)
                {
                    // We can fix the three exceptions below by using retry logic. But there's no point retrying for other exceptions.
                    if ((e is TimeoutException || e is ServerBusyException || e is MessagingCommunicationException) && retriesLeft > 0)
                    {
                        retriesLeft--;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public void AddAction(SMVAction action, SMVActionCompleteCallBack callback, object context)
        {
            string actionGuid = Guid.NewGuid().ToString();
            
            // Upload action directory to blob storage.

            string actionPath = Utility.GetActionDirectory(action);
            string zipPath = Path.GetTempFileName();
            File.Delete(zipPath);
            ZipFile.CreateFromDirectory(actionPath, zipPath);

            CloudBlockBlob blob = inputContainer.GetBlockBlobReference(actionGuid + ".zip");
            blob.UploadFromFile(zipPath, FileMode.Open);
            File.Delete(zipPath);

            // Add entry to table storage.

            // TODO: Due to constraints on sizes of properties in Azure table entities, serializedAction cannot be larger
            // than 64kB. Fix this if this becomes an issue.
            byte[] serializedAction = Utility.ObjectToByteArray(action);
            string moduleHash = string.Empty;
            ActionsTableEntry entry = new ActionsTableEntry(action.name, actionGuid, schedulerInstanceGuid, serializedAction,
                Utility.version, null, moduleHash);
            tableDataSource.AddEntry(entry);

            // Add message to queue.        
            Log.LogInfo("Executing: " + action.GetFullName() + " [cloud id:" + actionGuid + "]");
            string messageString = schedulerInstanceGuid + "," + actionGuid;
            var message = new CloudQueueMessage(messageString);
            actionsQueue.AddMessage(message);

            // Start listening for a message from the service bus.

            contextDictionary[actionGuid] = new CloudActionCompleteContext(action, callback, context);
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = false;
            options.AutoRenewTimeout = TimeSpan.FromHours(CloudConstants.BeginReceiveTimeoutInHours);
            
            //subscriptionClient.BeginReceive(TimeSpan.FromHours(CloudConstants.BeginReceiveTimeoutInHours), 
              //  new AsyncCallback(ActionComplete), null);
        }

        /// <summary>
        /// Callback that is called when SubscriptionClient.BeginReceive() receives a message.
        /// </summary>
        /// <param name="ar"></param>
        private void ActionComplete(BrokeredMessage message)
        {

            var actionGuid = (string)message.Properties["ActionGuid"];
            var waitTime = (TimeSpan)message.Properties["WaitTime"];    // The amount of time the rule had to wait before it started being processed.
            var dequeueCount = (int)message.Properties["DequeueCount"]; // The number of times the message we sent was dequeued by the workers.

            message.Complete();

            CloudActionCompleteContext context = contextDictionary[actionGuid];
            ActionsTableEntry entry = tableDataSource.GetEntry(schedulerInstanceGuid, actionGuid);
            var action = (SMVAction)Utility.ByteArrayToObject(entry.SerializedAction);

            Log.LogInfo("ActionComplete for " + action.GetFullName() + " [cloud id " + actionGuid + "]");

            // Populate the original action object so that the master scheduler gets the changes to the action object.
            context.action.analysisProperty = action.analysisProperty;
            context.action.result = action.result;
            context.action.variables = action.variables;

            var results = new SMVActionResult[] { context.action.result };
            if(entry.Status != (int)ActionStatus.Complete)
            {
                Log.LogError(string.Format("Failed to complete action: {0} ({1})", actionGuid, context.action.name));
                context.callback(context.action, new SMVActionResult[] { context.action.result }, context.context);
            }

            // Download and extract the results.
            CloudBlockBlob resultsBlob = outputContainer.GetBlockBlobReference(actionGuid + ".zip");
            string zipPath = Path.GetTempFileName();
            File.Delete(zipPath);
            resultsBlob.DownloadToFile(zipPath, FileMode.CreateNew);
            resultsBlob.Delete();
            string actionDirectory = Utility.GetActionDirectory(context.action);
            Utility.ClearDirectory(actionDirectory);
            ZipFile.ExtractToDirectory(zipPath, actionDirectory);
            File.Delete(zipPath);

            // Write to the cloudstats.txt file.
            using (StreamWriter writer = File.AppendText(Path.Combine(actionDirectory, "cloudstats.txt")))
            {
                writer.WriteLine("Wait Time: " + waitTime.ToString());
                writer.WriteLine("Dequeue Count: " + dequeueCount);
                writer.WriteLine("Output" + Environment.NewLine + results.First().output);
            }

            context.callback(context.action, results, context.context);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources.
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
