set $drive=%~1
set $sdxRoot=%~2
set $modPath=%~3
set $cmd=%~4
set $arg=%~5
set $timestamp=%~6
set $taskId=%~7
call %$drive%
call %$sdxRoot%\tools\razzle.cmd x86 fre no_oacr no_certcheck
call cd %$modPath%
call set usesmvsdv=true
call rmdir /s /q sdv
call rmdir /s /q smv
call rmdir /s /q sdv.temp
call rmdir /s /q objfre
call del smv* build*
call %RazzleToolPath%\%$cmd% %$arg%>log-output-%$timestamp%-%$taskId%.txt 2>log-error-%$timestamp%-%$taskId%.txt
exit