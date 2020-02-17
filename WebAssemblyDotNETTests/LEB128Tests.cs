using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class LEB128Tests
    {
        [TestMethod()]
        public void OverallTest()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                WriteTest(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                ReadTest(memoryStream);
            }
        }

        public void WriteTest(MemoryStream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            LEB128.WriteUInt32(writer, 0xFF00FF00);
            LEB128.WriteUInt32(writer, 0xAABBAA);
            LEB128.WriteUInt32(writer, 0xCC);
            LEB128.WriteUInt7(writer, 0x11);
            LEB128.WriteUInt7(writer, 0x7F);
            LEB128.WriteInt32(writer, 0x7F00FF00);
            LEB128.WriteInt32(writer, 0xAABBAA);
            LEB128.WriteInt32(writer, 0xCC);
            LEB128.WriteInt32(writer, -1);
            LEB128.WriteInt7(writer, 0x11);
            LEB128.WriteInt7(writer, -1);
        }

        public void ReadTest(MemoryStream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            Assert.AreEqual(LEB128.ReadUInt32(reader), 0xFF00FF00u);
            Assert.AreEqual(LEB128.ReadUInt32(reader), 0xAABBAAu);
            Assert.AreEqual(LEB128.ReadUInt32(reader), 0xCCu);
            Assert.AreEqual(LEB128.ReadUInt7(reader), 0x11u);
            Assert.AreEqual(LEB128.ReadUInt7(reader), 0x7Fu);
            Assert.AreEqual(LEB128.ReadInt32(reader), 0x7F00FF00);
            Assert.AreEqual(LEB128.ReadInt32(reader), 0xAABBAA);
            Assert.AreEqual(LEB128.ReadInt32(reader), 0xCC);
            Assert.AreEqual(LEB128.ReadInt32(reader), -1);
            Assert.AreEqual(LEB128.ReadInt7(reader), 0x11);
            Assert.AreEqual(LEB128.ReadInt7(reader), -1);
        }

        [TestMethod()]
        public void SizeOfTest()
        {
            // sizeof(Int7) is always 1 byte
            Assert.AreEqual(LEB128.SizeOf((byte)0), 1u);
            Assert.AreEqual(LEB128.SizeOf((byte)255), 1u);
            Assert.AreEqual(LEB128.SizeOf((sbyte)-1), 1u);
            Assert.AreEqual(LEB128.SizeOf((sbyte)127), 1u);

            Assert.AreEqual(LEB128.SizeOf(0), 1u);
            Assert.AreEqual(LEB128.SizeOf(0u), 1u);
            Assert.AreEqual(LEB128.SizeOf(int.MaxValue), 5u);
            Assert.AreEqual(LEB128.SizeOf(int.MinValue), 5u);
            Assert.AreEqual(LEB128.SizeOf(uint.MaxValue), 5u);
        }
    }
}