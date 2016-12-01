@echo off

rem check that target provided
if "%1"=="" (
    echo Please specify deployment directory...
    echo Usage: deploy.cmd directory version_name
    echo.
    goto :END
)

if "%2"=="" (
    echo Please specify version name...
    echo Usage: deploy.cmd directory version_name
    echo.
    goto :END
)

rem do some basic setup for sdv product
pushd "%~dp0\..\"
set smvsrc=%CD%
set config=Debug
set targetdir=%1
popd

echo SMVSRC=%smvsrc%
echo Config=%config%
echo Target=%targetdir%

rem create directories
rmdir /s /q %targetdir%
mkdir %targetdir%\bin
mkdir %targetdir%\analysisPlugins\

rem interception
call copy /y /v %smvsrc%\smvinterceptorwrapper\bin\%config%\smvinterceptorwrapper.exe %targetdir%\bin\ || goto :END
call copy /y /v %smvsrc%\smvinterceptorwrapper\bin\%config%\smvinterceptorwrapper.pdb %targetdir%\bin\ || goto :END
call copy /y /v %smvsrc%\smvinterceptor\all-intercept.xml %targetdir%\bin\ || goto :END
call copy /y /v %smvsrc%\smvinterceptor\bin\%config%\smvinterceptor.exe %targetdir%\bin\cl.exe || goto :END
call copy /y /v %smvsrc%\smvinterceptor\bin\%config%\smvinterceptor.pdb %targetdir%\bin\cl.pdb || goto :END
call copy /y /v %smvsrc%\smvinterceptor\bin\%config%\smvinterceptor.exe %targetdir%\bin\link.exe || goto :END
call copy /y /v %smvsrc%\smvinterceptor\bin\%config%\smvinterceptor.pdb %targetdir%\bin\link.pdb || goto :END

rem basic binaries etc.
call copy /y /v %smvsrc%\staticmoduleverifier\bin\%config%\* %targetdir%\bin\ || goto :END

rem copy config schemas and config xmls
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\config.xsd %targetdir%\bin\Config.xsd || goto :END
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\cloudconfig.xsd %targetdir%\bin\CloudConfig.xsd || goto :END
call copy /y /v %smvsrc%\smvlibrary\bin\%config%\cloudconfig.xml %targetdir%\bin\CloudConfig.xml || goto :END

rem delete unneeded files
call del /q %targetdir%\bin\*codeanalysis*
call del /q %targetdir%\bin\*vshost*

rem smvversionname.txt
echo %2> %targetdir%\bin\smvversionname.txt

:END
