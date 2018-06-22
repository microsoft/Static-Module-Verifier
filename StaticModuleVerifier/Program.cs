using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using System.Xml;
using System.Xml.Schema;
using System.Diagnostics;
using System.Web;
using System.Collections.Specialized;
using System.Collections;
using StaticModuleVerifier.Properties;
using SmvLibrary;
using System.Reflection;
using System.Globalization;
using System.Data.SqlClient;
using SmvDb;

namespace SmvSkeleton
{
    class Program
    {
        static SMVConfig smvConfig;
        const string configXmlFileName = "Config.xml";
        private static bool makeDefectsPortable = false; // TODO
        private static bool doAnalysis = false;
        private static string buildLogFileNamePrefix = "smvbuild";
        private static bool cloud = false;
        private static bool useDb = false;
        /// <summary>
        /// Prints the usage string to the console.
        /// </summary>
        static void PrintUsage()
        {
            Console.WriteLine(Resources.UsageString);
        }

        /// <summary>
        /// Prints detailed help text to the console.
        /// </summary>
        static void PrintHelp()
        {
            PrintUsage();
            Log.LogInfo(Resources.HelpTextWithoutUsageString);
        }

        /// <summary>
        /// Processes command line arguments for Analysis.
        /// </summary>
        /// <param name="args">The list of command line arguments.</param>
        /// <returns>true on success, false on failure.</returns>
        static bool ProcessArgs(string[] args)
        {
            bool help = false;
            bool unsupportedArgument = false;

            for (int i = 0; i < args.Length;)
            {
                if (args[i].Equals("/help", StringComparison.InvariantCultureIgnoreCase) || args[i].Equals("/?"))
                {
                    help = true;
                    PrintHelp();
                    break;
                }
                else if (args[i].Equals("/cloud", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogInfo("Using cloud.");
                    cloud = true;
                    Utility.schedulerType = "cloud";
                    i++;
                }
                else if (args[i].Equals("/db", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogInfo("Using db.");
                    useDb = true;
                    Utility.useDb = true;
                    i++;
                }
                else if (args[i].Equals("/jobobject", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogInfo("Using job objects.");
                    Utility.useJobObject = true;
                    i++;
                }
                else if (args[i].StartsWith("/config:", StringComparison.InvariantCulture) || args[i].StartsWith("/log:", StringComparison.InvariantCulture))
                {
                    String[] tokens = args[i].Split(new char[] { ':' }, 2);

                    if (tokens.Length == 2)
                    {
                        string value = tokens[1].Replace(@"""", String.Empty);

                        if (tokens[0].Equals("/config"))
                        {
                            Utility.SetSmvVar("configFilePath", value);
                        }
                        else if (tokens[0].Equals("/log"))
                        {
                            if (!Directory.Exists(value))
                            {
                                Log.LogFatalError("Log path does not exist.");
                            }
                            Log.SetLogPath(value);
                        }
                    }
                    i++;
                }
                else if (args[i].Equals("/analyze"))
                {
                    doAnalysis = true;
                    i++;
                }
                else if (args[i].StartsWith("/plugin:", StringComparison.InvariantCulture))
                {
                    String[] tokens = args[i].Split(new char[] { ':' }, 2);

                    if (File.Exists(tokens[1]))
                    {
                        Utility.pluginPath = tokens[1].Replace(Environment.GetEnvironmentVariable("smv"), "%smv%");
                        Assembly assembly = Assembly.LoadFrom(tokens[1]);
                        string fullName = assembly.ExportedTypes.ToList().Find(t => t.GetInterface(typeof(ISMVPlugin).FullName) != null).FullName;
                        Utility.plugin = (ISMVPlugin)assembly.CreateInstance(fullName);

                        if (Utility.plugin == null)
                        {
                            Log.LogFatalError("Could not load plugin.");
                        }
                        Utility.plugin.Initialize();
                    }
                    else
                    {
                        Log.LogFatalError("Plugin not found.");
                    }
                    i++;
                }
                else if (args[i].StartsWith("/projectfile:", StringComparison.InvariantCulture))
                {
                    String[] tokens = args[i].Split(new char[] { ':' }, 2);
                    Utility.SetSmvVar("projectFileArg", tokens[1]);
                    i++;
                }
                else if (args[i].Equals("/debug"))
                {
                    Utility.debugMode = true;
                    i++;
                }
                else if (args[i].StartsWith("/sessionID:", StringComparison.InvariantCultureIgnoreCase))
                {
                    String[] tokens = args[i].Split(new char[] { ':' }, 2);
                    if (!String.IsNullOrEmpty(tokens[1]))
                    {
                        Log.LogInfo("Setting session ID : " + tokens[1]);
                        Utility.sessionId = tokens[1];
                    }
                    else
                    {
                        Log.LogError("Session ID not found");
                    }
                    i++;
                }
                else if (args[i].StartsWith("/taskID:", StringComparison.InvariantCultureIgnoreCase))
                {
                    String[] tokens = args[i].Split(new char[] { ':' }, 2);
                    if (!String.IsNullOrEmpty(tokens[1]))
                    {
                        Log.LogInfo("Setting task ID : " + tokens[1]);
                        Utility.taskId = tokens[1];
                    }
                    else
                    {
                        Log.LogError("Task ID not found");
                    }
                    i++;
                }
                else
                {
                    unsupportedArgument = true;
                    i++;
                }
            }

            if (Utility.plugin != null)
            {
                Utility.plugin.ProcessPluginArgument(args);
            }
            else if (unsupportedArgument)
            {
                Log.LogFatalError("Unsupported arguments. Please provide a Plugin.");
            }

            if (help)
            {
                if (Utility.plugin != null)
                {
                    Utility.plugin.PrintPluginHelp();
                }
                return false;
            }

            return true;
        }

        static int Main(string[] args)
        {
            Utility.SetSmvVar("workingDir", Directory.GetCurrentDirectory());
            Utility.SetSmvVar("logFilePath", null);
            Utility.SetSmvVar("assemblyDir", Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            Utility.SetSmvVar("configFilePath", Path.Combine(Utility.GetSmvVar("workingDir"), configXmlFileName));
            Utility.SetSmvVar("smvLogFileNamePrefix", buildLogFileNamePrefix);
            try
            {
                Console.BufferHeight = Int16.MaxValue - 1;
            }
            catch (Exception)
            {

            }
            // Process commandline arguments.
            // Note that ProcessArgs will return false if execution should not continue. 
            // This happens in cases such as /help, /getAvailableModules, /searchmodules
            if (!ProcessArgs(args))
            {
                return -1;
            }
            if (useDb)
            {
                try
                {
                    using (var database = new SmvDbEntities())
                    {
                        SmvDb.Task task = database.Tasks.Where((x) => x.TaskID == Utility.taskId).FirstOrDefault();
                        if (task != null)
                        {
                            string argsString = string.Join(" ", args);
                            task.Arguments = argsString;

                            database.SaveChanges();
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogFatalError("Exception while updating database " + e);
                }
            }
            // Get the SMV version name.
            string smvVersionTxtPath = Path.Combine(Utility.GetSmvVar("assemblyDir"), "SmvVersionName.txt");
            if (!File.Exists(smvVersionTxtPath))
            {
                Log.LogFatalError("SmvVersionName.txt must exist in the SMV bin directory.");
            }
            string[] lines = File.ReadAllLines(smvVersionTxtPath);
            if (lines.Length < 1)
            {
                Log.LogFatalError("SmvVersionName.txt is empty.");
            }
            Utility.version = lines[0];

            // Consume specified configuration file
            smvConfig = Utility.GetSMVConfig();
            if (smvConfig == null)
            {
                Log.LogFatalError("Could not load Config file");
            }

            // Set the variables defined in the Variables node in the config file
            LoadGlobalVariables(smvConfig.Variables);

            // Project file value from command line overrides the Config value
            if (!String.IsNullOrEmpty(Utility.GetSmvVar("projectFileArg")))
            {
                Utility.SetSmvVar("projectFile", Utility.GetSmvVar("projectFileArg"));
            }

            bool buildResult = false;
            bool analysisResult = false;
            double buildTime = 0, analysisTime = 0;
            int localThreadCount = Environment.ProcessorCount;

            if (Utility.GetSmvVar("localThreads") != null)
            {
                localThreadCount = int.Parse(Utility.GetSmvVar("localThreads"));
            }
            Log.LogInfo(String.Format("Running local scheduler with {0} threads", localThreadCount));

            // Load the cloud config from an XML file.

            SMVCloudConfig cloudConfig = null;

            // Set up the schedulers.
            Utility.scheduler = new MasterSMVActionScheduler();
            LocalSMVActionScheduler localScheduler = new LocalSMVActionScheduler(localThreadCount);
            CloudSMVActionScheduler cloudScheduler = null;
            if (cloud)
            {
                cloudConfig = Utility.GetSMVCloudConfig();
                cloudScheduler = new CloudSMVActionScheduler(cloudConfig);
            }
            Utility.scheduler.AddScheduler("local", localScheduler);
            Utility.scheduler.AddScheduler("cloud", cloudScheduler);
            // Do build if specified in the configuration file
            if (smvConfig.Build != null)
            {
                Stopwatch sw = Stopwatch.StartNew();

                // Populate the actions dictionary that will be used by the schedulers.
                Utility.PopulateActionsDictionary(smvConfig.Build);

                if (string.IsNullOrEmpty(Utility.GetSmvVar("projectFile")))
                {
                    Utility.scheduler.Dispose();
                    Log.LogFatalError("Project file not set");
                }

                List<SMVActionResult> buildActionsResult = Utility.ExecuteActions(Utility.GetRootActions(smvConfig.Build));
                buildResult = Utility.IsExecuteActionsSuccessful(buildActionsResult);
                if (Utility.plugin != null && buildResult == false)
                {
                    Utility.plugin.Finally(true);
                }

                if (Utility.plugin != null)
                {
                    Utility.plugin.PostBuild(smvConfig.Build);
                }
                sw.Stop();
                buildTime = sw.Elapsed.TotalSeconds;
            }

            // If build succeeded or it was not specified, do analysis (if specified and called)
            if (smvConfig.Build == null || buildResult)
            {
                if (smvConfig.Analysis != null)
                {
                    if (doAnalysis)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        Utility.PopulateActionsDictionary(smvConfig.Analysis);

                        if (Utility.plugin != null)
                        {
                            Log.LogInfo("Using plugin " + Utility.plugin + " for analysis.");
                            analysisResult = Utility.plugin.DoPluginAnalysis(smvConfig.Analysis);

                            Utility.plugin.PostAnalysis(smvConfig.Analysis);
                        }
                        else
                        {
                            List<SMVActionResult> analysisActionsResult = Utility.ExecuteActions(Utility.GetRootActions(smvConfig.Analysis));
                            analysisResult = Utility.IsExecuteActionsSuccessful(analysisActionsResult);
                        }

                        if (!analysisResult)
                        {
                            Utility.scheduler.Dispose();
                            Utility.plugin.Finally(true);
                            Log.LogFatalError("Analysis failed.");
                        }

                        sw.Stop();
                        analysisTime = sw.Elapsed.TotalSeconds;
                    }
                }
                Utility.plugin.Finally(false);
            }
            else
            {
                Utility.plugin.Finally(true);
                Utility.scheduler.Dispose();
                Log.LogFatalError("Build failed, skipping Analysis.");
            }

            Utility.PrintResult(Utility.result, (int)buildTime, (int)analysisTime, true);

            Log.LogInfo(String.Format("Total time taken {0} seconds", (int)(buildTime + analysisTime)));

            if (Utility.plugin != null)
            {
                int bugCount = Utility.plugin.GenerateBugsCount();
                Log.LogInfo("Found " + bugCount + " bugs!");
                if (useDb)
                {
                    try
                    {
                        using (var database = new SmvDbEntities())
                        {
                            SmvDb.Task task = database.Tasks.Where((x) => x.TaskID == Utility.taskId).FirstOrDefault();
                            if (task != null)
                            {
                                string bugCountString = bugCount.ToString();
                                task.Bugs = bugCountString;
                                database.SaveChanges();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Utility.scheduler.Dispose();
                        Log.LogFatalError("Exception while updating database " + e);
                        return -1;
                    }
                }
            }
            localScheduler.Dispose();
            if (cloud) cloudScheduler.Dispose();

            if (makeDefectsPortable)
            {
                foreach (string bugDirectory in Directory.EnumerateDirectories(Path.Combine(Utility.smvVars["smvOutputDir"], "Bugs")))
                {
                    try
                    {
                        Utility.makeDefectPortable(bugDirectory);
                    }
                    catch (Exception e)
                    {
                        Log.LogFatalError("Exception occurred when making defect portable." + e.ToString());
                    }
                }
                Log.LogInfo("Defects, if any, made portable successfully");
            }
            return Convert.ToInt32(Utility.scheduler.errorsEncountered);
        }

        /// <summary>
        /// Sets the global variables, defined in the Config file, in the SmvVar dictionary
        /// </summary>
        /// <param name="globalVars">The variables defined in the config file.</param>
        static void LoadGlobalVariables(SetVar[] globalVars)
        {
            if (globalVars != null)
            {
                foreach (SetVar smvVar in globalVars)
                {
                    string value = Environment.ExpandEnvironmentVariables(smvVar.value);
                    Utility.SetSmvVar(smvVar.key, Utility.ExpandSmvVariables(value));
                }
            }
        }

    }
}
