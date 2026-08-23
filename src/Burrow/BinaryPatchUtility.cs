using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;


// Adapted from https://github.com/LogosBible/bsdiff.net/blob/master/src/bsdiff/BinaryPatchUtility.cs

namespace Squirrel.Bsdiff
{
    /*
    The original bsdiff.c source code (http://www.daemonology.net/bsdiff/) is
    distributed under the following license:

    Copyright 2003-2005 Colin Percival
    All rights reserved

    Redistribution and use in source and binary forms, with or without
    modification, are permitted providing that the following conditions
    are met:
    1. Redistributions of source code must retain the above copyright
        notice, this list of conditions and the following disclaimer.
    2. Redistributions in binary form must reproduce the above copyright
        notice, this list of conditions and the following disclaimer in the
        documentation and/or other materials provided with the distribution.

    THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
    IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
    WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
    ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
    DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
    DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
    OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
    HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
    STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
    IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
    POSSIBILITY OF SUCH DAMAGE.
    */
    public class DeltaGenerationStats
    {
        public long BaseSize { get; set; }
        public long NewSize { get; set; }
        public long RawDeltaSize { get; set; }
        public long CompressedDeltaSize { get; set; }
        public int CopyOperations { get; set; }
        public int LiteralOperations { get; set; }
        public long TotalBytesCopied { get; set; }
        public long TotalBytesInserted { get; set; }
        public double CopyRatio => NewSize > 0 ? (double)TotalBytesCopied / NewSize * 100.0 : 0.0;
        public double LiteralRatio => NewSize > 0 ? (double)TotalBytesInserted / NewSize * 100.0 : 0.0;
        public double AverageCopyLength => CopyOperations > 0 ? (double)TotalBytesCopied / CopyOperations : 0.0;
        public int MaxCopyLength { get; set; }
        public long ControlStreamSize { get; set; }
        public long DiffStreamSize { get; set; }
        public long ExtraStreamSize { get; set; }
        public long ZstdCompressedControlSize { get; set; }
        public long ZstdCompressedDiffSize { get; set; }
        public long ZstdCompressedExtraSize { get; set; }
        public double CompressionRatio => NewSize > 0 ? (double)CompressedDeltaSize / NewSize * 100.0 : 0.0;
        public double MatchDiscoveryTimeMs { get; set; }
        public double InstructionGenerationTimeMs { get; set; }
        public double ZstdCompressionTimeMs { get; set; }
        public double TotalGenerationTimeMs { get; set; }

        public override string ToString()
        {
            return $"Base: {BaseSize:N0} B, New: {NewSize:N0} B | Delta: {CompressedDeltaSize:N0} B ({CompressionRatio:F2}%) | Copied: {CopyRatio:F1}% ({CopyOperations} ops, avg {AverageCopyLength:F0} B), Inserted: {LiteralRatio:F1}% ({LiteralOperations} ops) | Times: Match={MatchDiscoveryTimeMs:F1}ms, Gen={InstructionGenerationTimeMs:F1}ms, Zstd={ZstdCompressionTimeMs:F1}ms, Total={TotalGenerationTimeMs:F1}ms";
        }
    }

    class BinaryPatchUtility
    {
        public const long SignatureLegacy    = 0x3034464649445342L; // "BSDIFF40"
        public const long SignatureZstd      = 0x4454535A46445342L; // "BSDFZSTD"
        public const long SignatureDeflate   = 0x3146454446445342L; // "BSDFDEF1"
        public const long SignatureZstdDict  = 0x5443494454535A42L; // "BZSTDDI1"
        public const long SignatureZstdDiff2 = 0x5A53544449464632L; // "ZSTDIFF2"

        public const int DefaultZstdCompressionLevel = 9;

        /// <summary>
        /// Creates a high-performance, ultra-compact binary delta patch using Burrow's ZstdDiff2 engine.
        /// </summary>
        public static DeltaGenerationStats Create(byte[] oldData, byte[] newData, Stream output)
        {
            return CreateZstdDiff2(oldData, newData, output, DefaultZstdCompressionLevel);
        }

        public static DeltaGenerationStats Create(byte[] oldData, byte[] newData, Stream output, int compressionLevel)
        {
            return CreateZstdDiff2(oldData, newData, output, compressionLevel);
        }

