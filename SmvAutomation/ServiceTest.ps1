param([string] $sdxRoot, [string] $configFilePath)
$ErrorActionPreference = 'Continue'

# Parsing the XML file to get the modules and the plugins
[xml] $XmlDocument = Get-Content -Path $sdxRoot\$configFilePath
$modulePaths = $XmlDocument.ServiceConfig.Module.path
$plugins = $XmlDocument.ServiceConfig.Plugin
$directoryPath = Get-Location

$backgroundJobScript = {
    Param([string] $modPath, [string] $cmd, [string] $arg, [string] $pluginName, [string] $dirPath, [string] $sdxRoot, [string] $sessionId)
    function CreateDirectoryIfMissing ([string] $path){
        If(!(test-path $path))
        {
            New-Item $path -ItemType Directory
        }
    }

    # Setting process parameters
	$ps = new-object System.Diagnostics.Process
    $ps.StartInfo.Filename = "cmd.exe"
    $ps.StartInfo.RedirectStandardInput = $True
    $ps.StartInfo.RedirectStandardOutput = $True
    $ps.StartInfo.RedirectStandardError = $True
    $ps.StartInfo.UseShellExecute = $false
    $ps.Start()

    # Running razzle window and the corresponding analysis
    $ps.StandardInput.WriteLine("$sdxRoot\tools\razzle.cmd x86 fre no_oacr no_certcheck")
    $ps.StandardInput.WriteLine("cd $sdxRoot\$modPath")
    $ps.StandardInput.WriteLine("rmdir /s /q sdv")
    $ps.StandardInput.WriteLine("rmdir /s /q objfre")
    $ps.StandardInput.WriteLine("del smv* build*")
    $ps.StandardInput.WriteLine("%RazzleToolPath%\$cmd $arg")
    $ps.StandardInput.WriteLine("exit")
    $ps.WaitForExit()

    # Logging the sessionId, taskId and the process output/error
    $taskId = [GUID]::NewGuid()
    $stdout = ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
    $stdout += $ps.StandardOutput.ReadToEnd()
    $stderr = ("SessionID: " + $sessionId + "`r`nTaskID : " + $taskId +"`r`n`r`n")
    $stderr += $ps.StandardError.ReadToEnd()

    # Saving the log file
    $timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss" 
    CreateDirectoryIfMissing $dirPath\SMVResults\$modPath\$pluginName\Output
    CreateDirectoryIfMissing $dirPath\SMVResults\$modPath\$pluginName\Error
    $path = "$dirPath\SMVResults\$modPath\$pluginName"
    $stdout | Out-File $path\Output\log-output-$timestamp.txt
    $stderr | Out-File $path\Error\log-error-$timestamp.txt
}

$sessionId = [GUID]::NewGuid()
foreach($modulePath in $modulePaths){
    foreach($plugin in $plugins){
        Start-Job -ScriptBlock $backgroundJobScript -ArgumentList $modulePath, $plugin.command, $plugin.arguments, $plugin.name, $directoryPath, $sdxRoot, $sessionId
    }
}