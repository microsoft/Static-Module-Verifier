# Static Module Verifier 

Static Module Verifier enables two things at it's core:

  - Building IR for a module to perform full program analysis
  - Scaling the analysis using the Azure cloud

StaticModuleVerifier supports multiple build environments, and can
produce IR based on any toolchain that you specify. Examples of such
toolchains are the [SMACK] (https://github.com/smackers/smack/)
toolchain and the [SLAM]
(https://www.microsoft.com/en-us/research/project/slam/) toolchain,
which is also used as the frontend in the [Static Driver Verifier]
(https://msdn.microsoft.com/en-us/library/windows/hardware/ff552808(v=vs.85).aspx)
project.

StaticModuleVerifier takes as input a configuration file that
specifies the series of actions to execute for building, intercepting,
and then performing analysis. Details of the configurations can be
found in the documentation folder.

# Build

## Windows
- Prerequisites:
  + Visual Studio 2015
  + Microsoft Azure SDK for VS 2015: version 2.9 .NET
  + NuGet with default configurations
- After cloning, you should be able to open the smv.sln file in your repository in VS2015
- The smv.sln solution should build in VS2015

## Linux
- Prerequisites
  + Mono complete installation
  + NuGet for Linux
- After cloning, you will need to install the following packages
```
    + nuget install Microsoft.Data.Edm WindowsAzure.ServiceBus
    + nuget install WindowsAzure.Storage
    + nuget install WindowsAzure.ServiceBus
    + nuget install WindowsAzure.WindowsAzure.ServiceRuntime
    + nuget install WindowsAzure.ServiceBus
    + nuget install AzureSDK2.2DLLs
    + nuget install WindowsAzure.Storage
    + nuget install WindowsAzure.Storage
    + nuget install WindowsAzure.Storage -Version 4.3.0
    + nuget install WindowsAzure.ServiceBus -Version 3.0.0-preview -Pre
    + nuget install WindowsAzure.ServiceBus -Version 3.0.0
```
    + Note that nuget will place them in the current working
    directory. It is suggested that you create a packages folder and
    run nuget within that folder to have the packages be centrally
    located.
- Edit the following files and make sure the Azure references point to the correct locations (hintpaths):
    + SMVActionsTable/SMVActionsTable.csproj
    + SmvCloudWorker/SmvCloudWorker.csproj
    + SmvLibrary/SmvLibrary.csproj
    + SmvSkeleton/SmvSkeleton.csproj
- Now you should be able to do xbuild at the top level to build smv.sln

# Usage
## Packaging
StaticModuleVerifier expects the following directory structure for plugins:
  - bin: all StaticModuleVerifier core binaries are placed here (including external dependencies)
  - analysisPlugins:  analysis plugins are created in an analysisPlugins folder which resides at the same level as the bin folder (where you placed all the binaries).
    + bin: anlaysis plugin specific binaries (your checker etc.) and top level script for invoking StaticModuleVerifier
    + configurations: configurations that are to be used for StaticModuleVerifier.exe

The final directory structure should look as follows:

- %SMV%
  + bin: contains all binaries, and today, the intercept.xml as well
  + analysisPlugins: contains sub folders that have analysis plugins
    * SDV: Static Driver Verifier analysis plugin
    - bin: binaries that are SDV specific. Usually also a cmd script that
      wraps calls to smv.exe
    - configurations: SMV configurations for build and analysis
      - ...: any other folders you need for your plugin

## Plugin
Coming soon...

## Interception
Coming Soon...

# SMV and the Azure Cloud
- Deploying to Azure
  + You will need a valid subscription in Azure
  + You will need to create the following:
    * A storage account
    * A service bus namespace
  + Once those are created, you can deploy the SMV Worker Role project directly from VS2015. Before that, create the following services in Azure:
    * Storage service with your name of choice (for example, MySmvCloud)
    * Service bus with the same name as the service (MySmvCloud) and a queue called smvactions
    * Cloud service with same name as the service (MySmvCloud)
  + Edit the following files for the connection strings for your newly created services:
    * SmvCloud\ServiceConfiguration.Cloud.cscfg
    * SmvLibrary\CloudConfig.xml
    * SmvCloudWorkerContent\CloudConfig.xml
- To use the cloud, you can add *executeOn="cloud"* to any action tag,  and it will execute using the SMV cloud
