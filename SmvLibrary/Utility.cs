using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Globalization;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Xsl;
using System.Xml.Serialization;
using System.Data.SqlClient;
using SmvDb;

[assembly: CLSCompliant(true)]
namespace SmvLibrary
{
    public class Utility
    {
        const string configXsdFileName = "Config.xsd";
        const string xsltFileName = "Transform.xsl";
        const string cloudConfigXmlFileName = "CloudConfig.xml";
        const string cloudConfigXsdFileName = "CloudConfig.xsd";
        public static ISMVPlugin plugin = null;                                            /// The plugin used for this run of SMV.
        public static string pluginPath = string.Empty;                                    /// Name of the plugin. Used for cloud scheduling.
        public static MasterSMVActionScheduler scheduler;                                  /// Used to schedule actions.
        public static OrderedDictionary result = new OrderedDictionary();                  /// Stores the result of the actions.        
        public static string version;                                                      /// Name for this version of SMV. Used for cloud scheduling.        
        public static Dictionary<string, string> smvVars = new Dictionary<string, string>();      /// Dictionary to store the current run specific variables
        public static bool debugMode = false;
        public static string sessionId = String.Empty;
        public static string taskId = String.Empty;
        public static bool useDb = false;
        public static bool useJobObject = false;
        public static string schedulerType = "local";
        private static IDictionary<string, SMVAction> actionsDictionary = new Dictionary<string, SMVAction>();
        public static object lockObject = new object();
        private static List<SMVActionResult> actionResults;
        
        private Utility() { }

        /// <summary>
        /// Add actions to the actions dictionary that will be used by the scheduler.
        /// </summary>
        /// <param name="actions">The list of actions to be added to the dictionary.</param>
        public static void PopulateActionsDictionary(IEnumerable<SMVAction> actions)
        {
            foreach (SMVAction action in actions)
            {
                actionsDictionary.Add(action.name, action);
            }
        }

        /// <summary>
        /// Returns all the root actions (actions which are not the nextAction of any action).
        /// </summary>
        /// <param name="actions">The list of actions.</param>
        public static SMVAction[] GetRootActions(IEnumerable<SMVAction> actionList)
        {
            List<SMVAction> result = new List<SMVAction>();
            List<string> dependentActions = new List<string>();

            foreach (SMVAction action in actionList)
            {
                if (!string.IsNullOrEmpty(action.nextAction))
                {
                    dependentActions.Add(action.nextAction);    
                }
            }

            foreach (SMVAction action in actionList)
            {
                if (!dependentActions.Contains(action.name))
                {
                    result.Add(action);
                }
            }
            

            return result.ToArray();
        }

        /// <summary>
        /// Helper function that reads all lines in the files into one string, each line separated by a newline.
        /// </summary>
        /// <param name="filePath">The file to be read.</param>
        /// <returns>The contents of the file.</returns>
        public static string ReadFile(string filePath)
        {
            string lines = string.Empty;           

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        while (!sr.EndOfStream)
                        {
                            lines += sr.ReadLine() + Environment.NewLine;
                        }
                    }
                }

