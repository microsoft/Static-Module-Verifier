﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace StaticModuleVerifier.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("StaticModuleVerifier.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /Debug
        ///	To write all the output to the console.
        ///
        ////ProjectFile
        ///	Name of the project file. It can also be set in the config file.
        ///
        ////Analyze
        ///	To run analysis.
        ///
        ////Module:&lt;Name&gt;[@&lt;LastModified&gt;]
        ///	Specify a module for processing. &lt;Name&gt; is the name of the module. If there
        ///	are multiple modules with the same name, the version that was most recently
        ///	modified is used. Specify which version of the module to use by passing the
        ///	&lt;LastModified&gt; value in yyyyMMddHHmmss format. Use /SearchModules to find
        ///	al [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string HelpTextWithoutUsageString {
            get {
                return ResourceManager.GetString("HelpTextWithoutUsageString", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to smvskeleton.exe [/ProjectFile:&lt;Name&gt;] [/Analyze] [/Debug] [/Module:&lt;Name&gt;[@&lt;LastModified&gt;]] [/Rules|Check:*|Rule1,Rule2,Rule3...] [/Plugin:&lt;DLLPath&gt;] [/Log:&lt;LogFilePath&gt;] [/Config:&lt;ConfigFilePath&gt;] [/GetAvailableModules] [/SearchModules:&lt;SearchString&gt;] [/Help|/?].
        /// </summary>
        internal static string UsageString {
            get {
                return ResourceManager.GetString("UsageString", resourceCulture);
            }
        }
    }
}