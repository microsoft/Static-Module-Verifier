using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Configuration;

namespace SmvCmdlets
{
    [Cmdlet(VerbsCommon.Get, "BugsFolderWithoutSdxRoot")]
    public class GetBugsFolderWithoutSdxRoot : PSCmdlet
    {

        [Parameter(Position = 0, Mandatory = true)]
        public string SessionId
        {
            get { return sessionId; }
            set { sessionId = value; }
        }
        [Parameter(Position = 1, Mandatory = true)]
        public string ModulePath
        {
            get { return modulePath; }
            set { modulePath = value; }
        }

        [Parameter(Position = 2, Mandatory = true)]
        public string AzCopyPath
        {
            get { return azCopyPath; }
            set { azCopyPath = value; }
        }
        private string sessionId;
        public string modulePath;
        public string sdxRoot;
        public string azCopyPath;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            try
            {
                string destinationPath = SessionState.Path.CurrentFileSystemLocation.ToString();
                var connectionString = ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString;
                var connectionKey = ConfigurationManager.ConnectionStrings["StorageKey"].ConnectionString;
                getFolderFromAzure(modulePath, destinationPath, sessionId, azCopyPath, connectionString, connectionKey);
            }
            catch (Exception e)
            {
                WriteObject("Exception " + e);
            }
        }

        public static void getFolderFromAzure(string absolutePath, string destinationPath, string sessionId, string AzCopyPath, string connectionString, string connectionKey)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("smvautomation");
            CloudFileDirectory direc = share.GetRootDirectoryReference();
            absolutePath = absolutePath.ToLower();
            string cloudPath = Path.Combine(sessionId, "Logs", absolutePath, "Bugs");
            Console.WriteLine(cloudPath);
            CloudFileDirectory dir = direc.GetDirectoryReference(cloudPath);
            destinationPath = Path.Combine(destinationPath, "Bugs");
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }
            if (dir.Exists())
            {
                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = false;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                string changeLocation = "cd " + AzCopyPath;
                cmd.StandardInput.WriteLine(changeLocation);

                string command = @".\AzCopy.exe /Source:https://smvtest.file.core.windows.net/smvautomation/" + cloudPath + " /Dest:" + destinationPath + " /Sourcekey:" + connectionKey + " /S /Z:" + destinationPath;

                cmd.StandardInput.WriteLine(command);
                cmd.StandardInput.WriteLine("exit");
                cmd.WaitForExit();
            }
            else
            {
                Console.WriteLine("Could not find bugs folder in the location!");
            }
        }
    }
}
