param([string] $completePathCsvFile, [string] $columnName, [string] $dllFolderPath, [string] $sessionId, [string] $AzCopyPath)

$modulePaths = Import-Csv $completePathCsvFile | % {$_.$columnName}
& $dllFolderPath\preparePowershellWindow.ps1 -folderPath $dllFolderPath
$modulePaths
if(Test-Path Bugs-$sessionId){
	Remove-Item Bugs-$sessionId -Recurse
}
New-Item Bugs-$sessionId -ItemType Directory
cd Bugs-$sessionId
$count = 1
foreach($modulePath in $modulePaths){
	$modulePath
    New-Item Bugs_$count -ItemType Directory
    cd Bugs_$count
	$modulePath | Out-File classification.txt
    Get-BugsFolderWithoutSdxRoot -SessionId $sessionId -ModulePath $modulePath -AzCopyPath $AzCopyPath
    cd ..
    $count++
} 
cd ..
