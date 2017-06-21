-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[PluginOverview]
	-- Add the parameters for the stored procedure here
	@pluginId varchar(50) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT SessionTasks.SessionID, 		
				COUNT(DISTINCT TaskModules.ModuleID) AS NumberOfModules,
				COUNT(CASE WHEN TaskActions.Success = 0 THEN 1 END) AS ActionSuccessCount, 
				COUNT(CASE WHEN TaskActions.Success != 0 THEN 1 END) AS ActionFailureCount 
				FROM SessionTasks
	INNER JOIN TaskPlugins
	ON SessionTasks.TaskID = TaskPlugins.TaskID
	INNER JOIN TaskModules
	ON SessionTasks.TaskID = TaskModules.TaskID
	INNER JOIN TaskActions
	ON TaskPlugins.TaskID = TaskActions.TaskId 
	WHERE TaskPlugins.PluginID = @pluginId
	GROUP BY SessionTasks.SessionID
END