        public static DeltaGenerationStats CreateZstdDiff2(byte[] oldData, byte[] newData, Stream output, int compressionLevel = DefaultZstdCompressionLevel)
        {
            if (oldData == null) throw new ArgumentNullException(nameof(oldData));
            if (newData == null) throw new ArgumentNullException(nameof(newData));
            if (output == null) throw new ArgumentNullException(nameof(output));

            var stats = new DeltaGenerationStats
            {
                BaseSize = oldData.Length,
                NewSize = newData.Length
            };

            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var matchSw = System.Diagnostics.Stopwatch.StartNew();

            const int SEED_SIZE = 8;

            if (oldData.Length < SEED_SIZE || newData.Length < SEED_SIZE)
            {
                // Simple literal emit
                using (var ctrlMs = new MemoryStream(32))
                using (var diffMs = new MemoryStream(1))
                using (var extraMs = new MemoryStream(newData.Length))
                {
                    extraMs.Write(newData, 0, newData.Length);
                    WriteVarInt(ctrlMs, 0);
                    WriteVarInt(ctrlMs, newData.Length);
                    WriteVarLong(ctrlMs, 0);

                    stats.LiteralOperations = 1;
                    stats.TotalBytesInserted = newData.Length;

                    EmitZstdDiff2Output(oldData, newData, ctrlMs, diffMs, extraMs, output, compressionLevel, stats);
                    totalSw.Stop();
                    stats.TotalGenerationTimeMs = totalSw.Elapsed.TotalMilliseconds;
                    return stats;
                }
            }

            int tableSize = 1 << 23; // 8M buckets
            int mask = tableSize - 1;
            int[] head = System.Buffers.ArrayPool<int>.Shared.Rent(tableSize);
            Array.Clear(head, 0, tableSize);
            for (int i = 0; i < tableSize; i++) head[i] = -1;

            int step = oldData.Length > 50_000_000 ? 3 : 2;
            int maxNext = oldData.Length / step + 1;
            int[] next = System.Buffers.ArrayPool<int>.Shared.Rent(maxNext);

            int entryCount = 0;
            for (int i = 0; i <= oldData.Length - SEED_SIZE; i += step)
            {
                uint h = Hash8(oldData, i);
                int bucket = (int)(h & (uint)mask);
                next[entryCount] = head[bucket];
                head[bucket] = i;
                entryCount++;
            }

            matchSw.Stop();
            stats.MatchDiscoveryTimeMs = matchSw.Elapsed.TotalMilliseconds;

            var genSw = System.Diagnostics.Stopwatch.StartNew();

            using (var ctrlMs = new MemoryStream(65536))
            using (var diffMs = new MemoryStream(newData.Length / 4))
            using (var extraMs = new MemoryStream(newData.Length / 16))
            {
                int scan = 0;
                int lastScan = 0;
                int lastPos = 0;

                while (scan < newData.Length)
                {
                    int bestOldPos = -1;
                    int bestLen = 0;

                    for (int sc = scan; sc <= newData.Length - SEED_SIZE; sc++)
                    {
                        uint h = Hash8(newData, sc);
                        int bucket = (int)(h & (uint)mask);
                        int cand = head[bucket];

                        int probes = 0;
                        while (cand != -1 && probes++ < 64)
                        {
                            if (cand + SEED_SIZE <= oldData.Length)
                            {
                                int len = 0;
                                int score = 0;
                                int maxScore = 0;
                                int bestL = 0;

                                while (sc + len < newData.Length && cand + len < oldData.Length)
                                {
                                    if (newData[sc + len] == oldData[cand + len])
                                        score += 2;
                                    else
                                        score -= 1;

                                    if (score > maxScore)
                                    {
                                        maxScore = score;
                                        bestL = len + 1;
                                    }
                                    else if (score < maxScore - 64)
                                    {
                                        break;
                                    }
                                    len++;
                                }

                                if (bestL >= 8 && bestL > bestLen)
                                {
                                    bestLen = bestL;
                                    bestOldPos = cand;
                                    scan = sc;
                                }
                            }
                            int idx = cand / step;
                            cand = idx < maxNext ? next[idx] : -1;
                        }

                        if (bestLen >= 8)
                            break;
                    }

                    if (bestLen < 8)
                        break;

                    // Match extension backward
                    int back = 0;
                    int backScore = 0;
                    int maxBackScore = 0;
                    int bestBack = 0;
                    while (scan - back > lastScan && bestOldPos - back > 0)
                    {
                        if (newData[scan - back - 1] == oldData[bestOldPos - back - 1])
                            backScore += 2;
                        else
                            backScore -= 1;

                        if (backScore > maxBackScore)
                        {
                            maxBackScore = backScore;
                            bestBack = back + 1;
                        }
                        else if (backScore < maxBackScore - 32)
                            break;
                        back++;
                    }

                    scan -= bestBack;
                    bestOldPos -= bestBack;
                    bestLen += bestBack;

                    // Gap bridging
                    int gap = scan - lastScan;
                    if (gap > 0 && gap <= 8 && lastPos + gap == bestOldPos)
                    {
                        bestLen += gap;
                        scan = lastScan;
                        bestOldPos = lastPos;
                        gap = 0;
                    }

                    int litLen = gap > 0 ? gap : 0;
                    if (litLen > 0)
                    {
                        extraMs.Write(newData, lastScan, litLen);
                        stats.LiteralOperations++;
                        stats.TotalBytesInserted += litLen;
                    }

                    byte[] diffBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(bestLen);
                    for (int i = 0; i < bestLen; i++)
                    {
                        diffBuf[i] = (byte)(newData[scan + i] - oldData[bestOldPos + i]);
                    }
                    diffMs.Write(diffBuf, 0, bestLen);
                    System.Buffers.ArrayPool<byte>.Shared.Return(diffBuf);

                    stats.CopyOperations++;
                    stats.TotalBytesCopied += bestLen;
                    if (bestLen > stats.MaxCopyLength) stats.MaxCopyLength = bestLen;

                    WriteVarInt(ctrlMs, bestLen);
                    WriteVarInt(ctrlMs, litLen);
                    WriteVarLong(ctrlMs, (long)(bestOldPos - lastPos));

                    lastScan = scan + bestLen;
                    lastPos = bestOldPos + bestLen;
                    scan = lastScan;
                }

                // Trailing literals
                if (lastScan < newData.Length)
                {
                    int remaining = newData.Length - lastScan;
                    extraMs.Write(newData, lastScan, remaining);
                    stats.LiteralOperations++;
                    stats.TotalBytesInserted += remaining;

                    WriteVarInt(ctrlMs, 0);
                    WriteVarInt(ctrlMs, remaining);
                    WriteVarLong(ctrlMs, 0);
                }

                System.Buffers.ArrayPool<int>.Shared.Return(head);
                System.Buffers.ArrayPool<int>.Shared.Return(next);

                genSw.Stop();
                stats.InstructionGenerationTimeMs = genSw.Elapsed.TotalMilliseconds;

                EmitZstdDiff2Output(oldData, newData, ctrlMs, diffMs, extraMs, output, compressionLevel, stats);

                totalSw.Stop();
                stats.TotalGenerationTimeMs = totalSw.Elapsed.TotalMilliseconds;
                return stats;
            }
        }

