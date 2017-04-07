param([string] $sdxRoot)
[string]$location = Get-Location
$location = $location.Replace($sdxRoot,'')
Get-ModuleOverviewByPath -ModulePath $location

