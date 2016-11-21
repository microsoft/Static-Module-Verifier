@echo off
setlocal

REM
REM Ensure arguments are present
REM
IF "%1" == "" (
   ECHO.
   ECHO Error: Please specify the BPL file to analyze.
   ECHO.
   GOTO EXIT
)

SET FILE=%1

REM
REM Check that SMV environment variable is set correctly
REM 
PUSHD "%~dp0\..\..\..\"
SET SMV=%CD%
SET AVN=%SMV%\analysisPlugins\AVN
POPD

REM ECHO SMV: %SMV%
REM ECHO AVN: %AVN%

REM
REM Main work for SMV given conifguration etc.
REM
IF "%2" == "" (
   "%smv%\bin\smv" /plugin:"%smv%\analysisplugins\avn\bin\fastavn.dll" /config:"%avn%\configurations\split.xml" /analyze
   GOTO EXIT
)

IF "%2" == "/cloud" (
   ECHO [INFO] Running using cloud...
   "%smv%\bin\smv" /plugin:"%smv%\analysisplugins\avn\bin\fastavn.dll" /config:"%avn%\configurations\split-cloud.xml" /analyze /cloud 
   GOTO EXIT
)

:EXIT
