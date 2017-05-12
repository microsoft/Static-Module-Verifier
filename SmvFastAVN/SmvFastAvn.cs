using SmvLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace FastAVN
{
    public class SMVFastAVN : ISMVPlugin
    {

        private void log(string message)
        {
            SmvLibrary.Log.WriteLog("FastAVN", message, null);
        }

        public bool DoPluginAnalysis(SMVAction[] analysisActions)
        {
            foreach(SMVAction a in analysisActions)
            {
                if(a.name.Equals("CheckEntryPoints"))
                {
                    // get the directories etc.
                    string workingDir = Utility.GetSmvVar("smvOutputDir");
                    List<SMVAction> entryPointActions = new List<SMVAction>();
                    foreach(string d in Directory.EnumerateDirectories(workingDir).Where(s => !s.EndsWith("Bugs")))
                    {
                        SMVAction ea = new SMVAction(a, d);
                        ea.variables = new Dictionary<string, string>(Utility.smvVars);
                        ea.variables.Add("epDir", d);
                        ea.variables.Add("epDirName", new DirectoryInfo(d).Name);
                        ea.variables["workingDir"] = d;
                         ea.nextAction = null;
                        entryPointActions.Add(ea);              
                    }
                    Utility.ExecuteActions(entryPointActions.ToArray());
                }
                else
                {
                    a.variables = new Dictionary<string, string>(Utility.smvVars);
                    Utility.ExecuteAction(a, false, false, null);
                }
            }

            return true;
        }

        public void Initialize()
        {
            Log.LogInfo("SMVFastAVN initialized.");
        }

        public void PostAction(SMVAction action)
        {
            // todo

        }

        public void PostAnalysis(SMVAction[] analysisActions)
        {
            
            log("Merging results...");
            Utility.ExecuteAction(analysisActions.Where(a => a.name.Equals("MergeResults")).First(), false, false, null);
        }

        public void PostBuild(SMVAction[] buildActions)
        {
            // nothing to do here
            return;
        }

        public void PreAction(SMVAction action)
        {
            // nothing to do here
            return;
        }

        public void PrintPluginHelp()
        {
            // nothing special for FastAVN
            return;
        }

        public void ProcessPluginArgument(string[] args)
        {
            // no special arguments for FastAVN
        }

        public int GenerateBugsCount()
        {
            string workingDir = Path.Combine(Utility.GetSmvVar("smvOutputDir"),"Bugs");
            Regex regex = new Regex(@"Bug*");
            int bugFoldersCount = System.IO.Directory.GetDirectories(workingDir).Where(path => regex.IsMatch(path)).ToList().Count();
            return bugFoldersCount; 
        }
    }
}
