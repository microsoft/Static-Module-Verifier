-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[taskDetailsParameter] 
	-- Add the parameters for the stored procedure here
	@sessionIdParameter varchar(50) = NULL
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
		SELECT Modules.ModulePath, Plugins.PluginName, TaskActions.ActionName, TaskActions.WorkingDirectory, TaskActions.ActionTime, TaskActions.Success FROM SessionTasks 
INNER JOIN TaskModules
ON taskmodules.taskid = sessiontasks.taskid 
INNER JOIN Modules
ON TaskModules.ModuleID = Modules.ModuleID
INNER JOIN TaskPlugins
ON SessionTasks.TaskID = TaskPlugins.TaskID
INNER JOIN Plugins
ON TaskPlugins.PluginID = Plugins.PluginID
INNER JOIN TaskActions
ON SessionTasks.TaskID = TaskActions.TaskID
WHERE SessionID=@sessionIdParameter
END
