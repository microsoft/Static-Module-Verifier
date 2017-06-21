-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ModuleOverview] 
	-- Add the parameters for the stored procedure here
	@moduleId varchar(50) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT Sessions.SessionID, 		
				COUNT(DISTINCT TaskPlugins.PluginID) AS NumberOfPlugins,
				COUNT(CASE WHEN TaskActions.Success = 0 THEN 1 END) AS ActionSuccessCount, 
				COUNT(CASE WHEN TaskActions.Success != 0 THEN 1 END) AS ActionFailureCount 
				FROM Sessions
	INNER JOIN SessionTasks
ON Sessions.SessionID = SessionTasks.SessionID
INNER JOIN TaskPlugins
ON SessionTasks.TaskID = TaskPlugins.TaskID
INNER JOIN TaskModules
ON SessionTasks.TaskID = TaskModules.TaskID
INNER JOIN TaskActions
ON TaskPlugins.TaskID = TaskActions.TaskId 
WHERE TaskModules.ModuleID = @moduleId
GROUP BY Sessions.SessionID
END
