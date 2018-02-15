using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System.IO;
using System.Diagnostics;

namespace SmvCmdlets
{
    static class Utility
    {
        public static void getFolderFromAzure(string relativeFolderPath, string destinationPath, string sessionId, string AzCopyPath, string connectionString, string connectionKey)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("smvautomation");
            CloudFileDirectory direc = share.GetRootDirectoryReference();
            string cloudPath = Path.Combine(sessionId, "Logs", relativeFolderPath);
            Console.WriteLine(cloudPath);
            CloudFileDirectory dir = direc.GetDirectoryReference(cloudPath);
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
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
                Console.WriteLine("Could not find folder in the location!");
            }
        }
    }
}
