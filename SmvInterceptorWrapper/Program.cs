﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;

namespace SmvInterceptorWrapper
{
    class Program
    {

        static bool debugMode = false;
        const int DEFAULT_DEBUG_TIMEOUT = (5 * 60 * 1000);

        static int Main(string[] args)
        {
            // Get the output dir from the Environment variable set by SMV

            string smvOutDir = Environment.GetEnvironmentVariable("SMV_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(smvOutDir))
            {
                smvOutDir = Environment.CurrentDirectory;
            }
            string smvclLogPath = Path.Combine(smvOutDir, "smvcl.log");
            string smvRecordLogPath = Path.Combine(smvOutDir, "record.log");
            StringBuilder smvclLogContents = new StringBuilder();
            List<string> iargs = args.Where(x => !x.Contains("/iwrap:") && !x.Contains(".rsp") && !x.Contains("/plugin:")).ToList();

            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMV_DEBUG_MODE")))
            {
                debugMode = true;
            }

            #region cl.exe            
            if (args.Contains("/iwrap:cl.exe"))
            {
                if (args.Contains("/E")) return 0;
                smvclLogContents.Append("iwrap: cl.exe called with args " + string.Join(",", args));
                string[] unsupportedClExtensions = {".inf", ".mof", ".src", ".pdb", ".def", "~", ".rc" };

                // check for inf and other unsupported files and skip
                List<string> unsupportedArgs = args.Where(a => unsupportedClExtensions.Any(b => a.ToLowerInvariant().EndsWith(b))).ToList();
                args = args.Where(a => !unsupportedClExtensions.Any(b => a.ToLowerInvariant().EndsWith(b))).ToArray();
                iargs = iargs.Where(a => !unsupportedClExtensions.Any(b => a.ToLowerInvariant().EndsWith(b))).ToList();
                
                // try to make progress no matter what. 
                //if (unsupportedArgs.Count() > 0)
                //{
                //    smvclLogContents.Append("iwrap: cl.exe unsupported extension:" + string.Join(",", unsupportedArgs));
                //    File.WriteAllText(smvclLogPath, smvclLogContents.ToString());
                //    return 1;
                //}

                // get the name of the plugin
                string plugin = args.Where(x => x.Contains("/plugin:")).ToList().First().Replace("/plugin:", String.Empty);

                // call slamcl
                string rspContents = string.Empty;
                string rspFileContent = string.Empty;
                string rspFile = args.ToList().Find(x => x.Contains(".rsp") || x.StartsWith("@", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(rspFile))
                {
                    rspFile = rspFile.Replace("@", "");
                    rspContents = System.IO.File.ReadAllText(rspFile);
                    rspContents = rspContents.Replace("/analyze-", "");
                }                              

                // remove unsupported flags. currently we are not removing anything.
                Regex[] unsupportedFlags = {new Regex(@"\s+\/Yu[^\s]+\s{1}", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+\/Fp[^\s]+\s{1}", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+\/Yc[^\s]+\s{1}", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\/d1nodatetime)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\/d1trimfile:[^\s]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\/d2AllowCompatibleILVersions)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\/d2Zi\+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\/d1nosafedelete)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"\s+(\-nosafedelete)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"-DDBG=\d\s{1}|\/DDBG=\d\s{1}", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                               //new Regex(@"-DDEBUG\s{1}|\/DDEBUG\s{1}", System.Text.RegularExpressions.RegexOptions.IgnoreCase),            
                                               //new Regex("\\s+\\/Fp\"[^\"]+\"{1}", System.Text.RegularExpressions.RegexOptions.None)
                                           };

                foreach (Regex rgx in unsupportedFlags)
                {
                    rspContents = rgx.Replace(rspContents, " ");
                }
               
                rspContents = rspContents.Replace("\r", " ");
                rspContents = rspContents.Replace("\n", " ");
                rspContents = rspContents.Replace("/W4", string.Empty);
                rspContents = rspContents.Replace("/W3", string.Empty);
                rspContents = rspContents.Replace("/W2", string.Empty);
                rspContents = rspContents.Replace("/W1", string.Empty);
                rspContents = rspContents.Replace("/WX", string.Empty);

                // Remove any CA plugins from the RSP file, as they conflict with CfgPersist.dll
                Regex codeAnalysisRegex1 = new Regex("/analyze:quiet|/analyze:stacksize\\d+|/analyze:projectdirectory\".*?\"");
                Regex codeAnalysisRegex2 = new Regex("/analyze:rulesetdirectory\".*?\"|/analyze:ruleset\".*?\"");
                Regex codeAnalysisRegex3 = new Regex("/analyze:plugin\".*?WindowsPrefast.dll\"|/analyze:plugin\".*?EspXEngine.dll\"|/analyze:plugin\".*?drivers.dll\"");
                Regex codeAnalysisRegex4 = new Regex(@"/analyze[^\p{Pd}\w:.]+(\s)*"); // Sometimes, garbage characters are passed to /analyze; this removes them

                rspContents = codeAnalysisRegex1.Replace(rspContents, String.Empty);
                rspContents = codeAnalysisRegex2.Replace(rspContents, String.Empty);
                rspContents = codeAnalysisRegex3.Replace(rspContents, String.Empty);
                rspContents = codeAnalysisRegex4.Replace(rspContents, "/");

                rspFileContent = rspContents;

                string rspContentsDebug = Environment.ExpandEnvironmentVariables(" /nologo /w /Y- /D_PREFAST_ /errorReport:none" + " " + string.Join(" ", iargs)) + " " + rspContents + " /P /Fi.\\sdv\\";
                rspContentsDebug = rspContentsDebug.Replace("/analyze:only", " ");
                rspContentsDebug = rspContentsDebug.Replace("/analyze:autolog-", " ");

                rspContents = Environment.ExpandEnvironmentVariables(" /nologo /w /Y- /analyze:only /analyze:plugin \"" + plugin +
                              "\" /errorReport:none" + " " + string.Join(" ", iargs)) + " " + rspContents;



                // Persist the RSP file 
                // Remove file names (*.c) from the content
                // Ensure we get cases with quotation marks first
                Regex fileNameRegexQuote1 = new Regex(@"([\s]+("")[\w\p{Pd}\p{Pc}\s\\.]+\.c\b(""))", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                Regex fileNameRegexQuote2 = new Regex(@"([\s]+("")[\w\p{Pd}\p{Pc}\s\\.]+\.(cpp|cxx)\b(""))", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                Regex fileNameRegex1 = new Regex(@"([\s]+[\w\p{Pd}\p{Pc}\\.]+\.c\b)", RegexOptions.IgnoreCase|RegexOptions.Multiline);
                Regex fileNameRegex2 = new Regex(@"([\s]+[\w\p{Pd}\p{Pc}\\.]+\.(cpp|cxx))", RegexOptions.IgnoreCase|RegexOptions.Multiline);

                List<string> fileNames = new List<string>();
                int count = 0;

                foreach (Match m in fileNameRegexQuote1.Matches(rspContents))
                {
                    count++;
                    smvclLogContents.Append("match1: " + m.Value + Environment.NewLine);
                }
                foreach (Match m in fileNameRegexQuote2.Matches(rspContents))
                {
                    count++;
                    smvclLogContents.Append("match2: " + m.Value + Environment.NewLine);
                }
                foreach (Match m in fileNameRegex1.Matches(rspContents))
                {
                    count++;
                    smvclLogContents.Append("match1: " + m.Value + Environment.NewLine);
                }
                foreach (Match m in fileNameRegex2.Matches(rspContents))
                {
                    count++;
                    smvclLogContents.Append("match2: " + m.Value + Environment.NewLine);
                }
                rspFileContent = fileNameRegexQuote1.Replace(rspFileContent, String.Empty);
                rspFileContent = fileNameRegexQuote2.Replace(rspFileContent, String.Empty);
                rspFileContent = fileNameRegex1.Replace(rspFileContent, String.Empty);
                rspFileContent = fileNameRegex2.Replace(rspFileContent, String.Empty);

                File.WriteAllText(Path.Combine(smvOutDir, "sdv_cl.rsp"), rspFileContent);

                // if no files are left (only .src etc. was given) then just return. nothing to do
                if(count == 0) { return 0; }

                // call CL.exe

                ProcessStartInfo psi = new ProcessStartInfo(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables("%SMV_ANALYSIS_COMPILER%")), Environment.ExpandEnvironmentVariables(rspContents));
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                
                if (!psi.EnvironmentVariables.ContainsKey("esp.cfgpersist.persistfile"))
                {
                    psi.EnvironmentVariables.Add("esp.cfgpersist.persistfile", smvOutDir + "\\$SOURCEFILE.rawcfgf");
                    psi.EnvironmentVariables.Add("Esp.CfgPersist.ExpandLocalStaticInitializer", "1");
                    psi.EnvironmentVariables.Add("ESP.BplFilesDir", smvOutDir);
                }
                
                WriteInterceptorLog("LAUNCH: iwrap: " + psi.FileName + " " + psi.Arguments);
                WriteInterceptorLog("PATH: " + Environment.ExpandEnvironmentVariables("%PATH%"));

                StringBuilder sb = new StringBuilder();
                sb.Append("esp.cfgpersist.persistfile=");
                sb.Append((smvOutDir + "\\$SOURCEFILE.rawcfgf"));
                sb.Append("  ");
                sb.Append("Esp.CfgPersist.ExpandLocalStaticInitializer=1");
                sb.Append("  ");
                sb.Append("ESP.BplFilesDir=");
                sb.Append(smvOutDir);
                
                WriteInterceptorLog("Process-specific environment variables: " + sb.ToString());

                Process p = System.Diagnostics.Process.Start(psi);

                smvclLogContents.Append(p.StandardOutput.ReadToEnd());
                smvclLogContents.Append(p.StandardError.ReadToEnd());
                File.WriteAllText(smvclLogPath, smvclLogContents.ToString());
                p.WaitForExit();
                
                WriteInterceptorLog("EXIT: CL.exe.  Exit code: " + p.ExitCode);

                if (debugMode)
                {
                    // Run with /P to get preprocessed output for debugging
                    psi = new ProcessStartInfo(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables("%SMV_ANALYSIS_COMPILER%")), Environment.ExpandEnvironmentVariables(rspContentsDebug));
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    p = System.Diagnostics.Process.Start(psi);

                    // Get debug timeout from environment variable set in XML or command line
                    int debugTimeout = 0;

                    try
                    {
                        debugTimeout = int.Parse(Environment.GetEnvironmentVariable("SMV_DEBUG_TIMEOUT"));
                    }
                    catch (Exception)
                    {
                        // If there was any trouble, just set it to 5 minutes
                        debugTimeout = DEFAULT_DEBUG_TIMEOUT;
                    }

                    // Allow 5 minutes for this, then continue; don't hang indefinitely on debug
                    p.WaitForExit(debugTimeout);

                    // Call ESPSMVPRINT_AUX
                    foreach (FileInfo f in new DirectoryInfo(smvOutDir).GetFiles())
                    {
                        if (f.Name.EndsWith(".rawcfgf"))
                        {
                            string espSmvPrintPath = Path.Combine(Environment.ExpandEnvironmentVariables("%SDV%"), "bin", "engine", "espsmvprintaux.exe");

                            // If we are not in an SDV environment, this is pointless; break
                            if (!File.Exists(espSmvPrintPath)) break;

                            psi = new ProcessStartInfo(espSmvPrintPath, '"' + f.FullName + '"');
                            psi.RedirectStandardOutput = true;
                            psi.RedirectStandardError = true;
                            psi.UseShellExecute = false;

                            string cfgOutputPath = f.FullName.Replace(".rawcfgf", ".rawcfgf.txt");
                            string cfgErrorPath = f.FullName.Replace(".rawcfgf", ".rawcfgf.err");

                            p = System.Diagnostics.Process.Start(psi);
                            File.WriteAllText(cfgOutputPath, p.StandardOutput.ReadToEnd());
                            File.WriteAllText(cfgErrorPath, p.StandardError.ReadToEnd());

                            // Allow 5 minutes for each, then continue; don't hang indefinitely on debug
                            p.WaitForExit(debugTimeout);
                        }
                    }
                }

                return p.ExitCode;
            }
            #endregion
            #region link.exe
            else if (args.Contains("/iwrap:link.exe"))
            {
                //Console.WriteLine("iwrap: link.exe called with args " + string.Join(" ", iargs));
                //Console.WriteLine("iwrap: link.exe --> " + Environment.ExpandEnvironmentVariables("slamcl_writer.exe"));

                // get rsp contents
                string rspContents = string.Empty;
                string rspFile = args.ToList().Find(x => x.Contains(".rsp") || x.StartsWith("@", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(rspFile))
                {
                    rspFile = rspFile.Replace("@", "");
                    rspContents = System.IO.File.ReadAllText(rspFile);
                    rspContents = rspContents.Replace("\r", " ");
                    rspContents = rspContents.Replace("\n", " ");
                }
                
                // get out dir
                string outDir = smvOutDir;
                //Console.WriteLine("iwrap: link.exe --> outdir is " + outDir);

                // get rid of previous LI files, log files etc. 
                Directory.GetFiles(outDir, "*.li").ToList().ForEach(f => File.Delete(f));
                if(File.Exists(Path.Combine(outDir, "smvlink.log")))
                {
                    File.Delete(Path.Combine(outDir, "smvlink.log"));
                }

                //TODO: if link is called multiple times to create multiple binaries
                // we will still only create 1 LI file for all the LI files that are available
                // This happens in cases where a directory is used to store all the .obj 
                // files and then link is called multiple times with a subset of the .obj
                // files to produce various binaries. 
                // The solution is to look at the link command in its entirety, 
                // and extract the obj files that are being used in it. 
                // we should then produce the LI for the corresponding obj.li files.
                // the obj files are specified in the link rsp or command line and can 
                // be extracted using a regex, similar to how we extract lib files and locations.

                string[] rawcfgfFiles = System.IO.Directory.GetFiles(outDir, "*.rawcfgf");

                string[] files = rawcfgfFiles.Select(x => System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileName(x))).ToArray();

                files = files.Where(x => !x.Contains(".obj")).ToArray();

                ProcessStartInfo psi;
                Process p;

                WriteInterceptorLog("PATH: " + Environment.ExpandEnvironmentVariables("%PATH%"));

                foreach (string file in rawcfgfFiles)
                {
                    psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables("slamcl_writer.exe"), "--smv " + '"' + file + '"' + " " + ('"' + file + ".obj\"") );
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = outDir;

                    WriteInterceptorLog("LAUNCH: link: " + psi.FileName + " " + psi.Arguments);

                    //Console.WriteLine("iwrap: link.exe --> " + psi.FileName + " " + psi.Arguments);

                    try
                    {
                        p = System.Diagnostics.Process.Start(psi);
                        File.AppendAllText(outDir + "\\smvlink1.log", p.StandardOutput.ReadToEnd());
                        File.AppendAllText(outDir + "\\smvlink1.log", p.StandardError.ReadToEnd());

                        p.WaitForExit();

                        WriteInterceptorLog("EXIT: slamcl_writer.exe.  Exit code: " + p.ExitCode);
                        if (p.ExitCode != 0) return p.ExitCode;
                    }
                    catch (System.ComponentModel.Win32Exception w)
                    {
                        // In event of a Win32 exception, dump useful info
                        WriteInterceptorLog(w.Message);
                        WriteInterceptorLog(w.ErrorCode.ToString());
                        WriteInterceptorLog(w.NativeErrorCode.ToString());
                        WriteInterceptorLog(w.StackTrace);
                        WriteInterceptorLog(w.Source);
                        Exception e = w.GetBaseException();
                        WriteInterceptorLog(e.Message);
                        return w.ErrorCode;
                    }
                }

                // Surround each filename in quotes to support cases with spaces
                files = files.Select(x => '"' + x + ".rawcfgf.obj" + '"').ToArray();
                
                // if only 1 li file then just copy that to slam.li
                if (files.Length == 1)
                {
                    // Get rid of quotations before we copy 
                    files[0] = files[0].Replace('"', ' ').Trim();
                    WriteInterceptorLog("DEBUG: only one .rawcfgf.obj file found.");
                    File.Copy(outDir + "\\" + files.ElementAt(0) + ".li", outDir + "\\slam.li", true);
                }
                else
                {
                    // many LI files, link all LIs together
                    psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables("slamlink.exe "));
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = outDir;
                    psi.Arguments = " --lib " + string.Join(" ", files);

                    WriteInterceptorLog("DEBUG: about to run slamlink on " + files.Length + " .rawcfgf.obj files");
                    WriteInterceptorLog("LAUNCH: iwrap: " + psi.FileName + " " + psi.Arguments);

                    //Console.WriteLine("iwrap: link.exe --> " + psi.FileName + " " + psi.Arguments);
                    Process slamLinkProcess = System.Diagnostics.Process.Start(psi);
                    File.AppendAllText(Path.Combine(outDir, "smvlink2.log"), slamLinkProcess.StandardOutput.ReadToEnd());
                    File.AppendAllText(Path.Combine(outDir, "smvlink2.log"), slamLinkProcess.StandardError.ReadToEnd());

                    // copy the slam.lib.li produced by slamlink to slam.li
                    if (File.Exists(outDir + "\\slam.lib.li"))
                    {
                        File.Copy(outDir + "\\slam.lib.li", outDir + "\\slam.li", true);
                    }
                    slamLinkProcess.WaitForExit();

                    WriteInterceptorLog("EXIT: slamlink.exe.  Exit code: " + slamLinkProcess.ExitCode);
                    if (slamLinkProcess.ExitCode != 0) return slamLinkProcess.ExitCode;
                }

