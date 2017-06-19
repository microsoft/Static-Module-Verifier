Param([string] $modPath, [string] $cmd, [string] $arg, [string] $pluginName, [string] $smvRoot, [string] $root, [string] $environmentNameRoot, [string] $sessionId, [string] $connectionString, [string] $configKey, [bool] $useDb, [bool] $useJobObject, [string] $AzCopyPath)
$ErrorActionPreference = 'Continue'
# Pre-requisite utility functions needed
function CreateDirectoryIfMissingCloud([string] $path){
	$parts = $path.Split('\')
	foreach($part in $parts){
		$pathDir += ('\'+$part)
		New-AzureStorageDirectory -Share $share -Path $pathDir
	}
}

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

# Preparing necessary variables
$ctx = New-AzureStorageContext smvtest $configKey
$share = Get-AzureStorageShare smvautomation -Context $ctx
$taskId=[GUID]::NewGuid()
$path="$sessionId\Logs\$modPath"
$drive=$root[0]+":"
$fullModPath=$modPath.Replace("%$environmentNameRoot%\", "$root\")
$sdv=(Get-Item $smvRoot).Parent.FullName

if($useDb){
	# Making the necessary database entries
	$query = "insert into SessionTasks VALUES ('" + $sessionId + "' , '" + $taskId + "');";
	Invoke-DatabaseQuery –query $query –connectionString $connectionString

	$query = "SELECT ModuleID FROM Modules WHERE ModulePath='$modPath'"
	$module = Get-DatabaseData -query $query –connectionString $connectionString
	$moduleId = $module.moduleId

	$query = "SELECT PluginID FROM Plugins WHERE PluginName='$pluginName'"
	$plugin = Get-DatabaseData -query $query –connectionString $connectionString
	$pluginId = $plugin.pluginId

	$query = "insert into TaskModules VALUES ('" + $taskId + "' , '" + $moduleId + "');";
	Invoke-DatabaseQuery –query $query –connectionString $connectionString

	$query = "insert into TaskPlugins VALUES ('" + $taskId + "' , '" + $pluginId + "');";
	Invoke-DatabaseQuery –query $query –connectionString $connectionString

	$query = "insert into Tasks (TaskID, Log, Command, Arguments) VALUES ('" + $taskId + "' , '" + $path + "' , '" + $cmd + "' , '" + $arg +"');"
	Invoke-DatabaseQuery –query $query –connectionString $connectionString
	
	# Updating the arguments for SMV if DB is to be used
	$arg += (" /db /sessionId:" + $sessionId + " /taskId:" + $taskId)
}

if($useJobObject){
	$arg += (" /jobobject");
}

# Saving the log file
$timestamp=Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
CreateDirectoryIfMissingCloud -path $path\$pluginName
CreateDirectoryIfMissingCloud -path "$path\Bugs"

# Running SMV in a process window
$ps = new-object System.Diagnostics.Process
$ps.StartInfo.Filename = "cmd.exe"
$ps.StartInfo.RedirectStandardInput = $True
$ps.StartInfo.RedirectStandardOutput = $True
$ps.StartInfo.RedirectStandardError = $True
$ps.StartInfo.UseShellExecute = $false
$ps.Start()

$be="Razzle"
if($pluginName -eq "MSBUILD"){
	$be="MsBuild"
}

$ps.StandardInput.WriteLine("$smvRoot\bin\Automation\Smv$be.cmd `"$drive`" `"$root`" `"$fullModPath`" `"$cmd`" `"$arg`" `"$timestamp`" `"$taskId`" `"$sdv`"")

$ps.WaitForExit()

# Updating the roll up table for easier post-processing
if($useDb){
	$query = "EXEC [dbo].[InsertDataToRollUpTable] @taskId = '$taskId'"
	Invoke-DatabaseQuery –query $query –connectionString $connectionString
}

# Moving output logs to file share
Set-AzureStorageFileContent -Share $share -Source $fullModPath\log-output-$timestamp-$taskId.txt -Path $path\$pluginName\log-output-$timestamp.txt
Set-AzureStorageFileContent -Share $share -Source $fullModPath\log-error-$timestamp-$taskId.txt -Path $path\$pluginName\log-error-$timestamp.txt

# Moving Bugs folders to Azure, if any
$list=dir "$fullModPath\smv\Bugs" -Directory
foreach($folder in $list){
	$newId = [GUID]::NewGuid()
	& $AzCopyPath\AzCopy.exe /Source:"$fullModPath\smv\Bugs\$folder" /Dest:https://smvtest.file.core.windows.net/smvautomation/$path/Bugs/Bug$newId /destkey:$configKey /S /Z:"$fullModPath/smv/Bugs"
}

# Deleting all rawcfgf files and then uploading SMV output zip to Azure
Get-ChildItem "$fullModPath\smv" -Include *.rawcfgf -Recurse | foreach($_) {Remove-Item $_.FullName}
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory("$fullModPath\smv", "$fullModPath\smv_$taskId.zip")
& $AzCopyPath\AzCopy.exe /Source:"$fullModPath" /Dest:https://smvtest.file.core.windows.net/smvautomation/$path /destkey:$configKey /Pattern:"smv_$taskId.zip" /Z:"$fullModPath"

#Deleting local copy of log file
Remove-Item $fullModPath\smv* -Recurse
Remove-Item $fullModPath\build*
Remove-Item $fullModPath\log-output-$timestamp-$taskId.txt
Remove-Item $fullModPath\log-error-$timestamp-$taskId.txt