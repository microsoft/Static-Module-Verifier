param([string] $sdxRoot, [string] $sessionId, [string] $AzCopyPath)
$location = Get-Location
Get-BugsFolder -SessionId $sessionId -ModulePath $location.Path -SdxRoot $sdxRoot -AzCopyPath $AzCopyPath
