param([string] $dllFolderPath, [string] $sessionId)
$configPath = "$dllFolderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Import-Module "$dllFolderPath\SmvCmdlets.dll"
$results = Get-SessionActionFailureSummary -SessionId $sessionId

[System.Collections.ArrayList]$collection = New-Object System.Collections.ArrayList($null)
foreach($result in $results){
    $properties = [ordered]@{}
    $properties.ActionName = $result.ActionName
    $properties.FailureCount = $result.Count
    $collection.Add((New-Object PSObject -Property $properties)) | Out-Null
}
$collection | Export-CSV "SessionActionFailureSummary-$sessionId.csv" -NoTypeInformation -Encoding UTF8 
start "SessionActionFailureSummary-$sessionId.csv"