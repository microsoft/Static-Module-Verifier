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
                destinationPath = Path.Combine(destinationPath, "Bugs");
                string bugPath = Path.Combine(modulePath.ToLower(), "Bugs");
                Utility.getFolderFromAzure(bugPath, destinationPath, sessionId, azCopyPath, connectionString, connectionKey);
            }
            catch (Exception e)
            {
                WriteObject("Exception " + e);
            }
        }
    }
}
