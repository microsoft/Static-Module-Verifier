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

# Usage
## Packaging
StaticModuleVerifier expects the following directory structure for plugins:
  - bin: all StaticModuleVerifier core binaries are placed here (including external dependencies)
  - analysisPlugins:  analysis plugins are created in an analysisPlugins folder which resides at the same level as the bin folder (where you placed all the binaries).
    + bin: anlaysis plugin specific binaries (your checker etc.) and top level script for invoking StaticModuleVerifier
    + configurations: configurations that are to be used for StaticModuleVerifier.exe

The final directory structure should look as follows:

- %SMV% (top level folder where you created your deployment)
  + bin: contains all binaries, and today, the intercept.xml as well
  + analysisPlugins: contains sub folders that have analysis plugins
    * SDV: Static Driver Verifier analysis plugin
    - bin: binaries that are SDV specific. Usually also a cmd script that
      wraps calls to `smv.exe`
    - configurations: StaticModuleVerifier configurations for build and analysis
    - other folders can be created as necessary

## Plugin
Coming soon...

## Interception
Coming Soon...

# SMV and the Azure Cloud
- Deploying to Azure
  + You will need a valid subscription for Azure
  + You will need to create the following:
    * Storage service with your name of choice (for example, `MySmvCloud`)
      - Create the following containers: `smvversions, smvactions, smvresults`
    * Service bus with the same name as the service (`MySmvCloud`) and a queue called smvactions
    * Cloud service with same name as the service (`MySmvCloud`)
    * Queue named `smvactions`
  + Edit the following files for the connection strings for your newly created services:
    * SmvCloud\ServiceConfiguration.Cloud.cscfg
    * SmvLibrary\CloudConfig.xml
    * SmvCloudWorkerContent\CloudConfig.xml
- After creating your deployments
- To use the cloud, you can add `executeOn="cloud"` to any analysis action node in your configurations,  and it will execute using the SMV cloud

# Miscellaneous tools
- Azure Storage Explorer for viewing storage accounts and their contents