                return lines;
            }
            catch (FileNotFoundException e)
            {
                Log.LogError(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Copies a file and prints a logging message.
        /// </summary>
        /// <param name="source">Path to the file to be copied.</param>
        /// <param name="destination">Destination path of the file. Cannot be a directory.</param>
        public static void CopyFile(string source, string destination, TextWriter logger)
        {
            Log.LogInfo(String.Format(CultureInfo.InvariantCulture, "Copying file {0} to {1}.", source, destination), logger);
            File.Copy(source, destination, true);
        }

        /// <summary>
        /// Copy directory contents recursively
        /// </summary>
        public static void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    CopyDirectory(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        /// <summary>
        /// Deletes a file and prints a logging message.
        /// </summary>
        /// <param name="file">Path to the file to be deleted.</param>
        public static void DeleteFile(string file, TextWriter logger)
        {
            Log.LogInfo(String.Format(CultureInfo.InvariantCulture, "Deleting file {0}", file), logger);
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        /// <summary>
        /// Validates the config file against the schema.
        /// </summary>
        /// <param name="schemaPath">Path to the schema file.</param>
        /// <param name="configFile">Content of the config file</param>
        /// <returns>Boolean based on whether the validation failed</returns>
        public static bool ValidateXmlFile(string schemaPath, TextReader configFile)
        {
            XmlReader reader;
            Log.LogInfo("Validating XML against schema: " + schemaPath);

            // Load and validate the XML document.
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas.Add("", schemaPath);
            settings.ValidationType = ValidationType.Schema;

            try
            {
                reader = XmlReader.Create(configFile, settings);
            }
            catch (FileNotFoundException)
            {
                Log.LogError("Config file not found. Put it in the current directory or pass it in the arguments [/config:<PATH TO CONFIG FILE>]");
                return false;
            }

            ValidationEventHandler handler = new ValidationEventHandler(ValidationEventHandlerCallback);
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(reader);
                doc.Validate(handler);
            }
            catch (Exception e)
            {
                Log.LogError(e.ToString());
                return false;
            }

            // We do some validation ourselves because we can't use XSD 1.1 yet.
            foreach (XmlNode actionNode in doc.SelectNodes("SMVConfig/Analysis/Action"))
            {
                foreach (XmlNode copyArtifactNode in actionNode.SelectNodes("CopyArtifact"))
                {
                    string type = Environment.ExpandEnvironmentVariables(copyArtifactNode.Attributes["type"].Value).ToLowerInvariant();
                    string entity = Environment.ExpandEnvironmentVariables(copyArtifactNode.Attributes["entity"].Value).ToLowerInvariant();

                    if ((type == "rawcfgf" && entity != "compilationunit") ||
                        (type == "li" && entity == "functionunit") ||
                        (type == "bpl" && entity == "compilationunit"))
                    {
                        Log.LogError(String.Format(CultureInfo.InvariantCulture, "\"type\" attribute cannot have value \"{0}\" when \"entity\" attribute has value \"{1}\".", type, entity));
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Callback used by XmlDocument.Validate.
        /// </summary>
        /// <param name="sender">Not used.</param>
        /// <param name="e">Not used.</param>
        private static void ValidationEventHandlerCallback(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    Log.LogError(e.Message);
                    break;
                case XmlSeverityType.Warning:
                    Log.LogWarning(e.Message);
                    break;
            }
        }

        /// <summary>
        /// Get the path to an action's working directory.
        /// </summary>
        /// <param name="action">The action object.</param>
        /// <returns>Full path to the action's working directory.</returns>
        public static string GetActionDirectory(SMVAction action)
        {
            string path = string.Empty;
            if(action.Path != null && action.Path.value != null)
            {
                path = action.Path.value;
            }
            return Utility.ExpandVariables(path, action.variables);
        }

        /// <summary>
        /// Deletes all files and directories inside a directory.
        /// </summary>
        /// <param name="directory">The directory to clear.</param>
        public static void ClearDirectory(string directory)
        {
            DirectoryInfo di = new DirectoryInfo(directory);

            foreach (FileInfo f in di.GetFiles())
            {
                f.Delete();
            }

            foreach (DirectoryInfo d in di.GetDirectories())
            {
                d.Delete(true);
            }
        }

        /// <summary>
        /// Serializes an object into an array of bytes.
        /// </summary>
        /// <param name="obj">The object to be serialized.</param>
        /// <returns>The array of bytes representing the serialized object.</returns>
        public static byte[] ObjectToByteArray(object obj)
        {
            if(obj == null)
            {
                return null;
            }
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes an object from an array of bytes.
        /// </summary>
        /// <param name="bytes">The array to be used for deserialization.</param>
        /// <returns>The deserialized object.</returns>
        public static object ByteArrayToObject(byte[] bytes)
        {
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return bf.Deserialize(ms);
            }
        }
        /// <summary>
        /// Launches a process and executes the given command with the args provided.
        /// </summary>
        /// <param name="cmd">Command to execute.</param>
        /// <param name="args">Arguments to pass.</param>
        /// <param name="startDirectory">Directory in which to start the process</param>
        /// <param name="env">Environment variables</param>
        /// <returns>The process on success, null on failure.</returns>
        public static Process LaunchProcess(String cmd, String args, string startDirectory, SMVEnvVar[] env, TextWriter logger, JobObject jobObject)
        {
            try
            {

                var psi = new ProcessStartInfo(cmd, args);
                psi.RedirectStandardError = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;

                // Set environment variables for this process.
                SetEnvironmentVariables(psi, env, logger);

                Log.LogInfo(String.Format(CultureInfo.InvariantCulture, "Launching {0} with arguments: {1} ", cmd, args), logger);

                if (!String.IsNullOrEmpty(startDirectory))
                {
                    psi.WorkingDirectory = startDirectory;
                    Log.LogInfo("PATH: " + startDirectory, logger);
                }

                Process process = Process.Start(psi);
                if (process == null)
                {
                    Log.LogFatalError(String.Format(CultureInfo.InvariantCulture, "Could not create process: {0} with args {1}, working directory: {2}", cmd, args, startDirectory));
                }
                if (jobObject != null)
                {
                    jobObject.AddProcess(process.Id);
                }
                return process;
            }
            catch (Exception e)
            {
                Log.LogError(e.ToString());
                Log.LogFatalError(String.Format(CultureInfo.InvariantCulture, "Could not start process: {0} with args {1}, working directory: {2}", cmd, args, startDirectory));
                return null;
            }
        }

        /// <summary>
        /// Gets the child action for an action.
        /// </summary>
        /// <param name="action">The parent action.</param>
        /// <returns>The child action if one exists, else null.</returns>
        public static SMVAction GetNextAction(SMVAction action)
        {
            if(action.nextAction == null || !actionsDictionary.ContainsKey(action.nextAction))
            {
                return null;
            }
            SMVAction template = actionsDictionary[action.nextAction];
            SMVAction nextAction = new SMVAction(template, string.Empty);
            return nextAction;
        }

        /// <summary>
        /// Helper function that calls adds an array of actions to Utitlity.scheduler, waits until they all complete
        /// and returns the results. Call Utility.scheduler.AddAction() directory for finer-grained control.
        /// </summary>
        /// <param name="actions">The list of actions to be executed.</param>
        /// <returns>The list of results of the actions that were executed.</returns>
        public static List<SMVActionResult> ExecuteActions(SMVAction[] actions, SMVActionCompleteCallBack callback = null)
        {
            var waitHandle = new CountdownEvent(actions.Length);
            actionResults = new List<SMVActionResult>();
            if(callback == null)
            {
                callback = new SMVActionCompleteCallBack(DoneExecuteAction);
            }

            foreach (SMVAction action in actions)
            {
                if(action.variables == null)
                    action.variables = new Dictionary<string, string>(Utility.smvVars);
                if (!string.IsNullOrEmpty(action.analysisProperty))
                {
                    action.variables["analysisProperty"] = action.analysisProperty;
                }
                Utility.scheduler.AddAction(action, callback, waitHandle);
            }

            waitHandle.Wait();
            return actionResults;
        }

        /// <summary>
        /// Call back used by ExecuteActions().
        /// </summary>
        /// <param name="results"></param>
        /// <param name="context"></param>
        static void DoneExecuteAction(SMVAction action, IEnumerable<SMVActionResult> results, object context)
        {
            actionResults.AddRange(results);

            var countDownEvent = context as CountdownEvent;
            countDownEvent.Signal();
        }
        
        /// <summary>
        /// Called by schedulers to execute an action.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <returns>An SMVActionResult object representing the result of executing the action.</returns>
        public static SMVActionResult ExecuteAction(SMVAction action)
        {
            // NOTE: The code in this function must be thread safe.
            if(action == null)
            {
                return null;
            }

            // If there is a plugin, call PreAction first.
            if(plugin != null)
            {
                plugin.PreAction(action);
            }

            using(MemoryStream stream = new MemoryStream())
            {
                // We use a logger for writing messages since we can't output to the console in this function (As this
                // may be running in multiple threads).
                StreamWriter logger = new StreamWriter(stream);
                IDictionary<string, string> variables = action.variables;
                DateTime startTime = DateTime.Now;
                string actionPath = variables["workingDir"];
                string actionOutput = string.Empty;
                int cumulativeExitCode = 0;
                
                // Get the name of the action.
                string name = action.name;
                if (variables.ContainsKey("analysisProperty"))
                {
                    name = action.name + " - " + variables["analysisProperty"];
                }
                variables["name"] = action.name;
                Log.LogInfo("Running action: " + name, logger);

                // Get the path to the action.
                if (action.Path != null)
                {
                    actionPath = action.Path.value;
                }
                actionPath = ExpandVariables(actionPath, variables);
                variables["actionPath"] = actionPath;

                // Launch a cmd.exe process to run commands in.
                if (Console.InputEncoding.Equals(Encoding.UTF8))
                {
                    Console.InputEncoding = new UTF8Encoding(false);
                }
                
                
                // Run the commands.
                if (action.Command != null)
                {
                    foreach (SMVCommand cmd in action.Command)
                    {
                        JobObject jobObject = null;
                        if (useJobObject)
                        {
                            //Update maxTime and maxMemory allowed
                            int maxMemory = int.MaxValue;
                            int maxTime = int.MaxValue;
                            updateAttribute(ref maxTime, cmd.maxTime, "Time");
                            Log.LogDebug("Maximum time allowed for this command = " + maxTime);
                            updateAttribute(ref maxMemory, cmd.maxMemory, "Memory");

                            //Converting memory from MB to bytes, if input is valid
                            if (maxMemory < int.MaxValue)
                            {
                                maxMemory *= (1024 * 1024);
                            }
                            Log.LogDebug("Maximum memory allowed for this command = " + maxMemory);
                            jobObject = new JobObject();
                            jobObject.setConstraints(maxMemory, maxTime);
                        }
                        Process process = LaunchProcess("cmd.exe", "", actionPath, action.Env, logger, jobObject);
                        process.OutputDataReceived += (sender, e) => { Log.LogMessage(e.Data, logger); };
                        process.ErrorDataReceived += (sender, e) => { Log.LogMessage(e.Data, logger); };

                        // Get the command and arguments, and expand all environment as well as SMV variables.
                        string cmdAttr = ExpandVariables(Environment.ExpandEnvironmentVariables(cmd.value), variables);
                        string argumentsAttr = string.Empty;
                        if (!string.IsNullOrEmpty(cmd.arguments))
                        {
                            argumentsAttr = ExpandVariables(Environment.ExpandEnvironmentVariables(cmd.arguments), variables);
                        }

                        try
                        {
                            Log.LogInfo(String.Format(CultureInfo.InvariantCulture, "Launching {0} with arguments: {1}", cmdAttr, argumentsAttr), logger);
                            process.StandardInput.WriteLine(String.Join(" ", new String[] { cmdAttr, argumentsAttr }));
                            process.StandardInput.WriteLine("Exit %errorlevel%");
                            process.StandardInput.Close();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                            process.WaitForExit();
                            TimeSpan span = process.ExitTime - process.StartTime;
                            Log.LogMessage(string.Format("Command Exit code: {0}", process.ExitCode), logger);
                            cumulativeExitCode += Math.Abs(process.ExitCode);
                            if (useDb)
                            {
                                try
                                {
                                    using (var database = new SmvDbEntities())
                                    {
                                        var masterEntry = new TaskAction
                                        {
                                            ActionID = Guid.NewGuid().ToString(),
                                            TaskID = taskId,
                                            ActionName = action.name,
                                            Success = cumulativeExitCode.ToString(),
                                            ActionTime = span.ToString(),
                                            WorkingDirectory = variables["workingDir"]
                                        };
                                        database.TaskActions.Add(masterEntry);
                                        database.SaveChanges();
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.LogFatalError("Exception while updating database " + e);
                                }

                            }
                            if (useJobObject)
                            {
                                jobObject.QueryExtendedLimitInformation();
                                jobObject.Close();
                                jobObject.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            Log.LogInfo(e.ToString(), logger);
                            Log.LogInfo("Could not start process: " + cmdAttr, logger);
                            if (useJobObject)
                            {
                                jobObject.Close();
                                jobObject.Dispose();
                            }
                            return null;
                        }
                    }
                    
                }

                logger.Flush();
                stream.Position = 0;
                string output = new StreamReader(stream).ReadToEnd();

                if (debugMode)
                {
                    Log.WriteToFile(Path.Combine(actionPath, string.Format("smvexecute-{0}.log", action.name)), output, false);
                }
                Log.LogDebug("cumulative exit code is " + cumulativeExitCode);
                     
                DateTime endTime = DateTime.Now;

                action.result = new SMVActionResult(action.name, output, (cumulativeExitCode == 0),
                    cumulativeExitCode != 0 && action.breakOnError, (int)(endTime - startTime).TotalSeconds);

                // Call plugin post action only if we were successful in executing the action.
                if (cumulativeExitCode == 0)
                {
                    // get the output directory and set the output of the action from the build log.
                    if (action.name.Equals("NormalBuild"))
                    {
                        string logPath = Path.Combine(variables["workingDir"], variables["smvLogFileNamePrefix"] + ".log");
                        action.result.output = Utility.ReadFile(logPath);

                        variables["outputDir"] = ExtractBuildPath(variables["workingDir"], action.result.output, logger);
                        Utility.SetSmvVar("outputDir", variables["outputDir"]);
                    }

                    // Get the output directory and the analysis directory.
                    if (action.name.Equals("InterceptedBuild"))
                    {
                        string logPath = Path.Combine(variables["workingDir"], variables["smvLogFileNamePrefix"] + ".log");
                        action.result.output = Utility.ReadFile(logPath);                        
                    }

                    // Call the plugin's post action.
                    if (plugin != null)
                    {
                        plugin.PostAction(action);
                    }
                }
                else
                {
                    // are we sure we want to exit here... the cloud worker instance becomes 
                    // unhealthy after exiting here...
                    if (action.breakOnError)
                    {
                        Log.LogFatalError(String.Format("Action: {0}, failed.", name));
                    }
                    else
                    {
                        Log.LogError(String.Format("Action: {0}, failed.", name));
                    }
                }

                return action.result;
            }
        }

        public static void updateAttribute(ref int attribute, string cmdAttribute, string attributeName)
        {
            if(cmdAttribute != null)
            {
                try
                {
                    attribute = Convert.ToInt32(cmdAttribute);
                }
                catch (Exception)
                {
                    Log.LogWarning(String.Format("Could not convert {0} value from String to Int. Removing the {0} constraint.", attributeName));
                }
            }
        }

        /// <summary>
        /// Checks the result of ExecuteActions() for success.
        /// </summary>
        /// <param name="actionsResult">The result from ExecuteActions().</param>
        /// <returns>True on success, false on failure.</returns>
        public static bool IsExecuteActionsSuccessful(List<SMVActionResult> actionsResult)
        {
            foreach (SMVActionResult result in actionResults)
            {
                if (result.breakExecution)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This function is needed because it supports case insenstive string replacement.
        /// </summary>
        /// <param name="str">The string to be processed.</param>
        /// <param name="oldValue">The string to be replace.</param>
        /// <param name="newValue">The string that will replace <paramref name="oldValue"/></param>
        /// <param name="comparison">The type of comparison we will do.</param>
        /// <returns>The string with replacements.</returns>
        public static string StringReplace(string str, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(oldValue))
            {
                return null;
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                int previousIndex = 0;
                int index = str.IndexOf(oldValue, comparison);
                while (index != -1)
                {
                    sb.Append(str.Substring(previousIndex, index - previousIndex));
                    sb.Append(newValue);
                    index += oldValue.Length;

                    previousIndex = index;
                    index = str.IndexOf(oldValue, index, comparison);
                }
                sb.Append(str.Substring(previousIndex));

                return sb.ToString();
            }            
        }

        /// <summary>
        /// Sets environment variables based on a list of ENV nodes.
        /// </summary>
        /// <param name="env">The list of ENV nodes.</param>
        public static void SetEnvironmentVariables(ProcessStartInfo processInfo, SMVEnvVar[] env, TextWriter logger)
        {
            if (processInfo == null || env == null)
                return;

            Dictionary<string, string> variablesSet = new Dictionary<string, string>();
            variablesSet.Add("PATH", Environment.GetEnvironmentVariable("PATH"));
            String varKey = null, varValue = null;

            Log.LogInfo("Setting environment variables..", logger);


            foreach (SMVEnvVar envVar in env)
            {
                varKey = envVar.key;
                varValue = envVar.value ?? String.Empty;

                // We need to do this because Environment.ExpandEnvironmentVariable does not expand environment variables specific to a process

                foreach (KeyValuePair<string, string> pair in variablesSet)
                {
                    varValue = Regex.Replace(varValue, String.Format(CultureInfo.InvariantCulture, "%{0}%", pair.Key), pair.Value, RegexOptions.IgnoreCase);
                }

                varValue = ExpandSmvVariables(Environment.ExpandEnvironmentVariables(varValue));

                if (variablesSet.ContainsKey(varKey))
                    variablesSet.Remove(varKey);

                variablesSet.Add(varKey, varValue);

                if (processInfo.EnvironmentVariables.ContainsKey(varKey))
                    processInfo.EnvironmentVariables.Remove(varKey);

                Log.LogInfo(String.Format(CultureInfo.InvariantCulture, "Setting environment variable: {0}={1}", varKey, varValue), logger);
                processInfo.EnvironmentVariables.Add(varKey, varValue);
            }
        }

        /// <summary>
        /// Prints the result of the Build Actions.
        /// </summary>
        /// <param name="result">List of the action names and if they succeeded.</param>
        public static void PrintResult(IDictionary result, double buildTime, double analysisTime, bool abbreviated)
        {
            if (result != null && !abbreviated)
            {
                Log.LogMessage(Environment.NewLine);
                Log.LogMessage("=============================================================");
                Log.LogMessage("SMV Result:" + Environment.NewLine);

                foreach (DictionaryEntry actionResult in result)
                {
                    Log.LogMessage(String.Format(CultureInfo.InvariantCulture, "{0}  :  {1}", actionResult.Key.ToString().PadRight(45), actionResult.Value));
                }

                Log.LogMessage("=============================================================");
                Log.LogMessage(Environment.NewLine);

                Log.LogMessage("Build time: " + buildTime + " seconds.");
                Log.LogMessage("Analysis time: " + analysisTime + " seconds.");
            }            
        }

        /// <summary>
        /// Get a list of distinct matches for a given regex and a haystack
        /// </summary>
        /// <param name="pattern">The regex pattern.</param>
        /// <param name="haystack">The string to search in.</param>
        /// <returns>Array of unique matches.</returns>
        public static string[] GetUniqueRegexMatches(string pattern, string haystack)
        {
            MatchCollection matches = Regex.Matches(haystack, pattern);
            HashSet<string> result = new HashSet<string>(matches.Cast<Match>().Select(m => m.Value));
            return result.ToArray();
        }

        /// <summary>
        /// Returns the value corresponding to the key and null if not present
        /// </summary>
        /// <param name="key">The key to lookup.</param>
        /// <returns>Returns the value.</returns>
        public static string GetSmvVar(string key)
        {
            if (smvVars.ContainsKey(key))
            {
                return smvVars[key];
            }

            return null;
        }

        /// <summary>
        /// Sets the value corresponding to the key.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set corresponding to the key.</param>
        public static void SetSmvVar(string key, string value)
        {
            if (smvVars.ContainsKey(key))
            {
                smvVars.Remove(key);
            }
            smvVars.Add(key, value);
        }
        

        /// <summary>
        /// Replaces the name of each variable embedded in the specified
        /// string with the string equivalent of the value of the variable, then returns
        /// the resulting string.
        /// </summary>
        /// <param name="args">A string containing the names of zero or more environment variables. Each
        ///     environment variable is quoted with the dollar sign character ($) and enclosed in [], for example, [$PATH].</param>
        /// <returns>A string with each variable replaced by its value.</returns>
        public static string ExpandVariables(string arg, IDictionary<string, string> dict, TextWriter logger = null)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return string.Empty;
            }

            string[] argVars = GetUniqueRegexMatches(@"\[\$(.*?)\]", arg);
            foreach (String k in argVars)
            {
                // Extract the var name without the prefix ([$) and suffix (])
                string key = k.Substring(2, k.Length - 3);
                string value = dict.ContainsKey(key) ? dict[key] : String.Empty;
                
                if (value == null)
                {
                    Log.LogWarning(String.Format("Value of var ({0}) not set.", key), logger);
                }

                arg = StringReplace(arg, k, value, StringComparison.InvariantCultureIgnoreCase);
            }

            if (GetUniqueRegexMatches(@"\[\$(.*?)\]", arg).Length > 0)
                return ExpandVariables(arg, dict);

            return arg;
        }

        /// <summary>
        /// Replaces the name of each SMV variable embedded in the specified
        /// string with the string equivalent of the value of the variable, then returns
        /// the resulting string.
        /// </summary>
        /// <param name="args">A string containing the names of zero or more environment variables. Each
        ///     environment variable is quoted with the dollar sign character ($) and enclosed in [], for example, [$PATH].</param>
        /// <returns>A string with each SMV variable replaced by its value.</returns>
        public static string ExpandSmvVariables(string arg)
        {
            return ExpandVariables(arg, smvVars);
        }

        /// <summary>
        /// Load the cloud configuration from an XML file and store it in an SMVCloudConfig object.
        /// </summary>
        /// <returns>The SMVCloudConfig object containing the cloud configuration.</returns>
        public static SMVCloudConfig GetSMVCloudConfig()
        {
            try
            {
                string cloudConfigXmlPath = Path.Combine(GetSmvVar("assemblyDir"), cloudConfigXmlFileName);
                string contents = ReadFile(cloudConfigXmlPath);
                if (!String.IsNullOrEmpty(contents))
                {
                    bool isXMLValid = false;
                    string schemaPath = Path.Combine(GetSmvVar("assemblyDir"), cloudConfigXsdFileName);

                    using (StringReader configContent = new StringReader(contents))
                    {
                        isXMLValid = ValidateXmlFile(schemaPath, configContent);
                    }

                    if (!isXMLValid)
                    {
                        Log.LogFatalError("Could not load and validate XML file: " + GetSmvVar("configFilePath"));
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
                    Log.LogFatalError("Could not load and validate XML file: " + GetSmvVar("configFilePath"));
                    return null;
                }
            }
            catch (Exception)
            {
                Log.LogFatalError("Could not load and validate XML file: " + GetSmvVar("configFilePath"));
                return null;
            }
        }

        /// <summary>
        /// Load the configuration from the config file and store it in an SMVConfig object.
        /// </summary>
        /// <returns>The configuration as an SMVConfig object.</returns>
        public static SMVConfig GetSMVConfig()
        {
            SMVConfig smvConfig;
            string configFileContent = Transform(GetSmvVar("configFilePath"));
            if (!String.IsNullOrEmpty(configFileContent))
            {
                bool isXMLValid = false;
                string schemaPath = Path.Combine(GetSmvVar("assemblyDir"), configXsdFileName);
                using (StringReader configContent = new StringReader(configFileContent))
                {
                    isXMLValid = ValidateXmlFile(schemaPath, configContent);
                }
                Log.LogDebug("The validity is " + isXMLValid);
                if (!isXMLValid)
                {
                    Log.LogError("Could not load and validate XML file: " + GetSmvVar("configFilePath"));
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
        /// XSLT transformation to XML file at the filePath to include its component modules
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        static string Transform(string filePath)
        {
            // Reading the input XML file
            XmlDocument document = new XmlDocument();
            document.Load(filePath);
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
            xmlReaderSettings.IgnoreWhitespace = true;
            XmlReader xmlReader = XmlReader.Create(new StringReader(document.OuterXml), xmlReaderSettings);

            XslCompiledTransform xslTransform = new XslCompiledTransform();
            XsltSettings xsltSettings = new XsltSettings(true, true);
            // Get Tranformation file
            string xsltFilePath = Path.Combine(GetSmvVar("assemblyDir"), xsltFileName);
            try
            {
                xslTransform.Load(xsltFilePath, xsltSettings, new XmlUrlResolver());
            }
            catch (Exception e)
            {
                Log.LogFatalError("Exception occurred while looking for the Tranformation XSLT file" + e);
                return null;
            }

            // Prepare memory stream
            Stream memStream = new MemoryStream();
            StreamWriter streamWriter = new StreamWriter(memStream);

            // Prepare arguments with path to the modules
            XsltArgumentList xsltArgumentList = new XsltArgumentList();   
            xsltArgumentList.AddParam("absolute-path", "", Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar);

            // Transform input xml to output in memoryStream
            try
            {
                xslTransform.Transform(xmlReader, xsltArgumentList, streamWriter);
            } catch(Exception e)
            {
                Log.LogError("Exception occurred while transforming modules of XML " + e);
                return null;
            }
            Log.LogDebug("Transformation successful");
            streamWriter.Flush();
            memStream.Position = 0;

            // Convert memoryStream to string
            StreamReader streamReader = new StreamReader(memStream);
            string xmlContents = streamReader.ReadToEnd();
            streamWriter.Close();
            return xmlContents;
        }
        /// <summary>
        /// Extract build path from output
        /// </summary>
        /// <param name="output">Output of the build.</param>
        public static string ExtractBuildPath(string workingDir, string output, TextWriter logger)
        {
            if (!String.IsNullOrEmpty(workingDir) && !String.IsNullOrEmpty(output))
            {
                output = output.Replace("\r\n", Environment.NewLine);

                // Razzle
                Match match = Regex.Match(output, @"cl.exe @(.*?)$", RegexOptions.Multiline);
                string path = String.Empty;

                try
                {
                    if (match.Success)
                    {
                    
                        string key = match.Groups[1].Value;
                        path = key.Trim();
                        path = Path.GetDirectoryName(path);

                        Log.LogInfo("Build path found - " + path, logger);
                        return path;
                    }
                    else
                    {
                        //MSBuild
                        string regex = String.Format(CultureInfo.InvariantCulture, "/F{0}\"(\\S+)\"|/F{0}(\\S+)", "[a|d|m|p|R|e|o|r|i]");
                        match = Regex.Match(output, regex);

                        if (match.Success)
                        {
                            string key = string.Empty;
                            if (!string.IsNullOrEmpty(match.Groups[1].Value))
                            {
                                key = match.Groups[1].Value;
                            }
                            else if (!string.IsNullOrEmpty(match.Groups[2].Value))
                            {
                                key = match.Groups[2].Value;
                            }
                            else
                            {
                                Log.LogFatalError("Cannot extract build path from the log file!");
                            }
                            path = Path.Combine(workingDir, key.Trim());

                            //detect whether its a directory or file. If file get the parent directory

                            //FileAttributes attr = File.GetAttributes(path);

                            //if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                            //{
                            //  path = Path.GetDirectoryName(path);                                
                            //}
                            path = Path.GetDirectoryName(path);
                            Log.LogInfo("Build path found - " + path, logger);
                            return path;
                        }
                        else
                        {
                            //Log.LogFatalError("Regex match failed, could not extract build path");
                            //return string.Empty;
                            return Environment.CurrentDirectory;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Log.LogFatalError("Could not extract build path");
                    return string.Empty;
                }
            }
            else
            {
                Log.LogFatalError("No build output, could not extract build path");
                return string.Empty;
            }
        }

    }
}
