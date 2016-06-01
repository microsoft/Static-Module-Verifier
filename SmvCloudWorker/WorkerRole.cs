using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Queue;
using SmvLibrary;
using SMVActionsTable;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.ServiceBus.Messaging;
using Microsoft.ServiceBus;

namespace SmvCloudWorker2
{
    public class WorkerRole : RoleEntryPoint
    {
        private TopicClient outputTopic;                    /// Output topic to inform the clients of results.
        private SMVCloudConfig cloudConfig;                 /// Object containing connection strings.   
        private bool acceptingMessages = true;              /// Set to false when the worker stops accepting new messages.
        private BlobRequestOptions options;                 /// Options for blob storage.
        private CloudQueue inputQueue;                      /// Queue with the actions to be executed.
        private CloudBlobContainer jobsContainer;           /// Jobs are downloaded from here.
        private CloudBlobContainer resultsContainer;        /// Results are uploaded here.
        private CloudBlobContainer versionsContainer;       /// SMV versions are downloaded from here.
        private ActionsTableDataSource tableDataSource;     /// Used to interact with the actions table.
        private string workingDirectory;                    /// The working directory for SMV.
        private string resultsDirectory;                    /// A temporary location for the result zip files.
        private string smvVersionsDirectory;                /// The directory containing the SMV versions.
        private string smvDirectory;                        /// The directory containing the current SMV version.       
        private CloudQueueMessage currentMessage;           /// The message being currently processed.

        /// Maps names of SMV versions to their locations on the file system.
        private IDictionary<string, Tuple<string, DateTime>> smvVersions = new Dictionary<string, Tuple<string, DateTime>>();

        private void SendMessageToTopic(BrokeredMessage msg)
        {
            bool done = false;
            for (int retryCount = 1; !done; retryCount++)
            {
                done = true;
                try
                {
                    outputTopic = TopicClient.CreateFromConnectionString(cloudConfig.ServiceBusConnectionString.value, CloudConstants.ResultsTopicName);
                    outputTopic.Send(msg);
                    outputTopic.Close();
                }
                catch (Exception)
                {
                    if (retryCount == CloudConstants.MaxRetries)
                    {
                        throw;
                    }
                    done = false;
                    System.Threading.Thread.Sleep(CloudConstants.RetryBackoffInterval);
                }
            }
        }

