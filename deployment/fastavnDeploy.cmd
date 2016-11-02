@echo off

rem Usage: smvDeploy.cmd configuration directory_name [version_name]

rem Check if the right parameters were passed.
if "%1"=="" (
    echo Usage: fastavnDeploy.cmd configuration directory_name [version_name]
    goto :END
)
if "%2"=="" (
    echo Usage: fastavnDeploy.cmd configuration directory_name [version_name]
    goto :END
)

if "%3"=="" (
   echo Usage: fastavnDeploy.cmd configuration directory_name [version_name]
   goto :END
)

set config=%1
set targetdir=%2
set version_name=%3
pushd "%~dp0\..\"
set smvsrc=%CD%
popd

rem If the target directory does not exist, create it.
if not exist %targetdir% (
    echo Creating directory: %targetdir%
    mkdir %targetdir%
)
if not exist %targetdir%\bin (
    echo Creating directory: %targetdir%\bin
    mkdir %targetdir%\bin
)
if not exist %targetdir%\analysisPlugins (
    echo Creating directory: %targetdir%\analysisPlugins
    mkdir %targetdir%\analysisPlugins
)
if not exist %targetdir%\analysisPlugins\avn (
    echo Creating directory: %targetdir%\analysisPlugins\avn\bin
    mkdir %targetdir%\analysisPlugins\avn\bin
    echo Creating directory: %targetdir%\analysisPlugins\avn\configruations
    mkdir %targetdir%\analysisPlugins\avn\configurations
)

rem Copy files over to the target directory.
echo Copying files to %targetdir%

rem basic binaries etc. 
call copy /y /v %smvsrc%\smvskeleton\bin\%config%\* %targetdir%\bin\ || goto :END

rem setup smv.exe 
call copy /y /v %smvsrc%\smvskeleton\bin\%config%\smv.exe %targetdir%\bin\smv.exe || goto :END
call copy /y /v %smvsrc%\smvskeleton\bin\%config%\smv.exe.config %targetdir%\bin\smv.exe.config || goto :END
call copy /y /v %smvsrc%\smvskeleton\bin\%config%\smv.pdb %targetdir%\bin\smv.pdb || goto :END

rem NOTE: We only copy the CloudConfig.xml file for the SE Asia deployment at the moment. This will have to change if we add
rem more deployments in the future.
rem copy config schemas and config xmls
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\config.xsd %targetdir%\bin\Config.xsd || goto :END
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\cloudconfig.xsd %targetdir%\bin\CloudConfig.xsd || goto :END
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\cloudconfig.xml %targetdir%\bin\CloudConfig.xml || goto :END

rem fastavn specific
call copy /y /v %smvsrc%\smvfastavn\bin\%config%\fastavn.* %targetdir%\analysisplugins\avn\bin\ || goto :END
call copy /y /v %smvsrc%\smvfastavn\configurations\*.xml %targetdir%\analysisplugins\avn\configurations\ || goto :END
call copy /y /v %smvsrc%\smvfastavn\avn.cmd %targetdir%\analysisplugins\avn\bin\ || goto :END

(echo %version_name%)> %targetdir%\bin\SmvVersionName.txt

rem print message
echo.
echo Please copy your FastAVN engine into %targetdir%\analysisplugins\avn\bin\engine folder.
echo Done.
echo.

:END
