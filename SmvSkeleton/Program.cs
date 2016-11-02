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
using System.Xml.Serialization;
using SmvSkeleton.Properties;
using SmvLibrary;
using System.Reflection;
using System.Globalization;

namespace SmvSkeleton
{
    class Program
    {
        static SMVConfig smvConfig;
        const string configXmlFileName = "Config.xml";
        const string configXsdFileName = "Config.xsd";
        const string cloudConfigXmlFileName = "CloudConfig.xml";
        const string cloudConfigXsdFileName = "CloudConfig.xsd";
        private static bool doAnalysis = false;
        private static string buildLogFileNamePrefix = "smvbuild";
        private static bool cloud = false;

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
                else if(args[i].Equals("/cloud", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogInfo("Using cloud.");
                    cloud = true;
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

        static void Main(string[] args)
        {
            Utility.SetSmvVar("workingDir", Directory.GetCurrentDirectory());
            Utility.SetSmvVar("logFilePath", null);
            Utility.SetSmvVar("assemblyDir", Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            Utility.SetSmvVar("configFilePath", Path.Combine(Utility.GetSmvVar("workingDir"), configXmlFileName));
            Utility.SetSmvVar("smvLogFileNamePrefix", buildLogFileNamePrefix);

            // Process commandline arguments.
            // Note that ProcessArgs will return false if execution should not continue. 
            // This happens in cases such as /help, /getAvailableModules, /searchmodules
            if (!ProcessArgs(args))
            {
                return;
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
            smvConfig = GetSMVConfig();

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

            SMVCloudConfig cloudConfig = GetSMVCloudConfig();

            // Set up the schedulers.
            Utility.scheduler = new MasterSMVActionScheduler();
            LocalSMVActionScheduler localScheduler = new LocalSMVActionScheduler(localThreadCount);
            CloudSMVActionScheduler cloudScheduler = null;
            if(cloud) cloudScheduler = new CloudSMVActionScheduler(cloudConfig);
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
                    Log.LogFatalError("Project file not set");
                }

                List<SMVActionResult> buildActionsResult = Utility.ExecuteActions(Utility.GetRootActions(smvConfig.Build));
                buildResult = Utility.IsExecuteActionsSuccessful(buildActionsResult);

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
                            Log.LogFatalError("Analysis failed.");
                        }

                        sw.Stop();
                        analysisTime = sw.Elapsed.TotalSeconds;
                    }
                }
            }
            else
            {
                Log.LogFatalError("Build failed, skipping Analysis.");
            }

            Utility.PrintResult(Utility.result, (int)buildTime, (int)analysisTime, true);
            Log.LogInfo(String.Format("Total time taken {0} seconds", (int)(buildTime + analysisTime)));

            localScheduler.Dispose();
            if(cloud) cloudScheduler.Dispose();
        }

        /// <summary>
        /// Load the cloud configuration from an XML file and store it in an SMVCloudConfig object.
        /// </summary>
        /// <returns>The SMVCloudConfig object containing the cloud configuration.</returns>
        static SMVCloudConfig GetSMVCloudConfig()
        {
            try
            {
                string cloudConfigXmlPath = Path.Combine(Utility.GetSmvVar("assemblyDir"), cloudConfigXmlFileName);
                string contents = Utility.ReadFile(cloudConfigXmlPath);
                if (!String.IsNullOrEmpty(contents))
                {
                    bool isXMLValid = false;
                    string schemaPath = Path.Combine(Utility.GetSmvVar("assemblyDir"), cloudConfigXsdFileName);

                    using (StringReader configContent = new StringReader(contents))
                    {
                        isXMLValid = Utility.ValidateXmlFile(schemaPath, configContent);
                    }

                    if (!isXMLValid)
                    {
                        Log.LogError("Could not load and validate XML file: " + Utility.GetSmvVar("configFilePath"));
                        return null;
                    }

                    XmlSerializer serializer = new XmlSerializer(typeof(SMVCloudConfig));
                    SMVCloudConfig config = null;
                    using (TextReader reader = new StringReader(contents))
                    {
                        config = (SMVCloudConfig)serializer.Deserialize(reader);
                    }

                    return config;
                }
                else
                {
                    return null;
                }
            }
            catch(Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Load the configuration from the config file and store it in an SMVConfig object.
        /// </summary>
        /// <returns>The configuration as an SMVConfig object.</returns>
        static SMVConfig GetSMVConfig()
        {
            string configFileContent = Utility.ReadFile(Utility.GetSmvVar("configFilePath"));

            if (!String.IsNullOrEmpty(configFileContent))
            {
                bool isXMLValid = false;
                string schemaPath = Path.Combine(Utility.GetSmvVar("assemblyDir"), configXsdFileName);

                using (StringReader configContent = new StringReader(configFileContent))
                {
                    isXMLValid = Utility.ValidateXmlFile(schemaPath, configContent);
                }

                if (!isXMLValid)
                {
                    Log.LogError("Could not load and validate XML file: " + Utility.GetSmvVar("configFilePath"));
                    return null;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(SMVConfig));
                using (TextReader reader = new StringReader(configFileContent))
                {
                    smvConfig = (SMVConfig)serializer.Deserialize(reader);
                }

                return smvConfig;
            }
            else
            {
                return null;
            }
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
