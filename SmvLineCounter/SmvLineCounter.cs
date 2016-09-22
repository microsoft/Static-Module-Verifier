using SmvLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLineCounter
{
    public class SmvLineCounter : ISMVPlugin
    {
        string[] extensions = { "*.c", "*.h", "*.cpp", "*.cxx", "*.hxx" };

        private void log(string message)
        {
            Log.WriteLog("SmvLineCounter", message);
        }

        public void Initialize()
        {
            log("Initialized");
        }

        public void PrintPluginHelp()
        {
            log("The following arguments need to be provided:");
            log("\t/configuraiton:<configuration>");
            log("\t/platform:<platform>");
        }

        public void ProcessPluginArgument(string[] args)
        {
        }

        public void PreAction(SMVAction action) { }

        public void PostAction(SMVAction action)
        {
            string sourcesDirectory = Utility.GetSmvVar("sourcesdirectory");
            int totalLines = 0;
            log("Line Counts: ");
            foreach (string extension in extensions)
            {
                foreach (string file in Directory.EnumerateFiles(sourcesDirectory, extension, SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(file);
                    log(String.Format(CultureInfo.CurrentCulture, "{0}: {1} lines", file, lines.Length.ToString()));
                    totalLines += lines.Length;
                }
            }
            log("Total: " + totalLines);
        }

        public void PostBuild(SMVAction[] buildActions)
        {
            log("Build completed successfully.");
        }

        public bool DoPluginAnalysis(SMVAction[] analysisActions)
        {
            var results = Utility.ExecuteActions(analysisActions);
            return Utility.IsExecuteActionsSuccessful(results);
        }

        public void PostAnalysis(SMVAction[] analysisActions) { }
    }
}