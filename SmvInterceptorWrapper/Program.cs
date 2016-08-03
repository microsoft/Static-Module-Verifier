using System;
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
        static void Main(string[] args)
        {
            // Get the output dir from the Environment variable set by SMV
            string smvOutDir = Environment.GetEnvironmentVariable("SMV_OUTPUT_DIR");

            List<string> iargs = args.Where(x => !x.Contains("/iwrap:") && !x.Contains(".rsp") && !x.Contains("/plugin:")).ToList();

            #region cl.exe
            if (args.Contains("/iwrap:cl.exe"))
            {
                Console.WriteLine("iwrap: cl.exe called with args " + string.Join(",", args));
                string[] unsupportedClExtensions = {".inf", ".mof", ".src", ".pdb" };

                // check for inf and other unsupported files and skip
                if (args.Where(a => unsupportedClExtensions.Any(b => a.Contains(b))).Count() > 0)
                {
                    return;
                }

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

                rspFileContent = rspContents;

                rspContents = Environment.ExpandEnvironmentVariables(" /nologo /w /Y- /analyze:only /analyze:plugin \"" + plugin +
                              "\" /errorReport:none" + " " + string.Join(" ", iargs)) + " " + rspContents;

                // get out dir
                string outDir = smvOutDir;
                
                // Persist the RSP file 
                // Remove file names (*.c) from the content
                Regex fileNameRegex = new Regex(@"([\w\.\\$-]+\.[c|cpp|cxx])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (Match m in fileNameRegex.Matches(rspContents))
                {
                    Console.WriteLine("match: " + m.Value);
                }
                rspFileContent = fileNameRegex.Replace(rspFileContent, String.Empty);

                using (System.IO.StreamWriter str = new StreamWriter(Path.Combine(outDir, "sdv_cl.rsp"), false))
                {
                    str.Write(rspFileContent);
                }

                // call CL.exe
                ProcessStartInfo psi = new ProcessStartInfo(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables("%SMV_ANALYSIS_COMPILER%")), 
                    Environment.ExpandEnvironmentVariables(rspContents));
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;

                Console.WriteLine("starting " + psi.ToString());

                if (!psi.EnvironmentVariables.ContainsKey("esp.cfgpersist.persistfile"))
                {
                    psi.EnvironmentVariables.Add("esp.cfgpersist.persistfile", outDir + "\\$SOURCEFILE.rawcfgf");
                    psi.EnvironmentVariables.Add("Esp.CfgPersist.ExpandLocalStaticInitializer", "1");
                    psi.EnvironmentVariables.Add("ESP.BplFilesDir", outDir);
                }
                
                Console.WriteLine("iwrap: cl.exe --> " + psi.FileName + " " + psi.Arguments);

                Process p = System.Diagnostics.Process.Start(psi);
                using(System.IO.StreamWriter sw = new System.IO.StreamWriter(outDir + "\\smvcl.log", true))
                {
                    sw.Write(p.StandardOutput.ReadToEnd());
                    sw.Write(p.StandardError.ReadToEnd());
                }

                File.AppendAllText(outDir + "\\smvcl.log", psi.Arguments);
            }
            #endregion
            #region smv2sql.exe
            else if (args.Contains("/iwrap:smv2sql.exe"))
            {
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

                // get directory where build artifacts are being placed
                // Note: for Link, this is not just getting the /out:<ARG> 
                // since Link is using inputs, and the location of the inputs 
                // is the real location where rawcfgf/li files are going to 
                // TODO: think about whether the LI files should be placed
                // in the outdir
                string outDir = smvOutDir;
                Console.WriteLine("iwrap: smv2sql.exe --> outdir is " + outDir);

                // May not be the best way to get the source dir.
                File.Copy(Environment.ExpandEnvironmentVariables(@"%SMV%\bin\smv2sql.exe.config"), Path.Combine(outDir, "smv2sql.exe.config"), true);
                File.Copy(Environment.ExpandEnvironmentVariables(@"%SMV%\bin\smvaccessor.dll"), Path.Combine(outDir, "smvaccessor.dll"), true);

                // Don't let the Smv2Sql phase run twice.
                if (File.Exists(Path.Combine(outDir, "smv2sql.log")))
                {
                    Console.WriteLine("iwrap: link.exe --> quitting since this phase is already complete");
                    return;
                }

                // Run Smv2Sql to put the LI and BPL files into a DB.
                Process smv2SqlProcess;
                var psi = new ProcessStartInfo(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%SMV%\bin\smv2sql.exe")));
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = outDir;
                psi.Arguments = outDir + " /log verbose";
                Console.WriteLine("iwrap: link.exe --> " + psi.FileName + " " + psi.Arguments);
                smv2SqlProcess = System.Diagnostics.Process.Start(psi);
                using (StreamWriter sw = new System.IO.StreamWriter(outDir + "\\smv2sql.log", true))
                {
                    sw.Write(smv2SqlProcess.StandardOutput.ReadToEnd());
                    sw.Write(smv2SqlProcess.StandardError.ReadToEnd());
                }
            }
            #endregion
            #region link.exe
            else if (args.Contains("/iwrap:link.exe"))
            {
                Console.WriteLine("iwrap: link.exe called with args " + string.Join(" ", iargs));
                Console.WriteLine("iwrap: link.exe --> " + Environment.ExpandEnvironmentVariables("slamcl_writer.exe"));

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
                Console.WriteLine("iwrap: link.exe --> outdir is " + outDir);

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
                StreamWriter sw;

                psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables("slamcl_writer.exe"), "--smv *");
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = outDir;

                Console.WriteLine("iwrap: link.exe --> " + psi.FileName + " " + psi.Arguments);

                p = System.Diagnostics.Process.Start(psi);
                using (sw = new System.IO.StreamWriter(outDir + "\\smvlink.log", true))
                {
                    sw.Write(p.StandardOutput.ReadToEnd());
                    sw.Write(p.StandardError.ReadToEnd());
                }

                files = files.Select(x => x + ".rawcfgf.obj").ToArray();

                #region BPL for compilation units
                /*
                 * IF WE WANT TO PRODUCE BPLs for each compilation unit then we uncommend the following section
                // Produce BPL files from the LI files.
                Process li2BplProcess;
                File.Copy(Environment.ExpandEnvironmentVariables(@"%SMV%\bin\liConversion.txt"), Path.Combine(outDir, "liConversion.txt"), true);
                psi = new ProcessStartInfo(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%SMV%\bin\li2bpl.exe")));
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = outDir;
                sw = new System.IO.StreamWriter(outDir + "\\smvli2bpl.log", true);
                foreach(string file in files)
                {
                    psi.Arguments = " -liFile " + Path.Combine(outDir, file);
                    Console.WriteLine("iwrap: li2bpl.exe --> " + psi.FileName + " " + psi.Arguments);
                    li2BplProcess = System.Diagnostics.Process.Start(psi);
                    sw.Write(li2BplProcess.StandardOutput.ReadToEnd());
                    sw.Write(li2BplProcess.StandardError.ReadToEnd());
                    // Rename all the BPL files, appending "compilationunit." to the name of each file. 
                    // Can li2bpl be modified to take the name of the output bpl file as an argument?
                    string oldBplPath = Path.Combine(outDir, "li2c_prog.bpl");
                    string newBplPath = Path.Combine(outDir, "compilationunit." + file + ".bpl");
                    if (File.Exists(oldBplPath))
                    {
                        File.Copy(oldBplPath, newBplPath, true);
                    }
                }
                sw.Close();
                */
                #endregion

                Process slamLinkProcess;

                // if only 1 li file then just copy that to slam.li
                if (files.Length == 1)
                {
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
                    Console.WriteLine("iwrap: link.exe --> " + psi.FileName + " " + psi.Arguments);
                    slamLinkProcess = System.Diagnostics.Process.Start(psi);
                    using (sw = new System.IO.StreamWriter(outDir + "\\smvlink.log", true))
                    {
                        sw.Write(slamLinkProcess.StandardOutput.ReadToEnd());
                        sw.Write(slamLinkProcess.StandardError.ReadToEnd());
                    }

                    // copy the slam.lib.li produced by slamlink to slam.li
                    if (File.Exists(outDir + "\\slam.lib.li"))
                    {
                        File.Copy(outDir + "\\slam.lib.li", outDir + "\\slam.li", true);
                    }
                }

                // remove rawcfgf files and their corresponding LI files
                rawcfgfFiles.ToList().ForEach(f => { File.Delete(f); File.Delete(f + ".obj.li"); });

                // create copy for linking with libs
                if (File.Exists(outDir + "\\slam.li"))
                {
                    File.Copy(outDir + "\\slam.li", outDir + "\\slamout.obj.li", true);
                }
                else
                {
                    Console.WriteLine("iwrap: link.exe --> No slam.li found in " + outDir);
                }

                // get any libs that need to be added and the corresponding rawcfgs
                List<string> libs = GetLibs(string.Join(" ", iargs) + " " + rspContents + " ");

                libs.RemoveAll(l => string.IsNullOrEmpty(l));

                foreach (string l in libs)
                {
                    Console.WriteLine("lib is " + l);
                    if (l.Equals(outDir)) continue;
                    try
                    {
                        string[] liFilesInLibDir = Directory.GetFiles(l, "slam.li");

                        foreach (string liFile in liFilesInLibDir)
                        {
                            Console.WriteLine("iwrap: Linking " + liFile + " " + outDir + "\\slam.obj.li");

                            File.Copy(liFile, outDir + "\\slamlib.obj.li", true);
                            File.Copy(outDir + "\\slamout.obj.li", outDir + "\\slamorig.obj.li");

                            psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables("slamlink.exe"));
                            psi.RedirectStandardError = true;
                            psi.RedirectStandardOutput = true;
                            psi.UseShellExecute = false;
                            psi.WorkingDirectory = outDir;
                            psi.Arguments = " --lib slamorig.obj slamlib.obj /out:slamout.obj";
                            slamLinkProcess = System.Diagnostics.Process.Start(psi);
                            using (sw = new System.IO.StreamWriter(outDir + "\\smvlink.log", true))
                            {
                                sw.Write(slamLinkProcess.StandardOutput.ReadToEnd());
                                sw.Write(slamLinkProcess.StandardError.ReadToEnd());
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }

                    if (File.Exists(outDir + "\\slamout.obj.li"))
                    {
                        File.Copy(outDir + "\\slamout.obj.li", outDir + "\\slam.li", true);
                    }
                
                }
            }
            #endregion
            #region lib.exe
            else if (args.Contains("/iwrap:lib.exe"))
            {
                Console.WriteLine("iwrap: Currently unimplemented. In general link functionality should be used.");
            }
            #endregion
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
