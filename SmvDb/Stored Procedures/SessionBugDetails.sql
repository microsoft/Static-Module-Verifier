-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [SessionBugDetails]
	-- Add the parameters for the stored procedure here
	@sessionId varchar(MAX) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT  SessionTasks.SessionID, ModulePath, PluginName, Tasks.Command, Tasks.Arguments, Bugs
  FROM [dbo].[SessionTasks]
  INNER JOIN Tasks
  ON SessionTasks.TaskID = Tasks.TaskID
  INNER JOIN TaskModules
  ON Tasks.TaskID = TaskModules.TaskID
  INNER JOIN Modules
  ON TaskModules.ModuleID = Modules.ModuleID
  INNER JOIN TaskPlugins 
  ON Tasks.TaskID = TaskPlugins.TaskID
  INNER JOIN Plugins
  ON TaskPlugins.PluginID = Plugins.PluginID
  where sessionid=@sessionId AND Bugs>0
END