                // remove rawcfgf files and their corresponding LI files
                //rawcfgfFiles.ToList().ForEach(f => { File.Delete(f); File.Delete(f + ".obj.li"); });

                // create copy for linking with libs
                if (File.Exists(outDir + "\\slam.li"))
                {
                    File.Copy(outDir + "\\slam.li", outDir + "\\slamout.obj.li", true);
                }
                else
                {
                    //Console.WriteLine("iwrap: link.exe --> No slam.li found in " + outDir);
                }

                // get any libs that need to be added and the corresponding rawcfgs
                List<string> libs = GetLibs(string.Join(" ", iargs) + " " + rspContents + " ");

                libs.RemoveAll(l => string.IsNullOrEmpty(l));

                WriteInterceptorLog("DEBUG: About to attempt processing " + libs.Count + " libraries." );

                foreach (string l in libs)
                {
                    //Console.WriteLine("lib is " + l);
                    if (l.Equals(outDir)) continue;
                    try
                    {
                        WriteInterceptorLog("DEBUG: Processing lib " + l);
                        string[] liFilesInLibDir = Directory.GetFiles(l, "slam.li");

                        foreach (string liFile in liFilesInLibDir)
                        {
                            Process slamLinkProcess;

                            //Console.WriteLine("iwrap: Linking " + liFile + " " + outDir + "\\slam.obj.li");

                            File.Copy(liFile, outDir + "\\slamlib.obj.li", true);
                            File.Copy(outDir + "\\slamout.obj.li", outDir + "\\slamorig.obj.li");

                            psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables("slamlink.exe"));
                            psi.RedirectStandardError = true;
                            psi.RedirectStandardOutput = true;
                            psi.UseShellExecute = false;
                            psi.WorkingDirectory = outDir;
                            psi.Arguments = " --lib slamorig.obj slamlib.obj /out:slamout.obj";

                            WriteInterceptorLog("DEBUG: about to run slamlink on " + liFile);
                            WriteInterceptorLog("LAUNCH: iwrap: " + psi.FileName + " " + psi.Arguments);

                            slamLinkProcess = System.Diagnostics.Process.Start(psi);

                            File.AppendAllText(outDir + "\\smvlink3.log", slamLinkProcess.StandardOutput.ReadToEnd());
                            File.AppendAllText(outDir + "\\smvlink3.log", slamLinkProcess.StandardError.ReadToEnd());

                            slamLinkProcess.WaitForExit();

                            WriteInterceptorLog("EXIT: slamlink.exe.  Exit code: " + slamLinkProcess.ExitCode);

                            if (slamLinkProcess.ExitCode != 0) return slamLinkProcess.ExitCode;
                        }
                    }
                    catch(Exception e)
                    {
                        WriteInterceptorLog(e.ToString());
                    }

