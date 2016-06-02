SMV - Static Module Verifier

# Introduction

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
