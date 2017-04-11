param([string] $sdxRoot)
[string]$location = Get-Location
$location = $location.Replace($sdxRoot,'%SDXROOT%')
Get-ModuleOverviewByPath -ModulePath $location

