param([string] $dllFolderPath, [string] $sessionId, [string] $outputFileName)
$configPath = "$dllFolderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Import-Module "$dllFolderPath\SmvCmdlets.dll"
$results = Get-SessionOverview -SessionId $sessionId

[System.Collections.ArrayList]$collection = New-Object System.Collections.ArrayList($null)
foreach($result in $results){
    $properties = [ordered]@{}
    $properties.ModulePath = $result.ModulePath
    $properties.PluginName = $result.PluginName
    $properties.ActionSuccessCount = $result.ActionSuccessCount
    $properties.ActionFailureCount = $result.ActionFailureCount
    $properties.Command = $result.Command
    $properties.Arguments = $result.Arguments
    $collection.Add((New-Object PSObject -Property $properties)) | Out-Null
}
$collection | Export-CSV "$outputFileName.csv" -NoTypeInformation -Encoding UTF8 
start "$outputFileName.csv"