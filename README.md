SMV - Static Module Verifier
============================

# Introduction
--------------

This is the core project that enables users to build and verify their
modules using SMV. Currently SMV supports multiple build environments,
and produces IR that is based on the SLAM toolchain. Currently, SMV is
capable of producing IR in the LI format for C/C++ projects that are
using the following build environments: Razzle, MSBuild, CoreXT, and
Make. SMV can be easily configured to use any other toolchain in mind
for producing different IR.

# Building

- Prerequisites:
  + Visual Studio 2015
  + Microsoft Azure SDK for VS 2015: version 2.9 .NET
  + NuGet with default configurations
- Please clone the SMV sources from https://staticmoduleverifier.visualstudio.com/SMV/_git/SMV
- After cloning, you should be able to open the smv.sln file in your repository in VS2015
- The solution should build as is for all configurations and platforms

# Installing

Installing SMV requires that the relevant binaries are gathered in a
single folder. For this purpose, the deployment folder contains
scripts to create such deployments. After gathering the relevant
binaries, SMV expects that analysis plugins are created in an
analysisPlugins folder which resides at the same level as the bin
folder (where you placed all the binaries).

Interception can be performed based on an intercept.xml file that
needs to be present in the same folder as the interceptor
binaries. For example, if you are intercepting cl.exe, you need to
copy interceptor.exe to cl.exe and have an intercept.xml file in the
same folder.



# SMV and the Azure Cloud

- Deploying to Azure
  + You will need a valid subscription in Azure
  + You will need to create the following:
    * A storage account
    * A service bus namespace
  + Once those are created, you can deploy the SMV Worker Role project directly from VS2015
  