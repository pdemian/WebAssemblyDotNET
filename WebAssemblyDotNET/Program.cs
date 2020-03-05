using System;
using System.IO;
using System.Diagnostics;
using WebAssemblyDotNET;
using WebAssemblyDotNET.Sections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace WebAssemblyCS
{
    class Program
    {
        class Options
        {
            public readonly bool Help;
            public readonly bool Version;
            public readonly bool Debug;
            public readonly bool Strict;
            public readonly bool Execute;
            public readonly uint? Run;
            public readonly List<string> Filenames = new List<string>();

            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public Options(string[] args)
            {
                if (args.Length == 0) Help = true;

                for(int i = 0; i < args.Length; i++)
                {
                    string current_arg = args[i].ToUpper();

                    if(current_arg.StartsWith("-") || current_arg.StartsWith("/"))
                    {
                        current_arg = current_arg.Substring(1);
                        switch (current_arg)
                        {
                            case "H":
                            case "?":
                                if (Help) Warnings.Add("Duplicate help argument.");
                                Help = true;
                                break;
                            case "V":
                                if (Version) Warnings.Add("Duplicate version argument.");
                                Version = true;
                                break;
                            case "D":
                                if (Debug) Warnings.Add("Duplicate debug argument.");
                                Debug = true;
                                break;
                            case "S":
                                if (Strict) Warnings.Add("Duplicate strict argument.");
                                Strict = true;
                                break;
                            case "E":
                                if (Execute) Warnings.Add("Duplicate execute argument.");
                                Execute = true;
                                break;
                            case "R":
                                if (Run != null) Warnings.Add("Duplicate run argument.");
                                try
                                {
                                    Run = uint.Parse(args[++i]);
                                }
                                catch(Exception)
                                {
                                    Errors.Add("Invalid run argument.");
                                }
                                break;
                            default:
                                Errors.Add($"Unknown argument {args[i]}.");
                                break;
                        }
                    }
                    else if(current_arg.StartsWith("--"))
                    {
                        current_arg = current_arg.Substring(2);
                        switch (current_arg)
                        {
                            case "HELP":
                                if (Help) Warnings.Add("Duplicate help argument.");
                                Help = true;
                                break;
                            case "VERSION":
                                if (Version) Warnings.Add("Duplicate version argument.");
                                Version = true;
                                break;
                            case "DEBUG":
                                if (Debug) Warnings.Add("Duplicate debug argument.");
                                Debug = true;
                                break;
                            case "STRICT":
                                if (Strict) Warnings.Add("Duplicate strict argument.");
                                Strict = true;
                                break;
                            case "EXECUTE":
                                if (Execute) Warnings.Add("Duplicate execute argument.");
                                Execute = true;
                                break;
                            case "RUN":
                                if (Run != null) Warnings.Add("Duplicate run argument.");
                                try
                                {
                                    Run = uint.Parse(args[++i]);
                                }
                                catch (Exception)
                                {
                                    Errors.Add("Invalid run argument.");
                                }
                                break;
                            default:
                                Errors.Add($"Unknown argument {args[i]}.");
                                break;
                        }
                    }
                    else
                    {
                        if (!File.Exists(args[i]))
                        {
                            Errors.Add($"File {args[i]} does not exist or cannot be accessed.");
                        }
                        else
                        {
                            Filenames.Add(args[i]);
                        }
                    }
                }

                if (Run != null && Execute) Warnings.Add("Run will overwrite the start function specified by execute.");

                if (Filenames.Count == 0) Errors.Add("No files to process.");

                if (Errors.Count > 0) Help = true;
            }

            public string GetHelp()
            {
                StringBuilder sb = new StringBuilder();

                if (Errors.Count > 0)
                {
                    foreach (var err in Errors)
                    {
                        sb.AppendLine(err);
                    }

                    sb.AppendLine($"Try '{GetName()} --help' for more information.");
                }
                else
                {
                    sb.AppendLine(GetNameAndVersion());
                    sb.AppendLine("\tParses, Validates, and Runs WebAssembly files.");
                    sb.AppendLine("Usage: ");
                    sb.AppendLine($"\t{GetName()} [options] file1.wasm [, file2.wasm, ...]");
                    sb.AppendLine("Options: ");
                    sb.AppendLine("\t-h/--help\t\tPrints this help message.");
                    sb.AppendLine("\t-v/--version\t\tPrints product version.");
                    sb.AppendLine("\t-d/--debug\t\tPrints debug information.");
                    sb.AppendLine("\t-s/--strict\t\tStrict parsing and execution.");
                    sb.AppendLine("\t-e/--execute\t\tExecute the WASM file based on the start section.");
                    sb.AppendLine("\t-r/--run [id]\t\tExecutes the WASM function [id].");
                }

                return sb.ToString();
            }

            public string GetWarnings()
            {
                StringBuilder sb = new StringBuilder();

                if (Warnings.Count > 0)
                {
                    foreach (var err in Warnings)
                    {
                        sb.AppendLine(err);
                    }
                }

                return sb.ToString();
            }

            public string GetNameAndVersion()
            {
                return $"{GetName()} {GetVersion()}";
            }

            private string GetName()
            {
                return Process.GetCurrentProcess().ProcessName;
            }

            private string GetVersion()
            {
                return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            }
        }

        public static int Main(string[] args)
        {
            Options options = new Options(args);

            if (options.Help)
            {
                Console.WriteLine(options.GetHelp());
                return 1;
            }
            else
            {
                if(options.Version)
                {
                    Console.WriteLine(options.GetNameAndVersion());
                }

                if (options.Debug)
                {
                    Console.WriteLine(options.GetWarnings());
                }
            }

            try
            {
                WASMFile file = new WASMFile();

                file.Parse(options.Filenames[0], options.Strict);

                if (options.Run != null)
                {
                    file.start = new StartSection((uint)options.Run);
                }

                if (options.Run != null || options.Execute)
                {
                    WebAssemblyInterpreter interpreter = new WebAssemblyInterpreter(file);
                    return interpreter.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return 0;
        }
    }
}
