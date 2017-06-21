-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ModuleDiffBetweenTwoSessions] 
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
SELECT FirstSession.ModulePath FROM
(SELECT DISTINCT RollUpTable.ModulePath FROM RollUpTable
WHERE SessionID = @FirstSession)
AS FirstSession
WHERE FirstSession.ModulePath NOT IN
(SELECT DISTINCT RollUpTable.ModulePath FROM RollUpTable
WHERE SessionID = @SecondSession)

END
