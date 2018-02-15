using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using SmvDb;

namespace SmvCmdlets
{
    [Cmdlet(VerbsCommon.Get, "SessionActionFailureSummary")]
    [OutputType(typeof(List<SessionActionFailureSummary_Result>))]
    public class GetSessionActionFailureSummary : PSCmdlet
    {

        [Parameter(Position = 0, Mandatory = true)]
        public string SessionId
        {
            get { return sessionId; }
            set { sessionId = value; }
        }
        private string sessionId;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            try
            {
                using (SmvDbEntities entity = new SmvDbEntities())
                {
                    List<SessionActionFailureSummary_Result> results = entity.SessionActionFailureSummary(sessionId).ToList();
                    WriteObject(results);
                }
            }
            catch (Exception e)
            {
                WriteObject("Exception " + e);
            }
        }
    }
}
