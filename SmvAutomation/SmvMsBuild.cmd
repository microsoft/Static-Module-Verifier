set $drive=%~1
set $root=%~2
set $modPath=%~3
set $cmd=%~4
set $arg=%~5
set $timestamp=%~6
set $taskId=%~7
set SDV=%~8
call "%VS140COMNTOOLS%"VsDevCmd.bat
call %$drive%
call cd "%$modPath%"
call "%$root%"\%$cmd% %$arg%>log-output-%$timestamp%-%$taskId%.txt 2>log-error-%$timestamp%-%$taskId%.txt
exit