param([string] $folderPath)
$configPath = "$folderPath\SmvCmdlets.dll.config"
[System.AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
Add-Type -AssemblyName System.Configuration
Import-Module "$folderPath\SmvCmdlets.dll"
