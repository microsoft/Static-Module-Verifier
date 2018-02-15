-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[PluginDiffBetweenTwoSessions] 
	-- Add the parameters for the stored procedure here
		@FirstSession varchar(50) = NULL, 
	@SecondSession varchar(50) = NULL
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	/****** Script for SelectTopNRows command from SSMS  ******/
SELECT FirstSession.PluginName FROM
(SELECT DISTINCT Plugins.PluginName FROM SessionTasks
INNER JOIN TaskPlugins
ON SessionTasks.TaskID = TaskPlugins.TaskId
INNER JOIN Plugins
ON TaskPlugins.PluginID = Plugins.PluginID
WHERE SessionID = @FirstSession)
AS FirstSession
WHERE FirstSession.PluginName NOT IN
(SELECT DISTINCT Plugins.PluginName FROM SessionTasks
INNER JOIN TaskPlugins
ON SessionTasks.TaskID = TaskPlugins.TaskId
INNER JOIN Plugins
ON TaskPlugins.PluginID = Plugins.PluginID
WHERE SessionID = @SecondSession)

END
