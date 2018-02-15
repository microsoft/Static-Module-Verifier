-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ActionDiffBetweenTwoSessions]
	-- Add the parameters for the stored procedure here
		@FirstSession varchar(50) = NULL, 
	@SecondSession varchar(50) = NULL
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
SELECT FirstSession.ModulePath, FirstSession.PluginName, FirstSession.Bugs, FirstSession.ActionSuccessCount, FirstSession.ActionFailureCount
FROM
(SELECT ModulePath, PluginName, Bugs, ActionSuccessCount, ActionFailureCount
FROM RollUpTable
WHERE SessionId=@firstSession)
AS FirstSession
EXCEPT
(SELECT ModulePath, PluginName, Bugs, ActionSuccessCount, ActionFailureCount
FROM RollUpTable
WHERE SessionId=@secondSession)


END
