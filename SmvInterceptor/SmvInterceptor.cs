// -------------------------------------------------------------------
//	Interceptor.cs : A simple program interceptor for use command line subsitution
//			Used for modifying the behavior of cl.exe
//
//	Created 2007/07/17 aleks gershaft [microsoft]
//
// -------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

namespace SmvInterceptor
{
    class SmvInterceptor
    {
        /// Determines the need for verbose output. Controlled by INTERCEPT_DEBUG_SPEW environment variable 
        ///		or debug_spew 
        static bool debugSpew;
        static string interceptorFullPath;	/// Path to the interceptor binary
		static string interceptDir;			/// Directory of the interceptor
		static string binaryName;           /// Binary that the interceptor is replacing

        const string interceptXmlFileName = "%BE%-Intercept.xml";
        const string interceptLogName = "Intercept.log";

        static int Main(string[] args)
        {
            Init();

            //Read the XML document for preferences
            XmlDocument doc = OpenXmlFile();

            // Main node for this invocation
            XmlNode binaryNode = null;

            // Read from the XML file, if it's loaded.
            if (doc != null)
            {
                // Get settings node
                XmlNode settingsNode = doc.SelectSingleNode("INTERCEPT/SETTINGS");

                // Print out debug information on startup, if specified
                PrintEnvSpew(args, settingsNode);

                // Figure out if there's a binary name override
                string binaryOverride = RetrieveVariable("binary_override", settingsNode);
                if (binaryOverride != null)
                    binaryName = binaryOverride;

                // Find the reason node within environment or the settings file
                string reasonStr = RetrieveVariable("REASON", settingsNode);
                if (reasonStr != null && reasonStr.Length > 0)
                {
                    // Find the corresponding binary node within the XML for the specific reason
                    XmlNode reasonNode = SelectNode(doc.SelectNodes("INTERCEPT/REASON"), reasonStr);
                    if (reasonNode != null)
                        binaryNode = SelectNode(reasonNode.SelectNodes("BINARY"), binaryName);
                    else
                    {
                        string interceptXmlPath = Path.Combine(interceptDir, Environment.ExpandEnvironmentVariables(interceptXmlFileName));
                        Console.WriteLine("WARNING: Unable to find node for reason={0} in xml file: {1}", reasonStr, interceptXmlPath);
                    }
                }
            }

            return ProcessBinaryNode(binaryNode);
        }

        /// <summary>
        /// Initialize the environment variables: Path, binary name
        /// </summary>
        static private void Init()
        {
            // Full path to ourself
            interceptorFullPath = Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location).ToLowerInvariant();

            //Find out how we were named
            binaryName = Path.GetFileName(interceptorFullPath);

            // Directory to ourself
            interceptDir = Path.GetDirectoryName(interceptorFullPath);

            // Override with environment
            if (RetrieveBool("USE_CWD", null))
                interceptDir = Environment.CurrentDirectory;
        }

