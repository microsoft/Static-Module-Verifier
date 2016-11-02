using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace SMVActionsTable
{
    /// <summary>
    /// This class is a wrapper around the actions table that provides some useful operations.
    /// </summary>
    public class ActionsTableDataSource
    {
        private CloudTable actionsTable;                /// Table where each row contains information about the action.
        public ActionsTableDataSource(CloudTable table)
        {
            actionsTable = table;
           // if (actionsTable.Exists()) return;
            //actionsTable.CreateIfNotExists();
        }

        /// <summary>
        /// Get an entry from the table.
        /// </summary>
        /// <param name="partitionKey">The partition key of the entry.</param>
        /// <param name="rowKey">The row key of the entry.</param>
        /// <returns>The entry if it was found in the table, null otherwise.</returns>
        public ActionsTableEntry GetEntry(string partitionKey, string rowKey)
        {
            var retrieveOperation = TableOperation.Retrieve<ActionsTableEntry>(partitionKey, rowKey);
            TableResult result = actionsTable.Execute(retrieveOperation);
            if(result.Result == null)
            {
                return null;
            }
            return (ActionsTableEntry)result.Result;
        }

        /// <summary>
        /// Get all the entries with a certain status. WARNING: Does a query across all partitions, so may be very slow.
        /// </summary>
        /// <param name="status">The status to use in the query.</param>
        /// <returns>The entries in the table which have the given status.</returns>
        public IEnumerable<ActionsTableEntry> GetEntries(ActionStatus status)
        {
            var query = new TableQuery<ActionsTableEntry>().Where(TableQuery.GenerateFilterConditionForInt("Status",
                QueryComparisons.Equal, (int)status));
            var results = actionsTable.ExecuteQuery(query);
            return results;
        }

        /// <summary>
        /// Adds an entry to the table.
        /// </summary>
        /// <param name="entry">The entry to be added.</param>
        public void AddEntry(ActionsTableEntry entry)
        {
            var insertOperation = TableOperation.Insert(entry);
            actionsTable.Execute(insertOperation);
        }

        /// <summary>
        /// Updates the status of an entry in the table.
        /// </summary>
        /// <param name="partitionKey">Partition key of the entry.</param>
        /// <param name="rowKey">Row key of the entry.</param>
        /// <param name="newStatus">The new status of the entry.</param>
        public void UpdateStatus(string partitionKey, string rowKey, ActionStatus newStatus)
        {
            var retrieveOperation = TableOperation.Retrieve<ActionsTableEntry>(partitionKey, rowKey);
            TableResult result = actionsTable.Execute(retrieveOperation);
            if (result.Result == null)
            {
                throw new ArgumentException(string.Format("Could not find an entry in the table for the given partitionKey, rowKey tuple: {0}, {1}",
                    partitionKey, rowKey));
            }
            var entry = (ActionsTableEntry)result.Result;
            entry.Status = (int)newStatus;

            var replaceOperation = TableOperation.Replace(entry);
            actionsTable.Execute(replaceOperation);
        }

        /// <summary>
        /// Updates the SerializedAction field of an entry in the table.
        /// </summary>
        /// <param name="partitionKey">Partition key of the entry.</param>
        /// <param name="rowKey">Row key of the entry.</param>
        /// <param name="serializedAction">The new value for the serialized action field.</param>
        public void UpdateAction(string partitionKey, string rowKey, byte[] serializedAction)
        {
            var retrieveOperation = TableOperation.Retrieve<ActionsTableEntry>(partitionKey, rowKey);
            TableResult result = actionsTable.Execute(retrieveOperation);
            if (result.Result == null)
            {
                throw new ArgumentException(string.Format("Could not find an entry in the table for the given partitionKey, rowKey tuple: {0}, {1}",
                    partitionKey, rowKey));
            }
            var entry = (ActionsTableEntry)result.Result;
            entry.SerializedAction = serializedAction;

            var replaceOperation = TableOperation.Replace(entry);
            actionsTable.Execute(replaceOperation);
        }
    }
}
