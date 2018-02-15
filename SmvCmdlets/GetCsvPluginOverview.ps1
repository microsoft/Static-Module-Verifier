param([string] $dllFolderPath, [string] $pluginId)
$configPath = "$dllFolderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Import-Module "$dllFolderPath\SmvCmdlets.dll"
$results = Get-PluginOverview -PluginId $pluginId

[System.Collections.ArrayList]$collection = New-Object System.Collections.ArrayList($null)
foreach($result in $results){
    $properties = [ordered]@{}
    $properties.SessionId = $result.SessionID
    $properties.NumberOfModules = $result.NumberOfModules
	$properties.ActionSuccessCount = $result.ActionSuccessCount
    $properties.ActionFailureCount = $result.ActionFailureCount
    $collection.Add((New-Object PSObject -Property $properties)) | Out-Null
}
$collection | Export-CSV "PluginOverview-$pluginId.csv" -NoTypeInformation -Encoding UTF8 
start "PluginOverview-$pluginId.csv"