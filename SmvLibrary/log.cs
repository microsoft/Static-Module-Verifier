using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvLibrary
{
    sealed public class Log
    {
        private Log()
        { 
        }

        static string logPath;


        public static void SetLogPath(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                logPath = path;
            }
        }

        /// <summary>
        /// Logs a message to the Console or a logger
        /// </summary>
        /// <param name="type">The type of message.</param>
        /// <param name="message">The message to log.</param>
        public static void WriteLog(string type, string message, TextWriter logger = null)
        {
            string result = String.Empty;

            if (String.IsNullOrEmpty(type))
            {
                result = message;
            }
            else
            {
                result = String.Format(CultureInfo.InvariantCulture, "[{0}] {1}", type, message);
            }

            if(logger != null)
            {
                lock (logger)
                {
                    logger.WriteLine(result);
                    logger.Flush();
                }
                return;
            }

            Console.WriteLine(result);
        }

        /// <summary>
        /// Logs a message to a file
        /// </summary>
        /// <param name="fileName">Filename.</param>
        /// <param name="text">The text to log.</param>
        public static void WriteToFile(string fileName, string text, bool useLogPath)
        {
            if (useLogPath)
            {
                if (!String.IsNullOrEmpty(logPath))
                {
                    string path = Path.Combine(logPath, fileName + ".txt");
                    using (StreamWriter sw = new StreamWriter(path))
                    {
                        sw.WriteLine(text);
                    }
                }
            }
            else
            {
                using (StreamWriter sw = new StreamWriter(fileName))
                {
                    sw.WriteLine(text);
                }
            }
        }

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogMessage(string message, TextWriter logger = null)
        {
            WriteLog("", message, logger);
        }

        /// <summary>
        /// Logs a INFO message
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string message, TextWriter logger = null)
        {
            WriteLog("INFO", message, logger);
        }

        /// <summary>
        /// Logs a DEBUG message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <param name="logger"></param>
        public static void LogDebug(string message, TextWriter logger = null)
        {
            if (Utility.debugMode)
            {
                WriteLog("DEBUG", message, logger);
            }
        }

        /// <summary>
        /// Logs a ERROR message
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogError(string message, TextWriter logger = null)
        {
            WriteLog("ERROR", message, logger);
        }

        /// <summary>
        /// Logs a error message and terminates
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogFatalError(string message)
        {
            WriteLog("FATAL ERROR", message, null);
            Environment.Exit(-1);
        }

        /// <summary>
        /// Logs a WARNING message
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogWarning(string message, TextWriter logger = null)
        {
            WriteLog("WARNING", message, logger);
        }

    }
}
