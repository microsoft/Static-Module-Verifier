param([string] $dllFolderPath, [string] $sessionId)
$configPath = "$dllFolderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Import-Module "$dllFolderPath\SmvCmdlets.dll"
$results = Get-SessionActionFailureDetails -SessionId $sessionId

[System.Collections.ArrayList]$collection = New-Object System.Collections.ArrayList($null)
foreach($result in $results){
    $properties = [ordered]@{}
    $properties.WorkingDirectory = $result.WorkingDirectory
    $properties.ActionName = $result.ActionName
    $properties.ExitCode = $result.Success
    $collection.Add((New-Object PSObject -Property $properties)) | Out-Null
}
$collection | Export-CSV "SessionActionFailureDetails-$sessionId.csv" -NoTypeInformation -Encoding UTF8 
start "SessionActionFailureDetails-$sessionId.csv"