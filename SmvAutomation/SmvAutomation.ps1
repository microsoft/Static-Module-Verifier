param([string] $automationConfigFilePath , [string] $configFilePath, [string] $AzCopyPath, [string] $maxConcurrentJobs)

# PREPARING PREREQUISITES
$ErrorActionPreference = 'Continue'
$scriptPath=$PSScriptRoot

# Setting max number of parallel jobs to either input or number of cores on system
$numberOfCores = Get-WmiObject -class Win32_processor | Select-Object -ExpandProperty NumberOfCores
if(!$maxConcurrentJobs){
    $maxConcurrentJobs = $numberOfCores
}

# Utility functions to work with the database
function Get-DatabaseData {
	[CmdletBinding()]
	param (
		[string]$connectionString,
		[string]$query
	)
	$connection = New-Object System.Data.SqlClient.SqlConnection
	$connection.ConnectionString = $connectionString
	$command = $connection.CreateCommand()
	$command.CommandText = $query
	$adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
	$dataset = New-Object System.Data.DataSet
	$adapter.Fill($dataset)
	$dataset.Tables[0]
}
function Invoke-DatabaseQuery {
	[CmdletBinding()]
	param (
		[string]$connectionString,
		[string]$query
	)
	$connection = New-Object System.Data.SqlClient.SqlConnection
	$connection.ConnectionString = $connectionString
	$command = $connection.CreateCommand()
	$command.CommandText = $query
	$connection.Open()
	$command.ExecuteNonQuery()
	$connection.close()
}

# REAL PROCESSING STARTS HERE

# Extracting passwords and config information from the respective files
[xml] $XmlDocument = Get-Content -Path $automationConfigFilePath
[xml] $configDocument = Get-Content -Path $configFilePath
$connectionString = $configDocument.Passwords.DbConnectionString.connectionString
$key = $configDocument.Passwords.SmvTestKey.key

# Extracting details about the unique modules and plugins
$root = $XmlDocument.ServiceConfig.Root.value
$environmentNameRoot = $XmlDocument.ServiceConfig.Root.environmentName
$smvRoot = $XmlDocument.ServiceConfig.SmvRoot.value
if(!$smvRoot -Or !$root -Or !$environmentNameRoot){
    echo "Roots have not been defined properly in the configuration"
    exit
}
$sdv = (Get-Item $smvRoot).Parent.FullName
$modulePaths = $XmlDocument.ServiceConfig.Modules.Module.path
if($XmlDocument.ServiceConfig.ModulesDirectory){
    $folderPath = $XmlDocument.ServiceConfig.ModulesDirectory.ModuleDirectory.path.Replace("%$environmentNameRoot%\", "$root\")
    $moduleDefinitionFile = $XmlDocument.ServiceConfig.ModulesDirectory.ModuleDirectory.moduleDefinitionFile
    $folders = Get-ChildItem -Path $folderPath -Filter $moduleDefinitionFile -Recurse
    $modulePaths += $folders.Directory.FullName
}
$modulePaths = $modulePaths.Replace("$root\", "%$environmentNameRoot%\")
$modulePaths = $modulePaths | select -Unique
$count = $modulePaths.Count
echo "Number of modules: $count"
$modulePaths = $modulePaths.Trim()

$plugins = $XmlDocument.ServiceConfig.Plugins.Plugin
# Setting up parameters for SMV
[bool]$useDb = [System.Convert]::ToBoolean($XmlDocument.ServiceConfig.Plugins.useDb)
[bool]$useJobObject = [System.Convert]::ToBoolean($XmlDocument.ServiceConfig.Plugins.useJobObject)
if(!$useDb){
    $useDb = $false
}

if($useDb){
    # Adding entry in Plugins table if it is not already present
    foreach($plugin in $plugins){
        $pluginName = $plugin.name
        $query = "SELECT PluginID FROM Plugins WHERE PluginName='$pluginName'"
        $plugin = Get-DatabaseData -query $query –connectionString $connectionString
        if(!$plugin){
            $pluginId = [GUID]::NewGuid()
            $query = "insert into Plugins VALUES('" + $pluginId + "' , '" + $pluginName + "');";
            Invoke-DatabaseQuery –query $query –connectionString $connectionString 
        }
    }

    # Adding entry in Modules table if it is not already present
    foreach($modulePath in $modulePaths){
        $query = "SELECT ModuleID FROM Modules WHERE ModulePath='$modulePath'"
        $module = Get-DatabaseData -query $query –connectionString $connectionString
        if(!$module){
            $moduleId = [GUID]::NewGuid()
            $query = "insert into Modules VALUES('" + $moduleId + "' , '" + $modulePath + "');";
            Invoke-DatabaseQuery –query $query –connectionString $connectionString    
        }
    }
}

$sessionId = [GUID]::NewGuid()
echo "Session ID: $sessionId"
$startTimestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
echo "Using DB: $useDb"
# Initiating the parallel jobs
foreach($plugin in $plugins){
    foreach($modulePath in $modulePaths){
        $check = $false
        while($check -eq $false){
            if((Get-Job -State 'Running').Count -lt $maxConcurrentJobs){
                Get-Job
                Start-Job -FilePath "$scriptPath\BackgroundJobScript.ps1" -ArgumentList $modulePath, $plugin.command, $plugin.arguments, $plugin.name, $smvRoot, $root, $environmentNameRoot, $sessionId, $connectionString, $key, $useDb, $useJobObject, $AzCopyPath
                $check = $true
            }
        }
    }
    Get-Job | Wait-Job
}

# Updating Sessions table in the database
if($useDb){
    $endTimeStamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
    $user = $env:USERNAME
    $query = "insert into Sessions VALUES ('" + $sessionId + "' , '" + $startTimestamp + "' , '" + $endTimestamp + "' , '" + $user + "');"
    Invoke-DatabaseQuery –query $query –connectionString $connectionString
}

# Copying SMV input folder to fileshare
& $AzCopyPath\AzCopy.exe /Source:"$smvRoot" /Dest:https://smvtest.file.core.windows.net/smvautomation/$sessionId/SMV /destkey:$key /S /Z:"$sdv"