        public override void Run()
        {
            while (acceptingMessages)
            {
                System.Threading.Thread.Sleep(30 * 1000);

                try
                {
                    currentMessage = inputQueue.GetMessage(TimeSpan.FromHours(1));
                    if (currentMessage != null)
                    {
                        // Parse the message.
                        string[] msgParts = currentMessage.AsString.Split(',');
                        string schedulerInstanceGuid = msgParts[0];
                        string actionGuid = msgParts[1];

                        // Get the table entry.
                        ActionsTableEntry tableEntry = tableDataSource.GetEntry(schedulerInstanceGuid, actionGuid);
                        tableDataSource.UpdateStatus(schedulerInstanceGuid, actionGuid, ActionStatus.InProgress);
                        CloudBlockBlob jobBlob = jobsContainer.GetBlockBlobReference(actionGuid + ".zip");
                        using (var outputMsg = new BrokeredMessage())
                        {
                            outputMsg.Properties["SchedulerInstanceGuid"] = schedulerInstanceGuid;
                            outputMsg.Properties["ActionGuid"] = actionGuid;
                            outputMsg.Properties["DequeueCount"] = currentMessage.DequeueCount;
                            outputMsg.Properties["WaitTime"] = DateTime.Now - currentMessage.InsertionTime;

                            // Check if we have tried to process this message too many times.
                            // If so, delete it and report an error back to the client.
                            if (currentMessage.DequeueCount >= CloudConstants.MaxDequeueCount)
                            {
                                tableDataSource.UpdateStatus(schedulerInstanceGuid, actionGuid, ActionStatus.Error);
                                SendMessageToTopic(outputMsg);
                                inputQueue.DeleteMessage(currentMessage);
                                jobBlob.Delete();
                                continue;
                            }

                            // Switch the version of SMV if required.
                            if (!SetSmvVersion(tableEntry.Version))
                            {
                                Trace.TraceError("Could not set SMV version.");
                                tableDataSource.UpdateStatus(schedulerInstanceGuid, actionGuid, ActionStatus.Error);
                                SendMessageToTopic(outputMsg);
                                inputQueue.DeleteMessage(currentMessage);
                                jobBlob.Delete();
                                continue;
                            }

                            // Load the plugin.
                            if (!string.IsNullOrEmpty(tableEntry.PluginPath))
                            {
                                string pluginPath = Environment.ExpandEnvironmentVariables(tableEntry.PluginPath);
                                var assembly = System.Reflection.Assembly.LoadFrom(pluginPath);
                                string fullName = assembly.ExportedTypes.First().FullName;
                                Utility.plugin = (ISMVPlugin)assembly.CreateInstance(fullName);
                                if (Utility.plugin == null)
                                {
                                    throw new Exception("Could not load plugin: " + tableEntry.PluginPath);
                                }
                                Utility.plugin.Initialize();
                            }

                            // Get the module object, if any.
                            if (!string.IsNullOrEmpty(tableEntry.ModuleHash))
                            {
                                SmvAccessor.ISmvAccessor accessor = Utility.GetSmvSQLAccessor();
                                Utility.smvModule = accessor.GetModuleByHash(tableEntry.ModuleHash);
                                if(Utility.smvModule == null)
                                {
                                    throw new Exception("Could not load module with hash: " + tableEntry.ModuleHash);
                                }
                            }

                            // Download the job and extract it to the working directory.
                            Utility.ClearDirectory(workingDirectory);
                            string jobZipPath = Path.Combine(workingDirectory, "job.zip");
                            jobBlob.DownloadToFile(jobZipPath, FileMode.CreateNew);
                            ZipFile.ExtractToDirectory(jobZipPath, workingDirectory);
                            File.Delete(jobZipPath);

                            // Deserialize the action.
                            SMVAction action = (SMVAction)Utility.ByteArrayToObject(tableEntry.SerializedAction);

                            // Get ready to execute the action.
                            // We substitute the value of assemblyDir and workingDir with the values on this machine.
                            string oldWorkingDir = action.variables["workingDir"].ToLower();
                            string oldAssemblyDir = action.variables["assemblyDir"].ToLower();
                            string newAssemblyDir = Path.Combine(smvDirectory, "bin").ToLower();
                            workingDirectory = workingDirectory.ToLower();
                            var keys = new List<string>(action.variables.Keys);
                            foreach (var key in keys)
                            {
                                if (!string.IsNullOrEmpty(action.variables[key]))
                                {
                                    if (action.variables[key].ToLower().StartsWith(oldAssemblyDir))
                                    {
                                        action.variables[key] = action.variables[key].ToLower().Replace(oldAssemblyDir, newAssemblyDir);
                                    }
                                    else if (action.variables[key].ToLower().StartsWith(oldWorkingDir))
                                    {
                                        action.variables[key] = action.variables[key].ToLower().Replace(oldWorkingDir, workingDirectory);
                                    }
                                }
                            }
                            // NOTE: We set the Path attribute in the action to null because the action is always processed in the working directory.
                            var path = action.Path;
                            action.Path = null;

                            Utility.SetSmvVar("workingDir", workingDirectory);

                            // Execute the action.
                            SMVActionResult result = Utility.ExecuteAction(action);

                            // Change the paths back to their old values.
                            foreach (var key in keys)
                            {
                                if (!string.IsNullOrEmpty(action.variables[key]))
                                {
                                    if (action.variables[key].ToLower().StartsWith(newAssemblyDir))
                                    {
                                        action.variables[key] = action.variables[key].ToLower().Replace(newAssemblyDir, oldAssemblyDir);
                                    }
                                    else if (action.variables[key].ToLower().StartsWith(workingDirectory))
                                    {
                                        action.variables[key] = action.variables[key].ToLower().Replace(workingDirectory, oldWorkingDir);
                                    }
                                }
                            }

                            // Now set the path attribute again because the client needs it.
                            action.Path = path;

                            // Zip up the working directory and upload it as the result.
                            string resultsZipPath = Path.Combine(resultsDirectory, actionGuid + ".zip");
                            ZipFile.CreateFromDirectory(workingDirectory, resultsZipPath);
                            CloudBlockBlob resultsBlob = resultsContainer.GetBlockBlobReference(actionGuid + ".zip");
                            resultsBlob.UploadFromFile(resultsZipPath, FileMode.Open);
                            File.Delete(resultsZipPath);

                            // Job done!
                            tableDataSource.UpdateAction(schedulerInstanceGuid, actionGuid, Utility.ObjectToByteArray(action));
                            tableDataSource.UpdateStatus(schedulerInstanceGuid, actionGuid, ActionStatus.Complete);
                            SendMessageToTopic(outputMsg);
                            if (currentMessage != null)
                            {
                                inputQueue.DeleteMessage(currentMessage);
                                currentMessage = null;
                            }
                            jobBlob.DeleteIfExists();
                            Utility.ClearDirectory(workingDirectory);
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError("Exception while processing queue item:" + e.ToString());
                    if (currentMessage != null)
                    {
                        inputQueue.UpdateMessage(currentMessage, TimeSpan.FromSeconds(5), MessageUpdateFields.Visibility);
                    }
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        private bool SetSmvVersion(string version)
        {
            // The version string cannot be empty.
            if (String.IsNullOrEmpty(version))
            {
                return false;
            }

            // First, check if the SMV version already exists in our local machine.
            string smvName = "smv-" + version;
            string smvFilename = smvName + ".zip";
            CloudBlockBlob blob = versionsContainer.GetBlockBlobReference(smvFilename);

            blob.FetchAttributes();
            if (smvVersions.ContainsKey(version) && smvVersions[version].Item2 >= blob.Properties.LastModified.Value.UtcDateTime)
            {
                smvVersionsDirectory = smvVersions[version].Item1;
                Environment.SetEnvironmentVariable("smv", smvVersionsDirectory);
                return true;
            }

            // Download the SMV version if it's not available locally.
            string zipPath = Path.Combine(smvVersionsDirectory, smvFilename);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            blob.DownloadToFile(zipPath, FileMode.CreateNew);

            // Delete the version's directory to clear it of any old files.
            string dir = Path.Combine(smvVersionsDirectory, smvName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            // Now we'll unpack our version of SMV.
            try
            {
                Directory.CreateDirectory(dir);
                ZipFile.ExtractToDirectory(zipPath, dir);
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                Trace.TraceError("Error unzipping smv.zip: Exception: {0}", e.ToString());
                return false;
            }

            // We've setup a version of SMV successfully. Set smvPath and add this version to the list of avaiable SMV versions.
            smvDirectory = dir;
            Environment.SetEnvironmentVariable("smv", smvDirectory);
            smvVersions[version] = new Tuple<string, DateTime>(smvDirectory, blob.Properties.LastModified.Value.UtcDateTime);

            return true;
        }

        public override bool OnStart()
        {
            try
            {
                // Set the maximum number of concurrent connections .
                System.Net.ServicePointManager.DefaultConnectionLimit = 12;

                // Set options for blob storage.
                options = new BlobRequestOptions();
                options.ServerTimeout = TimeSpan.FromMinutes(10);
                options.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval), CloudConstants.MaxRetries);

                // Get the connection strings.
                string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string cloudConfigPath = Path.Combine(assemblyDir, CloudConstants.CloudConfigXmlFileName);
                string cloudConfigContents = Utility.ReadFile(cloudConfigPath);
                string schemaPath = Path.Combine(assemblyDir, CloudConstants.CloudConfigXsdFileName);
                using (var c = new StringReader(cloudConfigContents))
                {
                    if (!Utility.ValidateXmlFile(schemaPath, c))
                    {
                        Trace.TraceError("Could not load cloud config from file: " + cloudConfigPath);
                        return false;
                    }
                }
                var serializer = new XmlSerializer(typeof(SMVCloudConfig));
                using (var reader = new StringReader(cloudConfigContents))
                {
                    cloudConfig = (SMVCloudConfig)serializer.Deserialize(reader);
                }

                bool done = false;
                while (!done)
                {
                    try
                    {
                        var storageAccount = CloudStorageAccount.Parse(cloudConfig.StorageConnectionString.value);

                        // Setup queue storage.
                        CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                        queueClient.DefaultRequestOptions = new QueueRequestOptions();
                        queueClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval),
                            CloudConstants.MaxRetries);
                        inputQueue = queueClient.GetQueueReference(CloudConstants.InputQueueName);
                        inputQueue.CreateIfNotExists();

                        // Setup blob storage.
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        blobClient.DefaultRequestOptions = new BlobRequestOptions();
                        blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval),
                            CloudConstants.MaxRetries);
                        jobsContainer = blobClient.GetContainerReference(CloudConstants.InputBlobContainerName);
                        jobsContainer.CreateIfNotExists();
                        resultsContainer = blobClient.GetContainerReference(CloudConstants.OutputBlobContainerName);
                        resultsContainer.CreateIfNotExists();
                        versionsContainer = blobClient.GetContainerReference(CloudConstants.VersionsContainerName);
                        versionsContainer.CreateIfNotExists();

                        // Setup table storage.
                        var tableStorage = storageAccount.CreateCloudTableClient();
                        tableStorage.DefaultRequestOptions = new TableRequestOptions();
                        tableStorage.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(CloudConstants.RetryBackoffInterval), CloudConstants.MaxRetries);
                        CloudTable table = tableStorage.GetTableReference(CloudConstants.TableName);
                        tableDataSource = new ActionsTableDataSource(table);

                        // Setup the service bus topic.
                        var namespaceManager = NamespaceManager.CreateFromConnectionString(cloudConfig.ServiceBusConnectionString.value);
                        if (!namespaceManager.TopicExists(CloudConstants.ResultsTopicName))
                        {
                            namespaceManager.CreateTopic(CloudConstants.ResultsTopicName);
                        }

                        done = true;
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failure trying to connect to Azure storage: " + e.ToString());
                        System.Threading.Thread.Sleep(5000);
                    }
                }

                // Get paths to important directories in the file system. Remove the trailing slash from the paths.
                LocalResource localResource = RoleEnvironment.GetLocalResource(CloudConstants.SmvWorkingDirectoryResourceName);
                workingDirectory = localResource.RootPath.Remove(localResource.RootPath.Length - 1);

                localResource = RoleEnvironment.GetLocalResource(CloudConstants.SmvResultsDirectoryResourceName);
                resultsDirectory = localResource.RootPath.Remove(localResource.RootPath.Length - 1);

                localResource = RoleEnvironment.GetLocalResource(CloudConstants.SmvDirectoryResourceName);
                smvVersionsDirectory = localResource.RootPath.Remove(localResource.RootPath.Length - 1);
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception while running OnStart(): " + e.ToString());
                return false;
            }

            Utility.result = null;

            return base.OnStart();
        }

        public override void OnStop()
        {
            try
            {
                // Stop accepting more messages.
                acceptingMessages = false;

                // Make the message available to another worker.
                if (currentMessage != null)
                {
                    inputQueue.UpdateMessage(currentMessage, TimeSpan.FromSeconds(5), MessageUpdateFields.Visibility);
                    currentMessage = null;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("An exception occurred while running OnStop(): " + e.ToString());
            }
        }
    }
}