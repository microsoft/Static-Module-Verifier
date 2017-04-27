using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using SmvDb;


namespace SmvCmdlets
{
    [Cmdlet(VerbsCommon.Get, "ModuleOverview")]
    [OutputType(typeof(List<ModuleOverview_Result>))]
    public class GetModuleOverview : PSCmdlet
    {

        [Parameter(Position = 0, Mandatory = true)]
        public string ModuleId
        {
            get { return moduleId; }
            set { moduleId = value; }
        }
        private string moduleId;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            try
            {
                using (SmvDbEntities entity = new SmvDbEntities())
                {
                    List<ModuleOverview_Result> results = entity.ModuleOverview(moduleId).ToList();
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