        private static void EmitZstdDiff2Output(byte[] oldData, byte[] newData, MemoryStream ctrlMs, MemoryStream diffMs, MemoryStream extraMs, Stream output, int compressionLevel, DeltaGenerationStats stats)
        {
            var compSw = System.Diagnostics.Stopwatch.StartNew();

            byte[] ctrlBytes = ctrlMs.ToArray();
            byte[] diffBytes = diffMs.ToArray();
            byte[] extraBytes = extraMs.ToArray();

            stats.ControlStreamSize = ctrlBytes.Length;
            stats.DiffStreamSize = diffBytes.Length;
            stats.ExtraStreamSize = extraBytes.Length;
            stats.RawDeltaSize = ctrlBytes.Length + diffBytes.Length + extraBytes.Length;

            using (var c = new ZstdSharp.Compressor(compressionLevel))
            {
                var cCtrl = c.Wrap(ctrlBytes);
                var cDiff = c.Wrap(diffBytes);
                var cExtra = c.Wrap(extraBytes);

                stats.ZstdCompressedControlSize = cCtrl.Length;
                stats.ZstdCompressedDiffSize = cDiff.Length;
                stats.ZstdCompressedExtraSize = cExtra.Length;

                using (var bw = new BinaryWriter(output, System.Text.Encoding.UTF8, true))
                {
                    long startPos = output.Position;

                    bw.Write(SignatureZstdDiff2);
                    bw.Write((long)oldData.Length);
                    bw.Write((long)newData.Length);
                    bw.Write((long)cCtrl.Length);
                    bw.Write((long)cDiff.Length);
                    bw.Write((long)cExtra.Length);
                    bw.Write(cCtrl.ToArray());
                    bw.Write(cDiff.ToArray());
                    bw.Write(cExtra.ToArray());

                    stats.CompressedDeltaSize = output.Position - startPos;
                }
            }

            compSw.Stop();
            stats.ZstdCompressionTimeMs = compSw.Elapsed.TotalMilliseconds;
        }

        private static uint Hash8(byte[] data, int offset)
        {
            uint h = 0x811c9dc5;
            uint k1 = BitConverter.ToUInt32(data, offset);
            uint k2 = BitConverter.ToUInt32(data, offset + 4);
            h = ((h ^ k1) * 0x01000193) ^ k2;
            return h;
        }

        private static void WriteVarInt(Stream stream, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80) { stream.WriteByte((byte)(v | 0x80)); v >>= 7; }
            stream.WriteByte((byte)v);
        }

        private static int ReadVarInt(Stream stream)
        {
            int result = 0, shift = 0;
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) break;
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        private static void WriteVarLong(Stream stream, long value)
        {
            ulong v = (ulong)((value << 1) ^ (value >> 63));
            while (v >= 0x80) { stream.WriteByte((byte)(v | 0x80)); v >>= 7; }
            stream.WriteByte((byte)v);
        }

