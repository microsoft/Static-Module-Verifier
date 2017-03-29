param([string] $sdxRoot, [string] $configFilePath , [string] $connectionString, [string] $outputDir)
$ErrorActionPreference = 'Continue'

# Parsing the XML file to get the modules and the plugins
[xml] $XmlDocument = Get-Content -Path $sdxRoot\$configFilePath
$modulePaths = $XmlDocument.ServiceConfig.Module.path
$plugins = $XmlDocument.ServiceConfig.Plugin



$backgroundJobScript = {
    Param([string] $modPath, [string] $cmd, [string] $arg, [string] $pluginName, [string] $dirPath, [string] $sdxRoot, [string] $sessionId, [string] $connectionString)
    function CreateDirectoryIfMissing ([string] $path){
        If(!(test-path $path))
        {
            New-Item $path -ItemType Directory
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
    $taskId = [GUID]::NewGuid()

    # Making the nexessary database entries
    $query = "insert into SessionTask VALUES ('" + $sessionId + "' , '" + $taskId + "');";
    Invoke-DatabaseQuery –query $query –connectionString $connectionString

    $query = "SELECT ModuleID FROM Module WHERE ModulePath='$modPath'"
    $module = Get-DatabaseData -query $query –connectionString $connectionString
    $moduleId = $module.moduleId
    $query = "SELECT PluginID FROM Plugin WHERE PluginName='$pluginName'"
    $plugin = Get-DatabaseData -query $query –connectionString $connectionString
    $pluginId = $plugin.pluginId

    $query = "insert into TaskModule VALUES ('" + $taskId + "' , '" + $moduleId + "');";
    Invoke-DatabaseQuery –query $query –connectionString $connectionString

    $query = "insert into TaskPlugin VALUES ('" + $taskId + "' , '" + $pluginId + "');";
    Invoke-DatabaseQuery –query $query –connectionString $connectionString

    # Setting process parameters
    $ps = new-object System.Diagnostics.Process
    $ps.StartInfo.Filename = "cmd.exe"
    $ps.StartInfo.RedirectStandardInput = $True
    $ps.StartInfo.RedirectStandardOutput = $True
    $ps.StartInfo.RedirectStandardError = $True
    $ps.StartInfo.UseShellExecute = $false
    $ps.Start()
	
    # Logging the sessionId, taskId and the process output/error
    $stdout = ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
    $stderr = ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
	$arg += (" /sessionId:" + $sessionId + " /taskId:" + $taskId) 

    # Running razzle window and the corresponding analysis
    $ps.StandardInput.WriteLine("$sdxRoot\tools\razzle.cmd x86 fre no_oacr no_certcheck")
    $ps.StandardInput.WriteLine("cd $sdxRoot\$modPath")
    $ps.StandardInput.WriteLine("set usesmvsdv=true")
    $ps.StandardInput.WriteLine("rmdir /s /q sdv")
    $ps.StandardInput.WriteLine("rmdir /s /q sdv.temp")
    $ps.StandardInput.WriteLine("rmdir /s /q objfre")
    $ps.StandardInput.WriteLine("del smv* build*")
    $ps.StandardInput.WriteLine("%RazzleToolPath%\$cmd $arg")
    $ps.StandardInput.WriteLine("exit")
    $ps.WaitForExit()

    $stdout += $ps.StandardOutput.ReadToEnd()
    $stderr += $ps.StandardError.ReadToEnd()
    # Saving the log file
    $timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
    CreateDirectoryIfMissing $dirPath\SMVResults\$modPath\$pluginName\Output
    CreateDirectoryIfMissing $dirPath\SMVResults\$modPath\$pluginName\Error
    $path = "$dirPath\SMVResults\$modPath\$pluginName"
    $query = "insert into Task (TaskID, ErrorLog, Command, Arguments) VALUES ('" + $taskId + "' , '" + $path + "' , '" + $cmd + "' , '" + $arg +"');";
    Invoke-DatabaseQuery –query $query –connectionString $connectionString
    $query = "insert into Session VALUES ('" + $sessionId + "' , '" + $timestamp + "');";
    Invoke-DatabaseQuery –query $query –connectionString $connectionString
    $stdout | Out-File $path\Output\log-output-$timestamp.txt
    $stderr | Out-File $path\Error\log-error-$timestamp.txt
    
}

$sessionId = [GUID]::NewGuid()

foreach($modulePath in $modulePaths){
    foreach($plugin in $plugins){
        Start-Job -ScriptBlock $backgroundJobScript -ArgumentList $modulePath, $plugin.command, $plugin.arguments, $plugin.name, $outputDir, $sdxRoot, $sessionId, $connectionString
    }
}
