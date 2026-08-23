using System;
using System.IO;
using System.Linq;
using System.Text;
using Squirrel.Bsdiff;
using Xunit;

namespace Squirrel.Tests
{
    public class BinaryPatchUtilityTests
    {
        [Theory]
        [InlineData(BinaryPatchUtility.SignatureZstd)]
        [InlineData(BinaryPatchUtility.SignatureLegacy)]
        [InlineData(BinaryPatchUtility.SignatureDeflate)]
        public void RoundTripPatchTest(long signature)
        {
            var oldText = "The quick brown fox jumps over the lazy dog. 1234567890.";
            var newText = "The fast brown fox leaped over the sleeping lazy dog! 1234567890 and more text.";

            var oldBytes = Encoding.UTF8.GetBytes(oldText);
            var newBytes = Encoding.UTF8.GetBytes(newText);

            byte[] patchBytes;
            using (var patchMs = new MemoryStream())
            {
                BinaryPatchUtility.Create(oldBytes, newBytes, patchMs, signature);
                patchBytes = patchMs.ToArray();
            }

            Assert.True(patchBytes.Length > 0);

            byte[] resultBytes;
            using (var outMs = new MemoryStream())
            {
                BinaryPatchUtility.Apply(
                    new MemoryStream(oldBytes),
                    () => new MemoryStream(patchBytes),
                    outMs);
                resultBytes = outMs.ToArray();
            }

            var resultText = Encoding.UTF8.GetString(resultBytes);
            Assert.Equal(newText, resultText);
        }

        [Fact]
        public void LargeBufferIterativeDiffTest()
        {
            // Verify that large payloads sort and diff without stack issues or corruption
            var random = new Random(42);
            var oldBytes = new byte[256 * 1024]; // 256KB
            random.NextBytes(oldBytes);

            var newBytes = new byte[256 * 1024];
            Buffer.BlockCopy(oldBytes, 0, newBytes, 0, oldBytes.Length);

            // Modify a few chunks
            for (int i = 5000; i < 15000; i++)
            {
                newBytes[i] = (byte)(newBytes[i] ^ 0xAA);
            }
            for (int i = 100000; i < 120000; i++)
            {
                newBytes[i] = (byte)(newBytes[i] + 1);
            }

            byte[] patchBytes;
            using (var patchMs = new MemoryStream())
            {
                BinaryPatchUtility.Create(oldBytes, newBytes, patchMs, BinaryPatchUtility.SignatureZstd);
                patchBytes = patchMs.ToArray();
            }

            // Patch should be much smaller than the full 256KB
            Assert.True(patchBytes.Length < newBytes.Length / 2);

            byte[] resultBytes;
            using (var outMs = new MemoryStream())
            {
                BinaryPatchUtility.Apply(
                    new MemoryStream(oldBytes),
                    () => new MemoryStream(patchBytes),
                    outMs);
                resultBytes = outMs.ToArray();
            }

            Assert.True(oldBytes.Length == resultBytes.Length);
            Assert.True(newBytes.SequenceEqual(resultBytes));
        }

        [Fact]
        public void CorruptedPatchThrows()
        {
            var oldBytes = Encoding.UTF8.GetBytes("Hello old world");
            var corruptPatch = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

            using (var outMs = new MemoryStream())
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    BinaryPatchUtility.Apply(
                        new MemoryStream(oldBytes),
                        () => new MemoryStream(corruptPatch),
                        outMs);
                });
            }
        }
    }
}
