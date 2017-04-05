using SmvLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmvTest
{
    class Program
    {
        static void Main(string[] args)
        {
            /*Stages - 
             * 1 - Parent time limit exceeded; timeLimit=100
             * 2 - Parent memory limit exceeded; memoryLimit=49
             * 3 - Child time limit exceeded; timeLimit=100;
             * 4 - Child memory limit exceeded; memoryLimit<190;
            */
            int stage = Convert.ToInt32(args[0]);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd", "");
                Process process;
                //to see memory exceeded and time exceeded by child process
                if (stage == 3 || stage == 4)
                {
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = false;
                    process = Process.Start(psi);
                    process.StandardInput.WriteLine("test.bat");
                    process.StandardInput.Close();
                    process.WaitForExit();
                }
                else
                {
                    process = Process.Start(psi);
                }
                Thread.Sleep(5000);
                Marshal.AllocHGlobal(50 * 1024 * 1024);
                //to see time exceeded by parent process
                if (stage == 1)
                {
                    while (true)
                    {
                        int k = 4 * 4;
                    }
                }
            }
            catch (Exception)
            {
                
            }
            Thread.Sleep(5000);
        }
    }
}