        /// <summary>
        /// Opens the XML file that controls the interceptor
        /// </summary>
        /// <returns>XmlDocument for the interceptor's XML file</returns>
        private static XmlDocument OpenXmlFile()
        {
            try
            {
                // Path to intercept.xml
                string interceptXmlPath = Path.Combine(interceptDir, Environment.ExpandEnvironmentVariables(interceptXmlFileName));
                // Load intercept.xml
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(interceptXmlPath, Encoding.ASCII));
                return doc;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to load Intercept.XML: ");
                Console.Error.WriteLine(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the node from the list with a specific named value for the attribute "name"
        /// </summary>
        /// <param name="list">List of XmlNodes that have attribute "name"</param>
        /// <param name="valueName">The </param>
        /// <returns></returns>
        private static XmlNode SelectNode(XmlNodeList list, string valueName)
        {
            // For case-insensitive compares
            valueName = valueName.ToLowerInvariant();

            // Loop through the nodes in the list
            foreach (XmlNode node in list)
            {
                // Make sure the attribute is there
                XmlAttribute attr = node.Attributes["name"];
                if (attr == null)
                {
#if debug
					throw new FormatException( string.Format( "Node {0} is missing the name attribute:\n{1}", node.Name, node.OuterXml ) );
#endif
                    Console.WriteLine("Node {0} is missing the name attribute:\n{1}", node.Name, node.OuterXml);
                }
                else if (String.Compare(valueName, attr.Value, StringComparison.OrdinalIgnoreCase) == 0)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// Executes the set of launch nodes for the binary that interceptor was invoked as. 
        /// </summary>
        /// <param name="binaryNode">The node in config file representing binary to be executing. (or NULL)</param>
        /// <returns>The result of launching the first executible in the list</returns>
        private static int ProcessBinaryNode(XmlNode binaryNode)
        {
            // If binaryNode is null, it will do a default passthrough with path stripping
            if (binaryNode == null)
            {
                return ProcessLaunchNode(null);
            }
            else
            {
                int finalReturnCode = 0;
                int numLaunches = 0;
                foreach (XmlNode launchNode in binaryNode.SelectNodes("LAUNCH"))
                {
                    // Process this LAUNCH node
                    int returnCode = ProcessLaunchNode(launchNode);

                    // Should we use this return code?
                    // Default is true for the first LAUNCH node, and false for the rest.
                    if (GetAttributeBool("use_return_code", launchNode, numLaunches == 0))
                        finalReturnCode = returnCode;

                    // Increment the count of how many things we launched
                    ++numLaunches;
                }

                return finalReturnCode;
            }
        }

        /// <summary>
        /// Log debug information to %SMV_OUTPUT_DIR%\smvexecute-Interceptor.log if in debug mode.
        /// Prefix any strings with [smvInterceptor]
        /// </summary>
        /// <param name="toLog">The string to be logged.</param>
        static void WriteInterceptorLog(string toLog)
        {
            // Only log if /debug enabled
            if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMV_DEBUG_MODE")))
            {
                return;
            }

            string smvOutDir = Environment.GetEnvironmentVariable("SMV_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(smvOutDir))
            {
                smvOutDir = Environment.CurrentDirectory;
            }

            File.AppendAllText(Path.Combine(smvOutDir, "smvexecute-Interceptor.log"), "[smvInterceptor] " + toLog + Environment.NewLine);
        }

        /// <summary>
        /// Calculates the executable to be used and processes the rules of adding/stripping arguments
        /// </summary>
        /// <param name="launchNode">Node containing the binary to be launched and the rules children</param>
        /// <returns>Result of the execution of the node</returns>
        private static int ProcessLaunchNode(XmlNode launchNode)
        {
            // Save the environment
            Dictionary<string, string> oldEnv = SaveEnvironment();

            // Whether to strip the interceptor's dir from the path
            bool stripPath = GetAttributeBool("strip_path", launchNode, true);

            // Modify the environment, required for the command and commandLine processing
            ModifyEnvironment(launchNode, stripPath);

            // Read the command binary and apply environment expansion
            string target = GetAttribute("filename", launchNode, binaryName);
            target = Environment.ExpandEnvironmentVariables(target);

            // Search the path for the target if it's not a full path
            if (!Path.IsPathRooted(target))
                target = FindInPath(target);

            // Make sure the file exists
            if (!File.Exists(target))
            {
                Console.WriteLine("WARNING: Interceptor cannot find file!: {0}", target);
                return -1;
            }

            // Make sure we're not calling ourselves again
            if (target == interceptorFullPath)
            {
                Console.WriteLine("WARNING: Launch loop detected, skipping");
                return -1;
            }

            // Modify the command line with new environment expansion
            string commandLine = ModifyCommandLine(launchNode, Environment.CommandLine);

            // Write to the intercept log
            //try
            //{
            //    string logDirectory = Path.Combine( interceptDir, interceptLogName );
            //    StreamWriter writer = new StreamWriter( new FileStream( logDirectory , FileMode.Append, FileAccess.Write ) );
            //    writer.WriteLine( "[{2}] Intercepted '{0}' with '{1}'", Environment.GetCommandLineArgs()[0], target, DateTime.Now );
            //    writer.Close();
            //}
            //catch ( Exception ) { }

            // Process echo nodes
            ProcessEchoNodes(launchNode);

            //Retrieve the priority for the new execution
            ProcessPriorityClass priority = RetrievePriority(launchNode);

            // Execute the command
            int returnCode = LaunchAndWait(target, commandLine, priority);

            // Restore the environment
            RestoreEnvironment(oldEnv);

            // Return the return code
            return returnCode;
        }

        /// <summary>
        /// Retrieves the desired priority for the new process invocation. (Normal if none is set)
        /// </summary>
        /// <param name="launchNode">The node containing all settings for </param>
        /// <returns>The desired priority for this execution</returns>
        private static ProcessPriorityClass RetrievePriority(XmlNode launchNode)
        {
            ProcessPriorityClass priority = ProcessPriorityClass.Normal;
            if (launchNode != null)
            {
                string priorityStr = GetAttribute("value", launchNode.SelectSingleNode("PRIORITY"),
                    ProcessPriorityClass.Normal.ToString());

                switch (priorityStr)
                {
                    case "Normal":
                        priority = ProcessPriorityClass.Normal;
                        break;
                    case "AboveNormal":
                        priority = ProcessPriorityClass.AboveNormal;
                        break;
                    case "BelowNormal":
                        priority = ProcessPriorityClass.BelowNormal;
                        break;
                    case "High":
                        priority = ProcessPriorityClass.High;
                        break;
                    case "Idle":
                        priority = ProcessPriorityClass.Idle;
                        break;
                    case "RealTime":
                        priority = ProcessPriorityClass.RealTime;
                        break;
                    default:
                        priority = ProcessPriorityClass.Normal;
                        break;
                }
            }
            return priority;
        }

        /// <summary>
        /// Prints out the information within the "<ECHO...>" node
        /// </summary>
        /// <param name="launchNode">The launch node that is currently being processed</param>
        private static void ProcessEchoNodes(XmlNode launchNode)
        {
            if (launchNode != null)
            {
                foreach (XmlNode echoNode in launchNode.SelectNodes("ECHO"))
                {
                    string text = GetAttribute("text", echoNode, null);
                    if (text != null)
                    {
                        text = Environment.ExpandEnvironmentVariables(text);
                        Console.WriteLine(text);
                    }
                }
            }
        }

        /// <summary>
        /// Iterates through the path variable in the environment to find the target executible
        /// </summary>
        /// <param name="target">The executable to be found</param>
        /// <returns>Full path to the executable</returns>
        private static string FindInPath(string target)
        {
            // If the path is rooted, just return it
            if (Path.IsPathRooted(target))
                return target;

            // Get the array of path pieces
            string[] pathPieces = Environment.GetEnvironmentVariable("path").Split(';');

            // Loop through them
            foreach (string pathPiece in pathPieces)
            {
                // Skip zero-length pieces
                if (pathPiece.Length < 1)
                    continue;

                // Whole target path
                string fullPath = Path.GetFullPath(Path.Combine(pathPiece.Trim('\"'), target));

                // Does it exist?
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // Didn't find it, just return the original string
            return target;
        }


        /// <summary>
        /// Launches the specified executable with the new priority
        /// </summary>
        /// <param name="target">The path to executable to be launched</param>
        /// <param name="commandLine">The command-line parameters to the executable to be launched with</param>
        /// <param name="priority">The priority the executable should be launched at</param>
        /// <returns>The return code of the executible</returns>
        private static int LaunchAndWait(string target, string commandLine, ProcessPriorityClass priority)
        {
            int exitCode;

            // Create the process without a new window and run it transparently through our output
            Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.ErrorDialog = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.StartInfo.FileName = target;
            p.StartInfo.Arguments = Environment.ExpandEnvironmentVariables(commandLine);

            p.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            p.StartInfo.UseShellExecute = false;

            // Setup the input/output forwarding
            p.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs eventArgs) { Console.WriteLine(eventArgs.Data); }
            );
            p.ErrorDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs eventArgs) { Console.Error.WriteLine(eventArgs.Data); }
            );

            try
            {
                WriteInterceptorLog("LAUNCH: " + p.StartInfo.FileName + " " + p.StartInfo.Arguments);
                p.Start();
                p.PriorityClass = priority;

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                p.WaitForExit();

                WriteInterceptorLog("EXIT: " + p.StartInfo.FileName + ". Exit code: " + p.ExitCode);
                exitCode = p.ExitCode;

            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to launch new executible from interceptor");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine("target = {0}", target);
                Console.Error.WriteLine("command line:");
                Console.Error.WriteLine(commandLine);

                exitCode = -1;
            }
            finally
            {
                p.Close();
            }

            return exitCode;
        }

        /// <summary>
        /// Modifies the environment based on the launchNode and strips the interceptor's directory from the path
        /// </summary>
        /// <param name="launchNode">The XML node describing environment modifications</param>
        /// <param name="stripPath">Should the interceptor directory be removed from path</param>
        private static void ModifyEnvironment(XmlNode launchNode, bool stripPath)
        {
            // Remove the tool from the path
            if (stripPath)
            {
                // Canonize the intercept dir
                string canonizedInterceptDir = CanonizePath(interceptDir);

                // StringBuilder for the new path to be rebuilt from pieces
                StringBuilder newPath = new StringBuilder();
                string[] pathPieces = Environment.GetEnvironmentVariable("PATH").Split(';');

                // Loop through each piece
                foreach (string piece in pathPieces)
                {
                    // Skip empty pieces
                    if (piece.Length < 1)
                        continue;

                    // Canonize this piece.  NOTE: This will convert a relative path into a full path, which could cause wonkiness
                    string canonPiece = Path.GetFullPath(CanonizePath(piece)).ToLowerInvariant();
                    if (canonPiece != null && canonPiece.Length > 0 && canonPiece != canonizedInterceptDir)
                    {
                        if (newPath.Length > 0)
                            newPath.Append(";");

                        newPath.Append(piece);
                    }
                }

                // Set final PATH into environment
                Environment.SetEnvironmentVariable("PATH", newPath.ToString());

                if (debugSpew)
                    Console.WriteLine("PATH = {0}", Environment.GetEnvironmentVariable("PATH"));
            }

            // Process any extra environment variables in the launchNode
            if (launchNode != null)
            {
                foreach (XmlNode node in launchNode.SelectNodes("ENV"))
                {
                    foreach (XmlAttribute attrib in node.Attributes)
                    {
                        string name = attrib.Name, value = attrib.Value;
                        value = Environment.ExpandEnvironmentVariables(value);
                        Environment.SetEnvironmentVariable(name, value);
                    }
                }
            }
        }

        /// <summary>
        /// Saves the current environment of the process
        /// </summary>
        /// <returns>Dictionary of environment variables</returns>
        private static Dictionary<string, string> SaveEnvironment()
        {
            Dictionary<string, string> oldEnv = new Dictionary<string, string>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                oldEnv.Add((string)entry.Key, (string)entry.Value);
            return oldEnv;
        }

        /// <summary>
        /// Restores the previous version of all environment variables
        /// </summary>
        /// <param name="oldEnv">The previous environment that should be restored</param>
        private static void RestoreEnvironment(Dictionary<string, string> oldEnv)
        {
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                string value;
                oldEnv.TryGetValue(key, out value);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        /// <summary>
        /// Changes the command line for the process as specified in the launchnode
        /// </summary>
        /// <param name="launchNode">The XML node that specifies parameters to add/remove to the command line</param>
        /// <param name="origCommandLine">Original command line to be modified</param>
        /// <returns>The new command line</returns>
        private static string ModifyCommandLine(XmlNode launchNode, string origCommandLine)
        {
            // Remove the original binary from the command line (including quotes)
            if (origCommandLine[0] == '\"')
                origCommandLine = origCommandLine.Remove(0, 2 + Environment.GetCommandLineArgs()[0].Length);
            else
                origCommandLine = origCommandLine.Remove(0, Environment.GetCommandLineArgs()[0].Length);

            // Default to inheriting the original command line
            string newCommandLine = origCommandLine;

            // Modify behavior based on XML if it's not NULL, otherwise just use the original
            if (launchNode != null)
            {
                // Are we supposed to inherit the command line?
                if (GetAttributeBool("inherit_command_line", launchNode, true))
                {
                    // Process the strip nodes based on the original command line
                    newCommandLine = StripArguments(launchNode.SelectNodes("STRIP"), origCommandLine);
                }
                else
                {
                    // Nothing to strip, so start with an empty command line
                    newCommandLine = "";
                }

                // Process the add nodes
                string addCommandLine = AddArguments(launchNode.SelectNodes("ADD"));

                // Concatenate the added nodes to original (stripped) command line.
                if (addCommandLine.Length > 0)
                    newCommandLine = newCommandLine + " " + addCommandLine;
            }

            // Now perform environment substitution
            newCommandLine = Environment.ExpandEnvironmentVariables(newCommandLine);

            // Return the new command line
            return newCommandLine;
        }

        /// <summary>
        /// Creates a string of new arguments for the binary
        /// </summary>
        /// <param name="addNodes">A list of nodes that need to be added</param>
        /// <returns>All of the new arguments to be added</returns>
        private static string AddArguments(XmlNodeList addNodes)
        {
            // Concatenate the strings with space-delimiter
            StringBuilder addCommandLine = new StringBuilder("");
            foreach (XmlNode addNode in addNodes)
            {
                try
                {
                    string addText = addNode.Attributes["text"].Value;
                    if (addText.Length < 1)
                        continue;

                    if (debugSpew)
                        Console.WriteLine("Adding {0}", addText);

                    addCommandLine.AppendFormat("{0} ", addText);
                }
                catch (Exception)
                {
                }
            }
            return addCommandLine.ToString();
        }

        /// <summary>
        /// Removes the set of arguments from the original command line
        /// </summary>
        /// <param name="stripNodes">A list of nodes to remove from the command line</param>
        /// <param name="originalCommandLine">Previous command line</param>
        /// <returns>The new command line with specific values removed</returns>
        private static string StripArguments(XmlNodeList stripNodes, string originalCommandLine)
        {
            foreach (XmlNode stripNode in stripNodes)
            {
                try
                {
                    string stripText = stripNode.Attributes["text"].Value;
                    if (stripText.Length < 1)
                        continue;

                    if (debugSpew)
                        Console.WriteLine("Removing {0}", stripText);

                    originalCommandLine = originalCommandLine.Replace(stripText, "");
                }
                catch (Exception)
                {
                }
            }
            return originalCommandLine;
        }

        /// <summary>
        ///  Retrieves the value from environment or settings node
        /// </summary>
        /// <param name="var">The name of the variable to retrieve</param>
        /// <param name="node">The node which contains that attribute</param>
        /// <returns>The value retrieved, or false if not found</returns>
        private static bool RetrieveBool(string var, XmlNode node)
        {
            string res = RetrieveVariable(var, node);
            if (res != null && bool.Parse(res))
                return true;
            return false;
        }

        /// <summary>
        /// Retrieves the value for the variable var from environment or settings
        /// </summary>
        /// <param name="var">The variable name (attribute) to retrieve</param>
        /// <param name="node">The XML settings to retrieve the value from</param>
        /// <returns>The value of the variable or NULL</returns>
        private static string RetrieveVariable(string var, XmlNode node)
        {
            string val = Environment.GetEnvironmentVariable("INTERCEPT_" + var);
            if (val != null)
                return val;
            if (node == null || node.Attributes[var.ToLowerInvariant()] == null)
                return null;
            return node.Attributes[var.ToLowerInvariant()].Value;
        }

        /// <summary>
        /// Retrieves the value of the attribute in the XML.
        /// </summary>
        /// <param name="attributeName">The name of the attribute to retrieve</param>
        /// <param name="node">The XML Node that may contain the attribute</param>
        /// <param name="defaultValue">The value returned if the attribute does not exist</param>
        /// <returns>The value retrieved or default value</returns>
        private static string GetAttribute(string attributeName, XmlNode node, string defaultValue)
        {
            try
            {
                return node.Attributes[attributeName].Value;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves the value (as boolean) of the attribute in the XML.
        /// </summary>
        /// <param name="attributeName">The name of the attribute to be retrieved</param>
        /// <param name="node">XML Node that may contain that attribute</param>
        /// <param name="defaultValue">The value returned if the attribute does not exist</param>
        /// <returns>The retrieved value or the default value</returns>
        private static bool GetAttributeBool(string attributeName, XmlNode node, bool defaultValue)
        {
            if (node != null)
            {
                XmlAttribute attribute = node.Attributes == null ? null : node.Attributes[attributeName];
                if (attribute != null)
                {
                    try
                    {
                        return Convert.ToBoolean(attribute.Value, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("WARNING: value of attribute {0} is not boolean: {1}", attributeName, attribute.OuterXml);
                    }
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Convert the path to a string without trailing \ or quotes
        /// </summary>
        /// <param name="str">Path to canonize</param>
        /// <returns>Canonized path</returns>
        private static string CanonizePath(string str)
        {
            return str.Replace("\"", null).TrimEnd('\\').ToLowerInvariant();
        }

        private static void PrintEnvSpew(string[] args, XmlNode settingsNode)
        {
            debugSpew = RetrieveBool("debug_spew", settingsNode);
            if (debugSpew)
            {
                Console.WriteLine("INCLUDE = {0}", Environment.GetEnvironmentVariable("INCLUDE"));
                Console.WriteLine("LIB = {0}", Environment.GetEnvironmentVariable("LIB"));
                Console.WriteLine("PATH = {0}", Environment.GetEnvironmentVariable("PATH"));
                foreach (string arg in args)
                {
                    if (arg[0] != '@')
                        continue;

                    try
                    {
                        string rsp = arg.Substring(1);
                        Console.WriteLine("Contents of response file ({0}):", rsp);
                        using (var reader = new StreamReader(new FileStream(rsp, FileMode.Open, FileAccess.Read)))
                        {
                            Console.Write(reader.ReadToEnd());
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error while reading .rsp file");
                    }
                }
                foreach (XmlAttribute setting in settingsNode.Attributes)
                {
                    Console.WriteLine("Setting::" + setting.Name + " = " + setting.Value);
                }
            }
        }
    }
}