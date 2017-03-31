using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvDb
{
    class SmvDbQueries
    {
        static void moduleDiffBetweenSessions(string firstSessionId, string secondSessionId)
        {
            try
            {
                using (var database = new SMVEntities())
                {
                    var firstSession = (from sessionTask in database.SessionTasks
                                        join taskModule in database.TaskModules on sessionTask.TaskID equals taskModule.TaskID
                                        join module in database.Modules on taskModule.ModuleID equals module.ModuleID
                                        where sessionTask.SessionID == firstSessionId
                                        select new
                                        {
                                            ModuleName = module.ModuleName
                                        }).Distinct().ToList();
                    var secondSession = (from sessionTask in database.SessionTasks
                                         join taskModule in database.TaskModules on sessionTask.TaskID equals taskModule.TaskID
                                         join module in database.Modules on taskModule.ModuleID equals module.ModuleID
                                         where sessionTask.SessionID == secondSessionId
                                         select new
                                         {
                                             ModuleName = module.ModuleName
                                         }).Distinct().ToList();

                    foreach (var firstSessionObject in firstSession)
                    {
                        if (!secondSession.Contains(firstSessionObject))
                        {
                            Console.WriteLine(firstSessionObject.ModuleName);
                        }
                    }

                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while accessing database " + e);
            }
        }

        static void pluginDiffBetweenSessions(string firstSessionId, string secondSessionId)
        {
            try
            {
                using (var database = new SMVEntities())
                {
                    var firstSession = (from sessionTask in database.SessionTasks
                                        join taskPlugin in database.TaskPlugins on sessionTask.TaskID equals taskPlugin.TaskID
                                        join plugin in database.Plugins on taskPlugin.PluginID equals plugin.PluginID
                                        where sessionTask.SessionID == firstSessionId
                                        select new
                                        {
                                            PluginName = plugin.PluginName
                                        }).Distinct().ToList();
                    var secondSession = (from sessionTask in database.SessionTasks
                                         join taskPlugin in database.TaskPlugins on sessionTask.TaskID equals taskPlugin.TaskID
                                         join plugin in database.Plugins on taskPlugin.PluginID equals plugin.PluginID
                                         where sessionTask.SessionID == secondSessionId
                                         select new
                                         {
                                             PluginName = plugin.PluginName
                                         }).Distinct().ToList();

                    foreach (var firstSessionObject in firstSession)
                    {
                        if (!secondSession.Contains(firstSessionObject))
                        {
                            Console.WriteLine(firstSessionObject.PluginName);
                        }
                    }
                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while accessing database " + e);
            }
        }

        static void actionDiffBetweenSessions(string firstSessionId, string secondSessionId)
        {
            try
            {
                using (var database = new SMVEntities())
                {
                    var firstSession = (from sessionTask in database.SessionTasks
                                        join taskAction in database.TaskActions on sessionTask.TaskID equals taskAction.TaskID
                                        where sessionTask.SessionID == firstSessionId
                                        select new
                                        {
                                            ActionName = taskAction.ActionName,
                                            WorkingDirectory = taskAction.WorkingDirectory,
                                            Success = taskAction.Success
                                        }).Distinct().ToList();
                    var secondSession = (from sessionTask in database.SessionTasks
                                         join taskAction in database.TaskActions on sessionTask.TaskID equals taskAction.TaskID
                                         where sessionTask.SessionID == secondSessionId
                                         select new
                                         {
                                             ActionName = taskAction.ActionName,
                                             WorkingDirectory = taskAction.WorkingDirectory,
                                             Success = taskAction.Success
                                         }).Distinct().ToList();

                    foreach (var firstSessionObject in firstSession)
                    {
                        if (!secondSession.Contains(firstSessionObject))
                        {
                            Console.WriteLine(firstSessionObject.ActionName + " " + firstSessionObject.WorkingDirectory + " " + firstSessionObject.Success);
                        }
                    }
                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while accessing database " + e);
            }
        }
    }
}
