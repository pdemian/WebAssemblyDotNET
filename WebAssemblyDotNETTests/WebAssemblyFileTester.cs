﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAssemblyDotNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WebAssemblyDotNET.Tests
{
    [TestClass()]
    public class WebAssemblyFileTester
    {
        [TestMethod()]
        public void OverallTest()
        {
            // Test every file in the tests directory
            foreach(string file in Directory.GetFiles("../../../WebAssemblyDotNET/tests", "*.wasm"))
            {
                TestFile(file);
            }
        }

        public void TestFile(string filename)
        {
            MemoryStream writeStream = new MemoryStream();
            MemoryStream readStream = new MemoryStream();
            WebAssemblyFile file = new WebAssemblyFile();

            // Read and save file
            file.ParseAsWASM(filename, false);
            file.SaveAsWASM(writeStream);

            // Assert that BinarySize is in fact correct. No need to test individual sizeofs as writeStream is summed anyways
            Assert.AreEqual<long>(writeStream.Position, file.BinarySize());

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                fs.CopyTo(readStream);
            }

            // Sanity check size on disk is in fact what we read
            Assert.AreEqual((new FileInfo(filename)).Length, readStream.Position);

            // Check that streams are the same
            Assert.AreEqual(writeStream.Position, readStream.Position);

            // Todo: For large files, should do buffered comparisons rather than using .ToArray()
            // ToArray doesn't require us to seek back to 0

            //writeStream.Seek(0, SeekOrigin.Begin);
            //readStream.Seek(0, SeekOrigin.Begin);

            Assert.IsTrue(writeStream.ToArray().SequenceEqual(readStream.ToArray()));

        }
    }
}