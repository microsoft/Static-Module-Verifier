using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using SmvDb;

namespace SmvCmdlets
{
    [Cmdlet(VerbsCommon.Get, "ModuleOverviewByPath")]
    [OutputType(typeof(List<ModuleOverviewByPath_Result>))]
    public class GetModuleOverviewByPath : PSCmdlet
    {

        [Parameter(Position = 0, Mandatory = true)]
        public string ModulePath
        {
            get { return modulePath; }
            set { modulePath = value; }
        }
        private string modulePath;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            try
            {
                using (SmvDbEntities entity = new SmvDbEntities())
                {
                    List<ModuleOverviewByPath_Result> results = entity.ModuleOverviewByPath(modulePath).ToList();
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
