-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ModuleOverviewByPath] 
	-- Add the parameters for the stored procedure here
	@modulePath varchar(MAX) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
		SELECT Sessions.SessionID, 		
				Plugins.PluginName,
				COUNT(CASE WHEN TaskActions.Success = 0 THEN 1 END) AS ActionSuccessCount, 
				COUNT(CASE WHEN TaskActions.Success != 0 THEN 1 END) AS ActionFailureCount 
				FROM Sessions
		INNER JOIN SessionTasks
	ON Sessions.SessionID = SessionTasks.SessionID
	INNER JOIN TaskPlugins
	ON SessionTasks.TaskID = TaskPlugins.TaskID
	INNER JOIN Plugins
	ON TaskPlugins.PluginID = Plugins.PluginID
	INNER JOIN TaskModules
	ON SessionTasks.TaskID = TaskModules.TaskID
	INNER JOIN TaskActions
	ON TaskPlugins.TaskID = TaskActions.TaskId 
	INNER JOIN Modules
	ON TaskModules.ModuleID = Modules.ModuleID
	WHERE Modules.ModulePath = @modulePath
	GROUP BY Sessions.SessionID, Plugins.PluginName
end
