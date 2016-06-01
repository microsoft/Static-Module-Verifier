using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Globalization;

namespace SMVActionsTable
{
    /// <summary>
    /// Denotes the status of the action.
    /// </summary>
    public enum ActionStatus { NotStarted, InProgress, Complete, Error };

    /// <summary>
    /// Represents a row in the actions table. The scheduler instance GUID is used as the partition key and the rule GUID is used as the row key.
    /// </summary>
    public class ActionsTableEntry : TableEntity
    {
        /// <summary>
        /// Name of the action.
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Status of the action.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// The serialized action.
        /// </summary>
        public byte[] SerializedAction { get; set; }

        /// <summary>
        /// Which version of SMV to use to run this action.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Path to the plugin used to run this action, relative to %smv%.
        /// </summary>
        public string PluginPath { get; set; }

        /// <summary>
        /// Hash of the module required to run this action.
        /// </summary>
        public string ModuleHash { get; set; }

        public ActionsTableEntry()
        {
            ActionName = "Invalid";
            RowKey = "Invalid";
            PartitionKey = "Invalid";
            SerializedAction = null;
            Version = "Invalid";
            PluginPath = "Invalid";
            ModuleHash = "Invalid";
            Status = (int)ActionStatus.Error;
        }

        public ActionsTableEntry(string actionName, string actionGuid, string schedulerInstanceGuid, byte[] serializedAction,
            string version, string plugin, string moduleHash)
        {
            ActionName = actionName;
            RowKey = actionGuid;
            PartitionKey = schedulerInstanceGuid;
            SerializedAction = serializedAction;
            Version = version;
            PluginPath = plugin;
            ModuleHash = moduleHash;
            Status = (int)ActionStatus.NotStarted;
        }

        public override string ToString()
        {
            return "Scheduler Instance GUID: " + PartitionKey + Environment.NewLine
                + "Action Name: " + ActionName + Environment.NewLine
                + "Action Guid: " + RowKey + Environment.NewLine
                + "Status: " + Status.ToString(CultureInfo.CurrentCulture);
        }
    }
}
