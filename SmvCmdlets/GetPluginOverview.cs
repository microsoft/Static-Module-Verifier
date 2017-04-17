using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using SmvDb;

namespace SmvCmdlets
{
    [Cmdlet(VerbsCommon.Get, "PluginOverview")]
    [OutputType(typeof(List<PluginOverview_Result>))]
    public class GetPluginOverview : PSCmdlet
    {

        [Parameter(Position = 0, Mandatory = true)]
        public string PluginId
        {
            get { return pluginId; }
            set { pluginId = value; }
        }
        private string pluginId;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            try
            {
                using (SmvDbEntities entity = new SmvDbEntities())
                {
                    List<PluginOverview_Result> results = entity.PluginOverview(pluginId).ToList();
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
