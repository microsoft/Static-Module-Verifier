param([string] $dllFolderPath, [string] $sessionId)
$configPath = "$dllFolderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Import-Module "$dllFolderPath\SmvCmdlets.dll"
$results = Get-SessionBugDetails -SessionId $sessionId

[System.Collections.ArrayList]$collection = New-Object System.Collections.ArrayList($null)
foreach($result in $results){
    $properties = [ordered]@{}
    $properties.SessionID = $result.SessionID
    $properties.ModulePath = $result.ModulePath
    $properties.PluginName = $result.PluginName
	$properties.Command = $result.Command
    $properties.Arguments = $result.Arguments
    $properties.BugCount = $result.Bugs
    $collection.Add((New-Object PSObject -Property $properties)) | Out-Null
}
$collection | Export-CSV "SessionBugDetails-$sessionId.csv" -NoTypeInformation -Encoding UTF8 
start "SessionBugDetails-$sessionId.csv"
