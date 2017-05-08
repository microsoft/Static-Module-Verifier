param([string] $sdxRoot, [string] $automationConfigFilePath , [string] $configFilePath, [string] $AzCopyPath, [string] $maxConcurrentJobs)
$ErrorActionPreference = 'Continue'

$numberOfCores = Get-WmiObject -class Win32_processor | Select-Object -ExpandProperty NumberOfCores
if(!$maxConcurrentJobs){
    $maxConcurrentJobs = $numberOfCores
}
$sdxRoot = $sdxRoot.Trim()
$sdxRoot
# Parsing the XML file to get the modules and the plugins
[xml] $XmlDocument = Get-Content -Path $automationConfigFilePath
[xml] $configDocument = Get-Content -Path $configFilePath
$connectionString = $configDocument.Passwords.DbConnectionString.connectionString
$key = $configDocument.Passwords.SmvTestKey.key
$modulePaths = $XmlDocument.ServiceConfig.Modules.Module.path
# For handling module directories
if($XmlDocument.ServiceConfig.ModulesDirectory){
    $folderPath = $XmlDocument.ServiceConfig.ModulesDirectory.ModuleDirectory.path.Replace("%SDXROOT%\", "$sdxRoot\")
    $moduleDefinitionFile = $XmlDocument.ServiceConfig.ModulesDirectory.ModuleDirectory.moduleDefinitionFile
    $folders = Get-ChildItem -Path $folderPath -Filter $moduleDefinitionFile -Recurse
    $modulePaths += $folders.Directory.FullName
}
$modulePaths = $modulePaths.Replace("$sdxRoot\", "%SDXROOT%\")
$modulePaths = $modulePaths | select -Unique

$plugins = $XmlDocument.ServiceConfig.Plugins.Plugin
[bool]$useDb = [System.Convert]::ToBoolean($XmlDocument.ServiceConfig.Plugins.useDb)
[bool]$useJobObject = [System.Convert]::ToBoolean($XmlDocument.ServiceConfig.Plugins.useJobObject)

if(!$useDb){
    $useDb = $false
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


$backgroundJobScript = {
    Param([string] $modPath, [string] $cmd, [string] $arg, [string] $pluginName, [string] $sdxRoot, [string] $sessionId, [string] $connectionString, [string] $configKey, [bool] $useDb, [bool] $useJobObject)

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


    $ctx = New-AzureStorageContext smvtest $configKey
    $share = Get-AzureStorageShare smvautomation -Context $ctx
    $taskId = [GUID]::NewGuid()
    $path = "$sessionId\Logs\$modPath\$pluginName"
    if($useDb){
        # Making the nexessary database entries
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
    }

	
    # Saving the log file
    $timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
    CreateDirectoryIfMissingCloud -path $path


    # Setting process parameters
    $ps = new-object System.Diagnostics.Process
    $ps.StartInfo.Filename = "cmd.exe"
    $ps.StartInfo.RedirectStandardInput = $True
    $ps.StartInfo.RedirectStandardOutput = $True
    $ps.StartInfo.RedirectStandardError = $True
    $ps.StartInfo.UseShellExecute = $false
    $ps.Start()
    $stdout = ""
    $stderr = ""
    if($useDb){
        # Logging the sessionId, taskId and the process output/error
        $stdout += ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
        $stderr += ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
	    $arg += (" /db /sessionId:" + $sessionId + " /taskId:" + $taskId)
    } 
	if($useJobObject){
		$arg += (" /jobobject");
	}
	$drive = $sdxRoot[0]+":"
    # Running razzle window and the corresponding analysis
    $ps.StandardInput.WriteLine($drive)
    $ps.StandardInput.WriteLine("$sdxRoot\tools\razzle.cmd x86 fre no_oacr no_certcheck")
    $ps.StandardInput.WriteLine("cd $modPath")
    $ps.StandardInput.WriteLine("set usesmvsdv=true")
    $ps.StandardInput.WriteLine("rmdir /s /q sdv")
    $ps.StandardInput.WriteLine("rmdir /s /q smv")
    $ps.StandardInput.WriteLine("rmdir /s /q sdv.temp")
    $ps.StandardInput.WriteLine("rmdir /s /q objfre")
    $ps.StandardInput.WriteLine("del smv* build*")
    $ps.StandardInput.WriteLine("%RazzleToolPath%\$cmd $arg")
    $ps.StandardInput.WriteLine("exit")
    $ps.WaitForExit()

    $stdout += $ps.StandardOutput.ReadToEnd()
    $stderr += $ps.StandardError.ReadToEnd()
    
    # Storing output in file
    $stdout | Out-File log-output-$timestamp-$taskId.txt
    $stderr | Out-File log-error-$timestamp-$taskId.txt
    

    # Moving output to file share
    Set-AzureStorageFileContent -Share $share -Source log-output-$timestamp-$taskId.txt -Path $path\log-output-$timestamp.txt
    Set-AzureStorageFileContent -Share $share -Source log-error-$timestamp-$taskId.txt -Path $path\log-error-$timestamp.txt
    
    #Deleting local copy of file
    Remove-Item log-output-$timestamp-$taskId.txt
    Remove-Item log-error-$timestamp-$taskId.txt

    
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
$useDb
$sessionId = [GUID]::NewGuid()
echo "Session ID: $sessionId"
$startTimestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
foreach($plugin in $plugins){
    foreach($modulePath in $modulePaths){
        $check = $false
        while($check -eq $false){
            if((Get-Job -State 'Running').Count -lt $maxConcurrentJobs){
                Start-Job -ScriptBlock $backgroundJobScript -ArgumentList $modulePath, $plugin.command, $plugin.arguments, $plugin.name, $sdxRoot, $sessionId, $connectionString, $key, $useDb, $useJobObject
                $check = $true
            }
        }
    }
    Get-Job | Wait-Job
}

if($useDb){
    $endTimeStamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
    $user = $env:USERNAME
    $query = "insert into Sessions VALUES ('" + $sessionId + "' , '" + $startTimestamp + "' , '" + $endTimestamp + "' , '" + $user + "');"
    Invoke-DatabaseQuery –query $query –connectionString $connectionString
}
# Copying SMV folder to fileshare
& $AzCopyPath\AzCopy.exe /Source:"$sdxRoot\tools\analysis\x86\sdv\smv" /Dest:https://smvtest.file.core.windows.net/smvautomation/$sessionId/SMV /destkey:$key /S
