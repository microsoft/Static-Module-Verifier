using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    /// <summary>
    /// Common constants needed by both SmvCloudWorker and CloudSmvActionScheduler.
    /// </summary>
    public static class CloudConstants
    {
        public static int MaxRetries = 6;                                   /// Maximum number of times to try to connect to blob storage before giving up.
        public const int RetryBackoffInterval = 4;                          /// Time to wait in seconds before retrying a request.
        public const int WorkerWaitTime = 5 * 1000;                         /// Time to wait between successive dequeues from the queue for new work
        public const int BeginReceiveTimeoutInHours = 9;                    /// Timeout for the BeginReceive() call.
        public const string InputBlobContainerName = "smvactions";          /// Container action data is uploaded to.
        public const string OutputBlobContainerName = "smvresults";         /// Container results are downloaded from.
        public const string VersionsContainerName = "smvversions";          /// Container where different versions of SMV are stored.
        public const string InputQueueName = "smvactions";                  /// Name of the actions queue.
        public const string ResultsTopicName = "smvresults";                /// Name of the service bus topic used to deliver result messages to the client.
        public const string TableName = "actionstable";                     /// Name of the table used to store information about the actions.
        public const string CloudConfigXmlFileName = "cloudconfig.xml";     /// Name of the XML file that contains the connection strings.
        public const string CloudConfigXsdFileName = "cloudconfig.xsd";     /// Name of XML schema file for the cloud config files.
        public const int MaxDequeueCount = 5;                               /// The maximum number of times a message can be dequeued for processing.
        public const string SmvWorkingDirectoryResourceName = "SMVWorking"; /// Used to get the path to the SMV working directory.
        public const string SmvResultsDirectoryResourceName = "SMVResults"; /// Used to get the path to the results directory.
        public const string SmvDirectoryResourceName = "SMVExec";           /// Used to get the location to SMV.
    }
}
