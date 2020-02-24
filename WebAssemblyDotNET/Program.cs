using System;
using System.IO;
using System.Diagnostics;
using WebAssemblyDotNET;
using WebAssemblyDotNET.Sections;

namespace WebAssemblyCS
{
    class Program
    {
        static int Main(string[] args)
        {
            WASMFile file = new WASMFile();
            try
            {   
                file.Parse("helloworld.wasm");

                file.start = new StartSection(1);

                WebAssemblyInterpreter interpreter = new WebAssemblyInterpreter(file);
                return interpreter.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return -1;
        }
    }
}