                    if (File.Exists(outDir + "\\slamout.obj.li"))
                    {
                        File.Copy(outDir + "\\slamout.obj.li", outDir + "\\slam.li", true);
                    }
                }

                WriteInterceptorLog("DEBUG: Succesfully exiting link.exe section.");

                return 0;
            }
            #endregion
            #region lib.exe
            else if (args.Contains("/iwrap:lib.exe"))
            {
                //Console.WriteLine("iwrap: Currently unimplemented. Consider using link functionality.");
                WriteInterceptorLog("DEBUG: Entering lib.exe region.  This should never happen!");
                return 1;
            }
            #endregion
            #region record
            if (args.Any(a => a.Contains("/iwrap:record")))
            {
                string currentProcessName = args.First(a => a.Contains("/iwrap:record")).Split('-')[1];
                string[] calls = { };
                if (File.Exists(smvRecordLogPath))
                {
                    calls = File.ReadAllLines(smvRecordLogPath);
                }
                string content = currentProcessName + " " + string.Join(",", args) + Environment.NewLine;
                if (!calls.Contains(content))
                {
                    File.AppendAllText(smvRecordLogPath, content);
                }
                return 0;
            }
            #endregion
            return 0;
        }

        /// <summary>
        /// Log debug information to %SMV_OUTPUT_DIR%\smvexecute-Interceptor.log if in debug mode.
        /// Prefix any strings with [smvInterceptorWrapper]
        /// </summary>
        /// <param name="toLog">The string to be logged.</param>
        static void WriteInterceptorLog(string toLog)
        {
            if (!debugMode)
            {
                return;
            }

            string smvOutDir = Environment.GetEnvironmentVariable("SMV_OUTPUT_DIR");

            if (string.IsNullOrWhiteSpace(smvOutDir))
            {
                smvOutDir = Environment.CurrentDirectory;
            }

            File.AppendAllText(Path.Combine(smvOutDir, "smvexecute-Interceptor.log"), "[smvInterceptorWrapper] " + toLog + Environment.NewLine);
        }

        /// <summary>
        /// Given a list of arguments, figures out what obj/lib files are 
        /// present, and returns the list
        /// </summary>
        /// <param name="args">arguments to process</param>
        /// <returns>list of libraries</returns>
        static List<string> GetLibs(string args)
        {
            // Extract all matches containing paths of lib and obj files which are enclosed by an optional space and does not have a space within
            MatchCollection matchList = Regex.Matches(args, @"[\s]?([^\s""]+\.((lib)|(obj)))[\s]$?", RegexOptions.IgnoreCase);
            List<string> spaceTokens = matchList.Cast<Match>()
                                        .Select(match => match.Value.Trim().Replace(@"/implib:", string.Empty))
                                        .Where(match => !match.StartsWith(@"/out", StringComparison.OrdinalIgnoreCase))
                                        .Where(match => !match.StartsWith(@"\\out", StringComparison.OrdinalIgnoreCase))
                                        .Select(match =>  System.IO.Path.GetDirectoryName(match.Trim())).ToList();

            // Extract all matches containing paths of lib and obj files which are enclosed within quotes
            matchList = Regex.Matches(args, @"""([^""]+\.(lib|obj))""", RegexOptions.IgnoreCase);
            List<string> quoteTokens = matchList.Cast<Match>()
                                        .Select(match => match.Value.Trim().Replace(@"/implib:", string.Empty))
                                        .Where(match => !match.StartsWith(@"/out", StringComparison.OrdinalIgnoreCase))
                                        .Where(match => !match.StartsWith(@"\\out", StringComparison.OrdinalIgnoreCase))
                                        .Select(match => System.IO.Path.GetDirectoryName(match.Replace("\"", ""))).ToList();

            return spaceTokens.Union(quoteTokens).ToList();
        }

        /// <summary>
        /// Given arguments to compiler, gets the output folder
        /// by matching on a regular expression
        /// </summary>
        /// <param name="args">arguments to process</param>
        /// <returns>compilers output directory</returns>
        static string GetOutDirCl(string args)
        {
            string regex = String.Format(CultureInfo.InvariantCulture, "/F{0}\"(\\S+)\"|/F{0}(\\S+)", "[a|d|m|p|R|e|o|r|i]");
            Match match = Regex.Match(args, regex);
            if(match.Success)
            {
                if (match.Groups[1].Success) return System.IO.Path.GetDirectoryName(match.Groups[1].Value);
                if (match.Groups[2].Success) return System.IO.Path.GetDirectoryName(match.Groups[2].Value);
            }
            return Environment.GetEnvironmentVariable("OBJECT_ROOT");
        }

        /// <summary>
        /// arguments to link are processed to figure out the 
        /// output directory of link
        /// </summary>
        /// <param name="args">arguments to process</param>
        /// <returns>output directory of link</returns>
        static string GetOutDirLink(string args)
        {
            Match match = Regex.Match(args, "/out:\"(.[^\"]+)\"|/out:(\\S+)", RegexOptions.IgnoreCase);
            string retVal = string.Empty;
            if (match.Success)
            {                
                if(match.Groups[1].Success) retVal = System.IO.Path.GetDirectoryName(match.Groups[1].Value);
                if (match.Groups[2].Success) retVal = System.IO.Path.GetDirectoryName(match.Groups[2].Value);
            }

            if (string.IsNullOrEmpty(retVal))
            {
                retVal = Environment.GetEnvironmentVariable("OBJECT_ROOT");
            }
            else
            {
                // check for relative path
                if (!Path.IsPathRooted(retVal))
                {
                    retVal = Path.Combine(Environment.CurrentDirectory, retVal);
                }
            }
            return retVal;
        }
            
        /// <summary>
        /// Extract build path from output
        /// </summary>
        /// <param name="output">Output of the build.</param>
        public static string ExtractBuildPath()
        {
            string lines = string.Empty;
            using (FileStream fs = new FileStream("smvbuild.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        lines += sr.ReadLine() + Environment.NewLine;
                    }
                }
            }
            
            if (!String.IsNullOrEmpty(lines))
            {
                lines = lines.Replace("\r\n", Environment.NewLine);
                Match match = Regex.Match(lines, @"\/\/iwrap: outdir is (.*?)$", RegexOptions.Multiline);
                string path = String.Empty;

                if (match.Success)
                {
                    try
                    {
                        string key = match.Groups[1].Value;
                        path = key.Trim();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("iwrap: Could not extract build path");
                    }
                    Console.WriteLine("iwrap: Build path found - " + path);
                    return path;
                }
                else
                {
                    Console.WriteLine("iwrap: Could not extract build path");
                    return string.Empty;
                }
            }
            else
            {
                Console.WriteLine("iwrap: Could not extract build path");
                return string.Empty;
            }
        }
    }
}