        private static long ReadVarLong(Stream stream)
        {
            ulong result = 0; int shift = 0;
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) break;
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return (long)(result >> 1) ^ -(long)(result & 1);
        }

        public static void Create(byte[] oldData, byte[] newData, Stream output, long signature, int compressionLevel = DefaultZstdCompressionLevel)
        {
            if (oldData == null)
                throw new ArgumentNullException(nameof(oldData));
            if (newData == null)
                throw new ArgumentNullException(nameof(newData));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (!output.CanSeek)
                throw new ArgumentException("Output stream must be seekable.", nameof(output));
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable.", nameof(output));

            byte[] header = new byte[c_headerSize];
            WriteInt64(signature, header, 0);
            WriteInt64(0, header, 8);
            WriteInt64(0, header, 16);
            WriteInt64(newData.Length, header, 24);

            long startPosition = output.Position;
            output.Write(header, 0, header.Length);

            int[] I = SuffixSort(oldData);

            byte[] db = new byte[newData.Length];
            byte[] eb = new byte[newData.Length];

            int dblen = 0;
            int eblen = 0;

            using (MemoryStream ctrlMs = new MemoryStream())
            {
                // compute the differences, writing ctrl as we go
                int scan = 0;
                int pos = 0;
                int len = 0;
                int lastscan = 0;
                int lastpos = 0;
                int lastoffset = 0;
                while (scan < newData.Length)
                {
                    int oldscore = 0;

                    for (int scsc = scan += len; scan < newData.Length; scan++)
                    {
                        len = Search(I, oldData, newData, scan, 0, oldData.Length, out pos);

                        for (; scsc < scan + len; scsc++)
                        {
                            if ((scsc + lastoffset < oldData.Length) && (oldData[scsc + lastoffset] == newData[scsc]))
                                oldscore++;
                        }

                        if ((len == oldscore && len != 0) || (len > oldscore + 8))
                            break;

                        if ((scan + lastoffset < oldData.Length) && (oldData[scan + lastoffset] == newData[scan]))
                            oldscore--;
                    }

                    if (len != oldscore || scan == newData.Length)
                    {
                        int s = 0;
                        int sf = 0;
                        int lenf = 0;
                        for (int i = 0; (lastscan + i < scan) && (lastpos + i < oldData.Length); )
                        {
                            if (oldData[lastpos + i] == newData[lastscan + i])
                                s++;
                            i++;
                            if (s * 2 - i > sf * 2 - lenf)
                            {
                                sf = s;
                                lenf = i;
                            }
                        }

                        int lenb = 0;
                        if (scan < newData.Length)
                        {
                            s = 0;
                            int sb = 0;
                            for (int i = 1; (scan >= lastscan + i) && (pos >= i); i++)
                            {
                                if (oldData[pos - i] == newData[scan - i])
                                    s++;
                                if (s * 2 - i > sb * 2 - lenb)
                                {
                                    sb = s;
                                    lenb = i;
                                }
                            }
                        }

                        if (lastscan + lenf > scan - lenb)
                        {
                            int overlap = (lastscan + lenf) - (scan - lenb);
                            s = 0;
                            int ss = 0;
                            int lens = 0;
                            for (int i = 0; i < overlap; i++)
                            {
                                if (newData[lastscan + lenf - overlap + i] == oldData[lastpos + lenf - overlap + i])
                                    s++;
                                if (newData[scan - lenb + i] == oldData[pos - lenb + i])
                                    s--;
                                if (s > ss)
                                {
                                    ss = s;
                                    lens = i + 1;
                                }
                            }

                            lenf += lens - overlap;
                            lenb -= lens;
                        }

                        for (int i = 0; i < lenf; i++)
                            db[dblen + i] = (byte)(newData[lastscan + i] - oldData[lastpos + i]);
                        for (int i = 0; i < (scan - lenb) - (lastscan + lenf); i++)
                            eb[eblen + i] = newData[lastscan + lenf + i];

                        dblen += lenf;
                        eblen += (scan - lenb) - (lastscan + lenf);

                        byte[] buf = new byte[8];
                        WriteInt64(lenf, buf, 0);
                        ctrlMs.Write(buf, 0, 8);

                        WriteInt64((scan - lenb) - (lastscan + lenf), buf, 0);
                        ctrlMs.Write(buf, 0, 8);

                        WriteInt64((pos - lenb) - (lastpos + lenf), buf, 0);
                        ctrlMs.Write(buf, 0, 8);

                        lastscan = scan - lenb;
                        lastpos = pos - lenb;
                        lastoffset = pos - scan;
                    }
                }

                // Compress and write control block
                long controlStartPosition = output.Position;
                CompressData(ctrlMs.ToArray(), output, signature, compressionLevel);
                long controlEndPosition = output.Position;
                WriteInt64(controlEndPosition - controlStartPosition, header, 8);

                // Compress and write diff block
                byte[] diffData = new byte[dblen];
                Buffer.BlockCopy(db, 0, diffData, 0, dblen);
                CompressData(diffData, output, signature, compressionLevel);
                long diffEndPosition = output.Position;
                WriteInt64(diffEndPosition - controlEndPosition, header, 16);

                // Compress and write extra block, if any
                if (eblen > 0)
                {
                    byte[] extraData = new byte[eblen];
                    Buffer.BlockCopy(eb, 0, extraData, 0, eblen);
                    CompressData(extraData, output, signature, compressionLevel);
                }

                // seek to the beginning, rewrite the header with lengths, then seek back to end
                long endPosition = output.Position;
                output.Position = startPosition;
                output.Write(header, 0, header.Length);
                output.Position = endPosition;
            }
        }

        private static void CompressData(byte[] data, Stream output, long signature, int compressionLevel)
        {
            if (signature == SignatureZstd)
            {
                using (var wrappingStream = new WrappingStream(output, Ownership.None))
                using (var zstd = new ZstdSharp.CompressionStream(wrappingStream, compressionLevel))
                {
                    zstd.Write(data, 0, data.Length);
                }
            }
            else if (signature == SignatureDeflate)
            {
                using (var deflate = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    deflate.Write(data, 0, data.Length);
                }
            }
            else
            {
                using (var deflate = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    deflate.Write(data, 0, data.Length);
                }
            }
        }

        private static Stream CreateDecompressor(Stream input, long signature)
        {
            if (signature == SignatureZstd)
            {
                return new ZstdSharp.DecompressionStream(input);
            }
            else if (signature == SignatureDeflate)
            {
                return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, true);
            }
            else if (signature == SignatureLegacy)
            {
                if (input.CanSeek)
                {
                    long pos = input.Position;
                    int b0 = input.ReadByte();
                    int b1 = input.ReadByte();
                    int b2 = input.ReadByte();
                    input.Position = pos;

                    if (b0 == 'B' && b1 == 'Z' && b2 == 'h')
                    {
                        return new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(input);
                    }
                    else
                    {
                        return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, true);
                    }
                }
                return new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(input);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported binary patch format signature: 0x{signature:X16}");
            }
        }

        /// <summary>
        /// Applies a binary patch to the data in <paramref name="input"/> and writes the results of patching to <paramref name="output"/>.
        /// Supports both modern Zstandard patches (BSDFZSTD) and legacy BZip2 patches (BSDIFF40).
        /// </summary>
        public static void Apply(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (openPatchStream == null)
                throw new ArgumentNullException(nameof(openPatchStream));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            long controlLength, diffLength, newSize, signature;
            using (Stream patchStream = openPatchStream())
            {
                if (!patchStream.CanRead)
                    throw new ArgumentException("Patch stream must be readable.", nameof(openPatchStream));
                if (!patchStream.CanSeek)
                    throw new ArgumentException("Patch stream must be seekable.", nameof(openPatchStream));

                byte[] header;
                try
                {
                    header = patchStream.ReadExactly(c_headerSize);
                }
                catch (EndOfStreamException ex)
                {
                    throw new InvalidOperationException("Corrupt patch: header is incomplete.", ex);
                }

                signature = ReadInt64(header, 0);

                if (signature == SignatureZstdDiff2)
                {
                    long oldLength = ReadInt64(header, 8);
                    newSize = ReadInt64(header, 16);
                    long cCtrlLen = ReadInt64(header, 24);

                    byte[] headerExt = patchStream.ReadExactly(16);
                    long cDiffLen = ReadInt64(headerExt, 0);
                    long cExtraLen = ReadInt64(headerExt, 8);

                    byte[] cCtrl = patchStream.ReadExactly((int)cCtrlLen);
                    byte[] cDiff = patchStream.ReadExactly((int)cDiffLen);
                    byte[] cExtra = patchStream.ReadExactly((int)cExtraLen);

                    byte[] ctrlBytes, diffBytes, extraBytes;
                    using (var decompressor = new ZstdSharp.Decompressor())
                    {
                        ctrlBytes = decompressor.Unwrap(cCtrl).ToArray();
                        diffBytes = decompressor.Unwrap(cDiff).ToArray();
                        extraBytes = decompressor.Unwrap(cExtra).ToArray();
                    }

                    byte[] baseData;
                    using (var ms = new MemoryStream((int)Math.Min(int.MaxValue, input.Length)))
                    {
                        input.Position = 0;
                        input.CopyTo(ms);
                        baseData = ms.ToArray();
                    }

                    byte[] restored = new byte[newSize];

                    using (var ctrlMs = new MemoryStream(ctrlBytes))
                    {
                        int oldPos = 0;
                        int newPos = 0;
                        int diffPos = 0;
                        int extraPos = 0;

                        while (ctrlMs.Position < ctrlMs.Length && newPos < newSize)
                        {
                            int diffLen = ReadVarInt(ctrlMs);
                            int extraLen = ReadVarInt(ctrlMs);
                            long seekOffset = ReadVarLong(ctrlMs);

                            if (extraLen > 0)
                            {
                                Buffer.BlockCopy(extraBytes, extraPos, restored, newPos, extraLen);
                                extraPos += extraLen;
                                newPos += extraLen;
                            }

                            if (diffLen > 0)
                            {
                                oldPos += (int)seekOffset;
                                for (int i = 0; i < diffLen; i++)
                                {
                                    restored[newPos + i] = (byte)(diffBytes[diffPos + i] + baseData[oldPos + i]);
                                }
                                diffPos += diffLen;
                                newPos += diffLen;
                                oldPos += diffLen;
                            }
                        }
                    }

                    output.Write(restored, 0, (int)newSize);
                    return;
                }

                if (signature == SignatureZstdDict)
                {
                    long oldLength = ReadInt64(header, 8);
                    newSize = ReadInt64(header, 16);
                    long compressedLength = ReadInt64(header, 24);

                    byte[] dictData;
                    using (var ms = new MemoryStream((int)Math.Min(int.MaxValue, input.Length)))
                    {
                        input.Position = 0;
                        input.CopyTo(ms);
                        dictData = ms.ToArray();
                    }

                    byte[] compressedData = patchStream.ReadExactly((int)compressedLength);

                    using (var decompressor = new ZstdSharp.Decompressor())
                    {
                        decompressor.LoadDictionary(dictData);
                        var restoredSpan = decompressor.Unwrap(compressedData, (int)newSize);
                        output.Write(restoredSpan.ToArray(), 0, restoredSpan.Length);
                    }
                    return;
                }

                if (signature != SignatureZstd && signature != SignatureLegacy && signature != SignatureDeflate)
                    throw new InvalidOperationException("Corrupt or unsupported patch format.");

                controlLength = ReadInt64(header, 8);
                diffLength = ReadInt64(header, 16);
                newSize = ReadInt64(header, 24);
                if (controlLength < 0 || diffLength < 0 || newSize < 0)
                    throw new InvalidOperationException("Corrupt patch.");
            }

            const int c_bufferSize = 1048576;
            byte[] newData = new byte[c_bufferSize];
            byte[] oldData = new byte[c_bufferSize];

            using (Stream compressedControlStream = openPatchStream())
            using (Stream compressedDiffStream = openPatchStream())
            using (Stream compressedExtraStream = openPatchStream())
            {
                compressedControlStream.Seek(c_headerSize, SeekOrigin.Current);
                compressedDiffStream.Seek(c_headerSize + controlLength, SeekOrigin.Current);
                compressedExtraStream.Seek(c_headerSize + controlLength + diffLength, SeekOrigin.Current);

                var hasExtraData = compressedExtraStream.Position < compressedExtraStream.Length;

                using (var controlStream = CreateDecompressor(compressedControlStream, signature))
                using (var diffStream = CreateDecompressor(compressedDiffStream, signature))
                using (var extraStream = hasExtraData ? CreateDecompressor(compressedExtraStream, signature) : null)
                {
                    long[] control = new long[3];
                    byte[] buffer = new byte[8];

                    int oldPosition = 0;
                    int newPosition = 0;
                    while (newPosition < newSize)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            controlStream.ReadExactly(buffer, 0, 8);
                            control[i] = ReadInt64(buffer, 0);
                        }

                        if (newPosition + control[0] > newSize || control[0] < 0)
                            throw new InvalidOperationException($"Corrupt patch. control=[{control[0]},{control[1]},{control[2]}] newPosition={newPosition} oldPosition={oldPosition} newSize={newSize}");

                        input.Position = oldPosition;

                        int bytesToCopy = (int)control[0];
                        while (bytesToCopy > 0)
                        {
                            int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                            diffStream.ReadExactly(newData, 0, actualBytesToCopy);

                            int availableInputBytes = Math.Min(actualBytesToCopy, (int)(input.Length - input.Position));
                            input.ReadExactly(oldData, 0, availableInputBytes);

                            for (int index = 0; index < availableInputBytes; index++)
                                newData[index] += oldData[index];

                            output.Write(newData, 0, actualBytesToCopy);

                            newPosition += actualBytesToCopy;
                            oldPosition += actualBytesToCopy;
                            bytesToCopy -= actualBytesToCopy;
                        }

                        if (newPosition + control[1] > newSize)
                            throw new InvalidOperationException($"Corrupt patch. control=[{control[0]},{control[1]},{control[2]}] newPosition={newPosition} oldPosition={oldPosition} newSize={newSize}");

                        bytesToCopy = (int)control[1];
                        while (bytesToCopy > 0)
                        {
                            int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                            extraStream.ReadExactly(newData, 0, actualBytesToCopy);
                            output.Write(newData, 0, actualBytesToCopy);

                            newPosition += actualBytesToCopy;
                            bytesToCopy -= actualBytesToCopy;
                        }

                        oldPosition = (int)(oldPosition + control[2]);
                    }
                }
            }
        }

        private static int CompareBytes(byte[] left, int leftOffset, byte[] right, int rightOffset)
        {
            for (int index = 0; index < left.Length - leftOffset && index < right.Length - rightOffset; index++)
            {
                int diff = left[index + leftOffset] - right[index + rightOffset];
                if (diff != 0)
                    return diff;
            }
            return 0;
        }

        private static int MatchLength(byte[] oldData, int oldOffset, byte[] newData, int newOffset)
        {
            int i;
            for (i = 0; i < oldData.Length - oldOffset && i < newData.Length - newOffset; i++)
            {
                if (oldData[i + oldOffset] != newData[i + newOffset])
                    break;
            }
            return i;
        }

        private static int Search(int[] I, byte[] oldData, byte[] newData, int newOffset, int start, int end, out int pos)
        {
            if (end - start < 2)
            {
                int startLength = MatchLength(oldData, I[start], newData, newOffset);
                int endLength = MatchLength(oldData, I[end], newData, newOffset);

                if (startLength > endLength)
                {
                    pos = I[start];
                    return startLength;
                }
                else
                {
                    pos = I[end];
                    return endLength;
                }
            }
            else
            {
                int midPoint = start + (end - start) / 2;
                return CompareBytes(oldData, I[midPoint], newData, newOffset) < 0 ?
                    Search(I, oldData, newData, newOffset, midPoint, end, out pos) :
                    Search(I, oldData, newData, newOffset, start, midPoint, out pos);
            }
        }

        private struct SplitRange
        {
            public int Start;
            public int Len;
            public int H;

            public SplitRange(int start, int len, int h)
            {
                Start = start;
                Len = len;
                H = h;
            }
        }

        private static void Split(int[] I, int[] v, int initialStart, int initialLen, int initialH)
        {
            var stack = new Stack<SplitRange>();
            stack.Push(new SplitRange(initialStart, initialLen, initialH));

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                int start = current.Start;
                int len = current.Len;
                int h = current.H;

                if (len < 16)
                {
                    int j;
                    for (int k = start; k < start + len; k += j)
                    {
                        j = 1;
                        int x = v[I[k] + h];
                        for (int i = 1; k + i < start + len; i++)
                        {
                            if (v[I[k + i] + h] < x)
                            {
                                x = v[I[k + i] + h];
                                j = 0;
                            }
                            if (v[I[k + i] + h] == x)
                            {
                                Swap(ref I[k + j], ref I[k + i]);
                                j++;
                            }
                        }
                        for (int i = 0; i < j; i++)
                            v[I[k + i]] = k + j - 1;
                        if (j == 1)
                            I[k] = -1;
                    }
                }
                else
                {
                    int x = v[I[start + len / 2] + h];
                    int jj = 0;
                    int kk = 0;
                    for (int i2 = start; i2 < start + len; i2++)
                    {
                        if (v[I[i2] + h] < x)
                            jj++;
                        if (v[I[i2] + h] == x)
                            kk++;
                    }
                    jj += start;
                    kk += jj;

                    int i = start;
                    int j = 0;
                    int k = 0;
                    while (i < jj)
                    {
                        if (v[I[i] + h] < x)
                        {
                            i++;
                        }
                        else if (v[I[i] + h] == x)
                        {
                            Swap(ref I[i], ref I[jj + j]);
                            j++;
                        }
                        else
                        {
                            Swap(ref I[i], ref I[kk + k]);
                            k++;
                        }
                    }

                    while (jj + j < kk)
                    {
                        if (v[I[jj + j] + h] == x)
                        {
                            j++;
                        }
                        else
                        {
                            Swap(ref I[jj + j], ref I[kk + k]);
                            k++;
                        }
                    }

                    for (i = 0; i < kk - jj; i++)
                        v[I[jj + i]] = kk - 1;
                    if (jj == kk - 1)
                        I[jj] = -1;

                    if (start + len > kk)
                        stack.Push(new SplitRange(kk, start + len - kk, h));

                    if (jj > start)
                        stack.Push(new SplitRange(start, jj - start, h));
                }
            }
        }

        private static int[] SuffixSort(byte[] oldData)
        {
            int[] buckets = new int[256];

            foreach (byte oldByte in oldData)
                buckets[oldByte]++;
            for (int i = 1; i < 256; i++)
                buckets[i] += buckets[i - 1];
            for (int i = 255; i > 0; i--)
                buckets[i] = buckets[i - 1];
            buckets[0] = 0;

            int[] I = new int[oldData.Length + 1];
            for (int i = 0; i < oldData.Length; i++)
                I[++buckets[oldData[i]]] = i;

            int[] v = new int[oldData.Length + 1];
            for (int i = 0; i < oldData.Length; i++)
                v[i] = buckets[oldData[i]];

            for (int i = 1; i < 256; i++)
            {
                if (buckets[i] == buckets[i - 1] + 1)
                    I[buckets[i]] = -1;
            }
            I[0] = -1;

            for (int h = 1; I[0] != -(oldData.Length + 1); h += h)
            {
                int len = 0;
                int i = 0;
                while (i < oldData.Length + 1)
                {
                    if (I[i] < 0)
                    {
                        len -= I[i];
                        i -= I[i];
                    }
                    else
                    {
                        if (len != 0)
                            I[i - len] = -len;
                        len = v[I[i]] + 1 - i;
                        Split(I, v, i, len, h);
                        i += len;
                        len = 0;
                    }
                }

                if (len != 0)
                    I[i - len] = -len;
            }

            for (int i = 0; i < oldData.Length + 1; i++)
                I[v[i]] = i;

            return I;
        }

        private static void Swap(ref int first, ref int second)
        {
            int temp = first;
            first = second;
            second = temp;
        }

        private static long ReadInt64(byte[] buf, int offset)
        {
            long value = buf[offset + 7] & 0x7F;

            for (int index = 6; index >= 0; index--)
            {
                value *= 256;
                value += buf[offset + index];
            }

            if ((buf[offset + 7] & 0x80) != 0)
                value = -value;

            return value;
        }

        private static void WriteInt64(long value, byte[] buf, int offset)
        {
            long valueToWrite = value < 0 ? -value : value;

            for (int byteIndex = 0; byteIndex < 8; byteIndex++)
            {
                buf[offset + byteIndex] = (byte)(valueToWrite % 256);
                valueToWrite -= buf[offset + byteIndex];
                valueToWrite /= 256;
            }

            if (value < 0)
                buf[offset + 7] |= 0x80;
        }

        const long c_fileSignature = 0x3034464649445342L;
        const int c_headerSize = 32;
    }

    /// <summary>
    /// A <see cref="Stream"/> that wraps another stream. One major feature of <see cref="WrappingStream"/> is that it does not dispose the
    /// underlying stream when it is disposed if Ownership.None is used; this is useful when using classes such as <see cref="BinaryReader"/> and
    /// <see cref="System.Security.Cryptography.CryptoStream"/> that take ownership of the stream passed to their constructors.
    /// </summary>
    /// <remarks>See <a href="http://code.logos.com/blog/2009/05/wrappingstream_implementation.html">WrappingStream Implementation</a>.</remarks>
    public class WrappingStream : Stream
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WrappingStream"/> class.
        /// </summary>
        /// <param name="streamBase">The wrapped stream.</param>
        /// <param name="ownership">Use Owns if the wrapped stream should be disposed when this stream is disposed.</param>
        public WrappingStream(Stream streamBase, Ownership ownership)
        {
            // check parameters
            if (streamBase == null)
                throw new ArgumentNullException("streamBase");

            m_streamBase = streamBase;
            m_ownership = ownership;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</returns>
        public override bool CanRead
        {
            get { return m_streamBase == null ? false : m_streamBase.CanRead; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</returns>
        public override bool CanSeek
        {
            get { return m_streamBase == null ? false : m_streamBase.CanSeek; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</returns>
        public override bool CanWrite
        {
            get { return m_streamBase == null ? false : m_streamBase.CanWrite; }
        }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length
        {
            get { ThrowIfDisposed(); return m_streamBase.Length; }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public override long Position
        {
            get { ThrowIfDisposed(); return m_streamBase.Position; }
            set { ThrowIfDisposed(); m_streamBase.Position = value; }
        }

        /// <summary>
        /// Begins an asynchronous read operation.
        /// </summary>
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            return m_streamBase.BeginRead(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous write operation.
        /// </summary>
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            return m_streamBase.BeginWrite(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Waits for the pending asynchronous read to complete.
        /// </summary>
        public override int EndRead(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            return m_streamBase.EndRead(asyncResult);
        }

        /// <summary>
        /// Ends an asynchronous write operation.
        /// </summary>
        public override void EndWrite(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            m_streamBase.EndWrite(asyncResult);
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
            ThrowIfDisposed();
            m_streamBase.Flush();
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position
        /// within the stream by the number of bytes read.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            return m_streamBase.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        public override int ReadByte()
        {
            ThrowIfDisposed();
            return m_streamBase.ReadByte();
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            return m_streamBase.Seek(offset, origin);
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            m_streamBase.SetLength(value);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position
        /// within this stream by the number of bytes written.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            m_streamBase.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
        /// </summary>
        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            m_streamBase.WriteByte(value);
        }

        /// <summary>
        /// Gets the wrapped stream.
        /// </summary>
        /// <value>The wrapped stream.</value>
        protected Stream WrappedStream
        {
            get { return m_streamBase; }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="WrappingStream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                // doesn't close the base stream, but just prevents access to it through this WrappingStream
                if (disposing)
                {
                    if (m_streamBase != null && m_ownership == Ownership.Owns)
                        m_streamBase.Dispose();
                    m_streamBase = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ThrowIfDisposed()
        {
            // throws an ObjectDisposedException if this object has been disposed
            if (m_streamBase == null)
                throw new ObjectDisposedException(GetType().Name);
        }

        Stream m_streamBase;
        readonly Ownership m_ownership;
    }

    /// <summary>
    /// Indicates whether an object takes ownership of an item.
    /// </summary>
    public enum Ownership
    {
        /// <summary>
        /// The object does not own this item.
        /// </summary>
        None,

        /// <summary>
        /// The object owns this item, and is responsible for releasing it.
        /// </summary>
        Owns
    }

    /// <summary>
    /// Provides helper methods for working with <see cref="Stream"/>.
    /// </summary>
    public static class StreamUtility
    {
        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="count">The count of bytes to read.</param>
        /// <returns>A new byte array containing the data read from the stream.</returns>
        public static byte[] ReadExactly(this Stream stream, int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            byte[] buffer = new byte[count];
            ReadExactly(stream, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/> into
        /// <paramref name="buffer"/>, starting at the byte given by <paramref name="offset"/>.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to read data into.</param>
        /// <param name="offset">The offset within the buffer at which data is first written.</param>
        /// <param name="count">The count of bytes to read.</param>
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            // check arguments
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0 || buffer.Length - offset < count)
                throw new ArgumentOutOfRangeException("count");

            while (count > 0)
            {
                // read data
                int bytesRead = stream.Read(buffer, offset, count);

                // check for failure to read
                if (bytesRead == 0)
                    throw new EndOfStreamException();

                // move to next block
                offset += bytesRead;
                count -= bytesRead;
            }
        }
    }
}