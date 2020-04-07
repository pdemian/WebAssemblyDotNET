using System;
using System.IO;
using System.Diagnostics;
using WebAssemblyDotNET;
using WebAssemblyDotNET.Sections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using NLog;
namespace WebAssemblyCS
{
    class Options
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public readonly bool Help;
        public readonly bool Verbose;
        public readonly bool Debug;
        public readonly bool Strict;
        public readonly bool Execute;
        public readonly uint? Run;
        public readonly List<string> Filenames = new List<string>();

        private readonly bool any_errors;

        public Options(string[] args)
        {
            if (args.Length == 0) Help = true;

            for (int i = 0; i < args.Length; i++)
            {
                string current_arg = args[i].ToUpper();

                if (current_arg.StartsWith("-") || current_arg.StartsWith("/"))
                {
                    current_arg = current_arg.Substring(1);
                    switch (current_arg)
                    {
                        case "H":
                        case "?":
                            if (Help) logger.Warn("Duplicate help argument.");
                            Help = true;
                            break;
                        case "V":
                            if (Verbose) logger.Warn("Duplicate verbose argument.");
                            Verbose = true;
                            break;
                        case "D":
                            if (Debug) logger.Warn("Duplicate debug argument.");
                            Debug = true;
                            break;
                        case "S":
                            if (Strict) logger.Warn("Duplicate strict argument.");
                            Strict = true;
                            break;
                        case "E":
                            if (Execute) logger.Warn("Duplicate execute argument.");
                            Execute = true;
                            break;
                        case "R":
                            if (Run != null) logger.Warn("Duplicate run argument.");
                            try
                            {
                                Run = uint.Parse(args[++i]);
                            }
                            catch (Exception)
                            {
                                logger.Error("Invalid run argument.");
                                any_errors = true;
                            }
                            break;
                        default:
                            logger.Error($"Unknown argument {args[i]}.");
                            any_errors = true;
                            break;
                    }
                }
                else if (current_arg.StartsWith("--"))
                {
                    current_arg = current_arg.Substring(2);
                    switch (current_arg)
                    {
                        case "HELP":
                            if (Help) logger.Warn("Duplicate help argument.");
                            Help = true;
                            break;
                        case "VERBOSE":
                            if (Verbose) logger.Warn("Duplicate Verbose argument.");
                            Verbose = true;
                            break;
                        case "DEBUG":
                            if (Debug) logger.Warn("Duplicate debug argument.");
                            Debug = true;
                            break;
                        case "STRICT":
                            if (Strict) logger.Warn("Duplicate strict argument.");
                            Strict = true;
                            break;
                        case "EXECUTE":
                            if (Execute) logger.Warn("Duplicate execute argument.");
                            Execute = true;
                            break;
                        case "RUN":
                            if (Run != null) logger.Warn("Duplicate run argument.");
                            try
                            {
                                Run = uint.Parse(args[++i]);
                            }
                            catch (Exception)
                            {
                                logger.Error("Invalid run argument.");
                                any_errors = true;
                            }
                            break;
                        default:
                            logger.Error($"Unknown argument {args[i]}.");
                            any_errors = true;
                            break;
                    }
                }
                else
                {
                    if (!File.Exists(args[i]))
                    {
                        logger.Error($"File {args[i]} does not exist or cannot be accessed.");
                        any_errors = true;
                    }
                    else
                    {
                        Filenames.Add(args[i]);
                    }
                }
            }

            if (Run != null && Execute) logger.Warn("Run will overwrite the start function specified by execute.");

            if (Verbose && Debug) logger.Warn("The debug flag overrides the verbose flag.");

            if (Filenames.Count == 0)
            {
                logger.Error("No files to process.");
                any_errors = true;
            }
        }

        public string GetHelp()
        {
            StringBuilder sb = new StringBuilder();

            if (any_errors && !Help)
            {
                sb.AppendLine($"Try '{Process.GetCurrentProcess().ProcessName} --help' for more information.");
            }
            else
            {
                sb.AppendLine("\tParses, Validates, and Runs WebAssembly files.");
                sb.AppendLine("Usage: ");
                sb.AppendLine($"\t{Process.GetCurrentProcess().ProcessName} [options] file1.wasm [, file2.wasm, ...]");
                sb.AppendLine("Options: ");
                sb.AppendLine("\t-h/--help\t\tPrints this help message.");
                sb.AppendLine("\t-v/--version\t\tPrints product version.");
                sb.AppendLine("\t-d/--debug\t\tPrints debug information.");
                sb.AppendLine("\t-s/--strict\t\tStrict parsing and execution.");
                sb.AppendLine("\t-e/--execute\t\tExecute the WebAssembly file based on the start section.");
                sb.AppendLine("\t-r/--run [id]\t\tExecutes the WebAssembly function [id].");
            }

            return sb.ToString();
        }
    }

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static int Main(string[] args)
        {
            Options options = new Options(args);

            if (options.Help)
            {
                logger.Info(options.GetHelp());
                return 1;
            }
            else
            {
                logger.Info($"{Process.GetCurrentProcess().ProcessName} {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}");

                if (options.Debug)
                {
                    LogManager.Configuration.Variables["minimumLogLevel"] = "Trace";
                    LogManager.ReconfigExistingLoggers();

                }
                else if (options.Verbose)
                {
                    LogManager.Configuration.Variables["minimumLogLevel"] = "Debug";
                    LogManager.ReconfigExistingLoggers();
                }
            }

            try
            {
                WebAssemblyFile file = new WebAssemblyFile();

                file.Parse(options.Filenames[0], options.Strict);

                if (options.Run != null)
                {
                    logger.Debug($"Setting start section to {options.Run}.");
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
                logger.Fatal(ex, $"Exception: {ex.Message}");
            }

            return 0;
        }
    }
}
