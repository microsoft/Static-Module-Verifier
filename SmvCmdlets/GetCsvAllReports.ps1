param([string] $scriptPath, [string] $dllFolderPath, [string] $sessionId)
& $scriptPath\GetCsvSessionOverview.ps1 -dllFolderPath $dllFolderPath -sessionId $sessionId
& $scriptPath\GetCsvSessionActionFailureSummary.ps1 -dllFolderPath $dllFolderPath -sessionId $sessionId
& $scriptPath\GetCsvSessionActionFailureDetails.ps1 -dllFolderPath $dllFolderPath -sessionId $sessionId
& $scriptPath\GetCsvSessionBugDetails.ps1 -dllFolderPath $dllFolderPath -sessionId $sessionId
