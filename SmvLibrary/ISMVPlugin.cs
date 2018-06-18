using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    public interface ISMVPlugin
    {
        /// <summary>
        /// Initialize the Plugin
        /// </summary>
        void Initialize();

        /// <summary>
        /// Prints help text specific to the plugin
        /// </summary>
        void PrintPluginHelp();

        /// <summary>
        /// Process custom arguments for use by the Plugin
        /// </summary>
        /// <param name="args">The arguments passed to smv.</param> 
        void ProcessPluginArgument(string[] args);

        /// <summary>
        /// Called before an action is run
        /// </summary>
        /// <param name="action">The action being run.</param>  
        void PreAction(SMVAction action);

        /// <summary>
        /// Called after an action is run
        /// </summary>
        /// <param name="action">The action being run.</param>
        void PostAction(SMVAction action);

        /// <summary>
        /// Called after build, if build node is present in the SMVConfig.
        /// </summary>
        /// <param name="buildResult">List of build actions.</param>
        void PostBuild(SMVAction[] buildActions);

        /// <summary>
        /// Do analysis if /analyze argument is not passed to SMV
        /// </summary>
        /// <param name="analysis">List of analysis actions.</param>
        /// <returns>true on success, false on failure.</returns>
        bool DoPluginAnalysis(SMVAction[] analysisActions);

        /// <summary>
        /// Called after analysis, if analysis node is present in the SMVConfig.
        /// </summary>
        /// <param name="analysisResult">List of result of the analysis actions.</param>
        void PostAnalysis(SMVAction[] analysisActions);

        /// <summary>
        /// Called after everything is completed, either with or without failure. 
        /// </summary>
        void Finally(bool failures);

        int GenerateBugsCount();
    }
}
