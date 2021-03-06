using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CSScripting.CodeDom;
using CSScriptLib;

namespace csscript
{
    partial class CSExecutor
    {
        public void ProcessConfigCommand(string command)
        {
            //-config                  - lists/print current settings value
            //-config:raw              - print current config file content
            //-config:ls               - lists/print current settings value (same as simple -config)
            //-config:create           - create config file with default settings
            //-config:default          - print default settings
            //-config:get:name         - print current config file value
            //-config:set:name=value   - set current config file value
            try
            {
                if (command == "create")
                {
                    CreateDefaultConfigFile();
                }
                else if (command == "default")
                {
                    print(new Settings().ToStringRaw());
                }
                else if (command == "ls" || command == null)
                {
                    print(Settings.Load(false).ToString());
                }
                else if (command == "raw" || command == "xml")
                {
                    var currentConfig = Settings.Load(false) ?? new Settings();
                    print(currentConfig.ToStringRaw());
                }
                else if (command.StartsWith("get:"))
                {
                    string name = command.Substring(4);
                    var currentConfig = Settings.Load(false) ?? new Settings();
                    var value = currentConfig.Get(ref name);
                    print(name + ": " + value);
                }
                else if (command.StartsWith("set:"))
                {
                    // set:DefaultArguments=-ac
                    // set:roslyn
                    string name, value;

                    if (command.SameAs("set:roslyn"))
                    {
                        var asmDir = Assembly.GetExecutingAssembly().GetAssemblyDirectoryName();

                        var providerFile = ExistingFile(asmDir, "CSSRoslynProvider.dll") ??
                                           ExistingFile(asmDir, "Lib", "CSSRoslynProvider.dll");

                        if (providerFile != null)
                        {
                            name = "UseAlternativeCompiler";
                            value = providerFile;
                        }
                        else
                            throw new CLIException("Cannot locate Roslyn provider CSSRoslynProvider.dll");
                    }
                    else
                    {
                        string[] tokens = command.Substring(4).Split(new char[] { '=', ':' }, 2);
                        if (tokens.Length != 2)
                            throw new CLIException("Invalid set config property expression. Must be in name 'set:<name>=<value>' format.");

                        name = tokens[0];
                        value = tokens[1].Trim().Trim('"');
                    }

                    var currentConfig = Settings.Load(true) ?? new Settings();
                    currentConfig.Set(name, value);
                    currentConfig.Save();

                    var new_value = currentConfig.Get(ref name);
                    print("set: " + name + ": " + new_value);
                }
            }
            catch (Exception e)
            {
                throw new CLIException(e.Message); //only a message, stack info for CLI is too verbose
            }
            throw new CLIExitRequest();
        }

        /// <summary>
        /// Show CS-Script version information.
        /// </summary>
        public void ShowVersion(string arg = null)
        {
            print(HelpProvider.BuildVersionInfo(arg));
        }

        public void ShowProjectFor(string arg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Show sample C# script file.
        /// </summary>
        public void Sample(string appType, string outFile)
        {
            if (appType == null)
            {
                print?.Invoke(HelpProvider.BuildSampleHelp());
            }
            else
            {
                foreach (var sample in HelpProvider.BuildSampleCode(appType, outFile))
                {
                    if (outFile.IsNotEmpty())
                    {
                        var file = Path.GetFullPath(outFile).ChangeExtension(sample.FileExtension);

                        print?.Invoke($"Created: {file}");
                        File.WriteAllText(file, sample.Code);
                    }
                    else
                    {
                        print?.Invoke($"\nsample{sample.FileExtension}:");
                        print?.Invoke(sample.Code);
                    }
                }
            }
        }

        /// <summary>
        /// Show sample precompiler C# script file.
        /// </summary>
        public void ShowPrecompilerSample()
        {
            if (print != null)
                print(HelpProvider.BuildPrecompilerSampleCode());
        }

        /// <summary>
        /// Performs the cache operations and shows the operation output.
        /// </summary>
        /// <param name="command">The command.</param>
        public void DoCacheOperations(string command)
        {
            if (print != null)
            {
                if (command == "ls")
                    print(Cache.List());
                else if (command == "trim")
                    print(Cache.Trim());
                else if (command == "clear")
                    print(Cache.Clear());
                else
                    print("Unknown cache command." + Environment.NewLine
                        + "Expected: 'cache:ls', 'cache:trim' or 'cache:clear'" + Environment.NewLine);
            }
        }

        /// <summary>
        /// Creates the default config file in the CurrentDirectory.
        /// </summary>
        public void CreateDefaultConfigFile()
        {
            string file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "css_config.xml");
            new Settings().Save(file);
            print("The default config file has been created: " + file);
        }

        /// <summary>
        /// Prints the config file default content.
        /// </summary>
        public void PrintDefaultConfig()
        {
            print(new Settings().ToStringRaw());
        }

        public void PrintDecoratedAutoclass(string script)
        {
            string code = File.ReadAllText(script);

            var decorated = AutoclassPrecompiler.Process(code);

            print(decorated);
        }

        /// <summary>
        /// Prints Help info.
        /// </summary>
        public void ShowHelpFor(string arg)
        {
            print?.Invoke(HelpProvider.BuildCommandInterfaceHelp(arg));
        }

        /// <summary>
        /// Prints CS-Script specific C# syntax help info.
        /// </summary>
        public void ShowHelp(string helpType, params object[] context)
        {
            print?.Invoke(HelpProvider.ShowHelp(helpType, context.Where(x => x != null).ToArray()));
        }

        public void EnableWpf(string arg)
        {
            const string console_type = "\"name\": \"Microsoft.NETCore.App\"";
            const string win_type = "\"name\": \"Microsoft.WindowsDesktop.App\"";

            var configFile = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".runtimeconfig.json");

            var content = File.ReadAllText(configFile);

            if (arg == "enable" || arg == "1")
                content = content.Replace(console_type, win_type);
            else if (arg == "disabled" || arg == "0")
                content = content.Replace(win_type, console_type);

            CSExecutor.print($"WPF support is {(content.Contains(win_type) ? "enabled" : "disabled")}");

            File.WriteAllText(configFile, content);
        }
    }
}