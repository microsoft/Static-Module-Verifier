-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE InsertDataToRollUpTable 
	-- Add the parameters for the stored procedure here
	@taskId varchar(50) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO RollUpTable
	SELECT SessionId, TaskModules.TaskId, Modules.ModulePath, 
		Plugins.PluginName, 
		Bugs,
		COUNT(CASE WHEN TaskActions.Success = 0 THEN 1 END) AS ActionSuccessCount, 
		COUNT(CASE WHEN TaskActions.Success>0 THEN 1 END) AS ActionFailureCount,
		Tasks.Command,
		Tasks.Arguments
	FROM TaskModules
	inner join sessiontasks
	ON TaskModules.TaskID = SESSIONTASKS.TASKID
	INNER JOIN Modules
	ON TaskModules.ModuleID = Modules.ModuleID
	INNER JOIN TaskPlugins
	ON taskmodules.TaskID = TaskPlugins.TaskID
	INNER JOIN Plugins
	ON TaskPlugins.PluginID = Plugins.PluginID
	INNER JOIN TaskActions
	ON taskmodules.TaskID = TaskActions.TaskID
	INNER JOIN Tasks
	ON taskmodules.TaskID = Tasks.TaskID
	WHERE taskmodules.TaskID=@taskId
	GROUP BY SessionId, TaskModules.TaskId, Bugs, Modules.ModulePath, Plugins.PluginName, Tasks.Command, Tasks.Arguments
END
