// COPYRIGHT 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Orts.Parsers.Msts
{
    /// <summary>
    /// Structured Block Reader can read compressed binary or uncompressed unicode files.
    /// Its intended to replace the KujuBinary classes ( which are binary only ).
    /// Every block must be closed with either Skip() or VerifyEndOfBlock()
    /// </summary>
    public abstract class SBR : IDisposable
    {
        public TokenID ID;
        public string Label;  // First data item may be a label ( usually a 0 byte )

        static readonly byte[] buffer = new byte[34];

        public static SBR Open(string filename)
        {
            Stream fb = new FileStream(filename, FileMode.Open, FileAccess.Read);

            Array.Clear(buffer, 0, buffer.Length);
            fb.Read(buffer, 0, 2);

            bool unicode = (buffer[0] == 0xFF && buffer[1] == 0xFE);  // unicode header

            string headerString;
            if (unicode)
            {
                fb.Read(buffer, 0, 32);
                headerString = System.Text.Encoding.Unicode.GetString(buffer, 0, 16);
            }
            else
            {
                fb.Read(buffer, 2, 14);
                headerString = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
            }

            // SIMISA@F  means compressed
            // SIMISA@@  means uncompressed
            if (headerString.StartsWith("SIMISA@F"))
            {
                fb = new InflaterInputStream(fb);
            }
            else if (headerString.StartsWith("\r\nSIMISA"))
            {
                // ie us1rd2l1000r10d.s, we are going to allow this but warn
                Console.Error.WriteLine("Improper header in " + filename);
                fb.Read(buffer, 0, 4);
            }
            else if (!headerString.StartsWith("SIMISA@@"))
            {
                throw new System.Exception("Unrecognized header \"" + headerString + "\" in " + filename);
            }

            // Read SubHeader
            string subHeader;
            if (unicode)
            {
                fb.Read(buffer, 0, 32);
                subHeader = System.Text.Encoding.Unicode.GetString(buffer, 0, 16);
            }
            else
            {
                fb.Read(buffer, 0, 16);
                subHeader = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
            }

            // Select for binary vs text content
            if (subHeader[7] == 't')
            {
                return new UnicodeFileReader(fb, filename, unicode ? Encoding.Unicode : Encoding.ASCII);
            }
            else if (subHeader[7] != 'b')
            {
                throw new System.Exception("Unrecognized subHeader \"" + subHeader + "\" in " + filename);
            }

            // And for binary types, select where their tokens will appear in our TokenID enum
            if (subHeader[5] == 'w')  // and [7] must be 'b'
            {
                return new BinaryFileReader(fb, filename, 300);
            }
            else
            {
                return new BinaryFileReader(fb, filename, 0);
            }
        }

        public abstract SBR ReadSubBlock();

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        public abstract void Skip();
        public abstract void VerifyEndOfBlock();
        public abstract uint ReadFlags();
        public abstract int ReadInt();
        public abstract uint ReadUInt();
        public abstract float ReadFloat();
        public abstract string ReadString();
        public abstract bool EndOfBlock();

        public Vector3 ReadVector3()
        {
            Vector3 vector3 = new Vector3();
            vector3.X = ReadFloat();
            vector3.Y = ReadFloat();
            vector3.Z = ReadFloat();
            return vector3;
        }

        public void VerifyID(TokenID desiredID)
        {
           if (ID != desiredID)
               TraceInformation("Expected block " + desiredID + "; got " + ID);
        }

        /// <summary>
        /// Verify that this is a comment block.
        /// </summary>
        /// <param name="block"></param>
        public void ExpectComment()
        {
            if (ID == TokenID.comment)
            {
                Skip();
            }
            else
            {
                TraceInformation("Expected block comment; got " + ID);
                Skip();
            }
        }

        public abstract void TraceInformation(string message);
        public abstract void TraceWarning(string message);
        public abstract void ThrowException(string message);

        public void Dispose()
        {
            VerifyEndOfBlock();
        }
    }

    /// <summary>
    /// Structured unicode text file reader
    /// </summary>
    public class UnicodeFileReader : UnicodeBlockReader
    {
        bool isClosed;

        public UnicodeFileReader(Stream inputStream, string filename, Encoding encoding)
        {
            f = new STFReader(inputStream, filename, encoding, false);
        }

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        /// <returns></returns>
        public override void Skip()
        {
            f.Dispose();
            isClosed = true;
        }

        public override void VerifyEndOfBlock()
        {
            if (isClosed) return;

            var s = f.ReadItem();
            var extraData = s;
            if (s.Length != 0)
            {
                // we have extra data at the end of the file
                while (s.Length != 0)
                {
                    if (s.Span != ")".AsSpan())  // we'll ignore extra )'s since the files are full of misformed brackets
                    {
                        TraceWarning("Expected end of file; got '" + s.ToString() + "'");
                        f.Dispose();
                        isClosed = true;
                        return;
                    }
                    s = f.ReadItem();
                }
            }
            f.Dispose();
            isClosed = true;
        }

        /// <summary>
        /// Note, it doesn't consume the end of block marker, you must still
        /// call VerifiyEndOfBlock to consume it
        /// </summary>
        /// <returns></returns>
        public override bool EndOfBlock()
        {
            return isClosed || atEndOfBlock || f.PeekPastWhitespace() == -1;
        }
    }

    /// <summary>
    /// Structured unicode text file reader
    /// </summary>
    public class UnicodeBlockReader : SBR
    {
        protected STFReader f;
        protected bool atEndOfBlock;

        public override SBR ReadSubBlock()
        {
            UnicodeBlockReader block = new UnicodeBlockReader();
            block.f = f;

            var token = f.ReadItem();

            if (token.Span == "(".AsSpan())
            {
                // ie 310.eng Line 349  (#_fire temp, fire mass, water mass, boil ...
                block.ID = TokenID.comment;
                return block;
            }

            // parse token
            block.ID = GetTokenID(token);

            if (token.Span == ")".AsSpan())
            {
                TraceWarning("Ignored extra close bracket");
                return block;
            }

            // now look for optional label, ie matrix MAIN ( ....
            token = f.ReadItem();

            if (token.Span != "(".AsSpan())
            {
                if (token.Length > 0)
                    block.Label = token.ToString();
                f.VerifyStartOfBlock();
            }

            return block;
        }

        /// <summary>
        /// Used to convert token string to their equivalent enum TokenID
        /// </summary>
        private static Dictionary<ReadOnlyMemory<char>, TokenID> TokenTable;

        private static void InitTokenTable()
        {
            TokenID[] tokenIDValues = (TokenID[])Enum.GetValues(typeof(TokenID));
            TokenTable = new Dictionary<ReadOnlyMemory<char>, TokenID>(tokenIDValues.GetLength(0));
            foreach (TokenID tokenID in tokenIDValues)
            {
                TokenTable.Add(tokenID.ToString().ToLower().AsMemory(), tokenID);
            }
        }

        private TokenID GetTokenID(ReadOnlyMemory<char> token)
        {
            if (TokenTable == null) InitTokenTable();

            foreach (var key in TokenTable.Keys)
                if (MemoryExtensions.Equals(key.Span, token.Span, StringComparison.OrdinalIgnoreCase))
                    return TokenTable[key];

            if (MemoryExtensions.CompareTo(token.Span, "SKIP".AsSpan(), StringComparison.OrdinalIgnoreCase) == 0)
                return TokenID.comment;
            else if (MemoryExtensions.CompareTo(token.Span, "COMMENT".AsSpan(), StringComparison.OrdinalIgnoreCase) == 0)
                return TokenID.comment;
            else if (token.Span.StartsWith("#"))
                return TokenID.comment;
            else
            {
                TraceWarning("Skipped unknown token " + token);
                return TokenID.comment;
            }
        }

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        /// <returns></returns>
        public override void Skip()
        {
            if (atEndOfBlock) return;  // already there

            // We are inside a pair of brackets, skip the entire hierarchy to past the end bracket
            int depth = 1;
            while (depth > 0)
            {
                var token = f.ReadItem();
                if (token.Length == 0)
                {
                    TraceWarning("Unexpected end of file");
                    atEndOfBlock = true;
                    return;
                }
                if (token.Span == "(".AsSpan())
                    ++depth;
                if (token.Span == ")".AsSpan())
                    --depth;
            }
            atEndOfBlock = true;
        }

        /// <summary>
        /// Note, it doesn't consume the end of block marker, you must still
        /// call VerifiyEndOfBlock to consume it
        /// </summary>
        /// <returns></returns>
        public override bool EndOfBlock()
        {
            return atEndOfBlock || f.PeekPastWhitespace() == ')' || f.EOF();
        }

        public override void VerifyEndOfBlock()
        {
            if (!atEndOfBlock)
            {
                var s = f.ReadItem();
                if (s.Span.StartsWith("#".AsSpan()) || 0 == MemoryExtensions.CompareTo(s.Span, "comment".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // allow comments at end of block ie
                    // MaxReleaseRate( 1.4074  #For train position 31-45  use (1.86 - ( 0.0146 * 31 ))	)
                    Skip();
                    return;
                }
                if (s.Span != ")".AsSpan())
                    TraceWarning("Expected end of block; got '" + s.ToString() + "'");

                atEndOfBlock = true;
            }
        }

        public override uint ReadFlags() { return f.ReadHex(null); }
        public override int ReadInt() { return f.ReadInt(null); }
        public override uint ReadUInt() { return f.ReadUInt(null); }
        public override float ReadFloat() { return f.ReadFloat(STFReader.UNITS.None, null); }
        public override string ReadString() { return f.ReadItem().ToString(); }

        public override void TraceInformation(string message)
        {
            STFException.TraceInformation(f, message);
        }

        public override void TraceWarning(string message)
        {
            STFException.TraceWarning(f, message);
        }

        public override void ThrowException(string message)
        {
            throw new STFException(f, message);
        }
    }

    /// <summary>
    /// Structured kuju binary file reader
    /// </summary>
    public class BinaryFileReader : BinaryBlockReader
    {
        /// <summary>
        /// Assumes that fb is positioned just after the SIMISA@F header
        /// filename is provided for error reporting purposes
        /// Each block has a token ID.  It's value corresponds to the value of
        /// the TokenID enum.  For some file types, ie .W files, the token value's 
        /// will be offset into the TokenID table by the specified tokenOffset.
        /// </summary>
        /// <param name="fb"></param>
        public BinaryFileReader(Stream inputStream, string filename, int tokenOffset)
        {
            Filename = filename;
            InputStream = inputStream;
            TokenOffset = tokenOffset;
        }

        public override void Skip()
        {
            var b = 1;
            while (b > 0)
                b = InputStream.Read(Bytes, 0, Bytes.Length); // InputStream.ReadByte() causes byte[] allocation, don't use that here.
        }

        public override bool EndOfBlock()
        {
            return InputStream.Position >= InputStream.Length;
        }

        public override void VerifyEndOfBlock()
        {
            if (InputStream.ReadByte() != -1)
                TraceWarning("Expected end of file; got more data");
            InputStream.Close();
        }
    }

    /// <summary>
    /// Structured kuju binary file reader
    /// </summary>
    public class BinaryBlockReader : SBR
    {
        public string Filename;  // for error reporting
        public Stream InputStream;
        public uint RemainingBytes;  // number of bytes in this block not yet read from the stream
        public uint Flags;
        protected int TokenOffset;     // the binaryTokens are offset by this amount, ie for binary world files 
        protected static readonly byte[] Bytes = new byte[1024]; // length must be min. 512

        public override SBR ReadSubBlock()
        {
            BinaryBlockReader block = new BinaryBlockReader();

            block.Filename = Filename;
            block.InputStream = InputStream;
            block.TokenOffset = TokenOffset;

            // Anything other than Stream.Read() (like e.g. Stream.ReadByte()) causes allocation, which is undesirable here.
            RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 8); // UInt16 + UInt16 + UInt32
            var ushorts = MemoryMarshal.Cast<byte, ushort>(Bytes.AsSpan().Slice(0, 4));
            var MSTSToken = ushorts[0];
            block.Flags = ushorts[1];
            block.RemainingBytes = MemoryMarshal.Cast<byte, uint>(Bytes.AsSpan().Slice(4, 4))[0];
            block.ID = (TokenID)(MSTSToken + TokenOffset);

            RemainingBytes -= block.RemainingBytes;

            block.RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 1);
            int labelLength = Bytes[0];
            if (labelLength > 0)
            {
                block.RemainingBytes -= (uint)InputStream.Read(Bytes, 0, labelLength * 2);
                block.Label = System.Text.Encoding.Unicode.GetString(Bytes, 0, labelLength * 2);
            }
            return block;
        }

        public override void Skip()
        {
            while (RemainingBytes > 0)
                RemainingBytes -= (uint)InputStream.Read(Bytes, 0, (int)Math.Min(Bytes.Length, RemainingBytes));
        }

        public override bool EndOfBlock()
        {
            return RemainingBytes == 0;
        }

        public override void VerifyEndOfBlock()
        {
            if (!EndOfBlock())
            {
                TraceWarning("Expected end of block " + ID + "; got more data");
                Skip();
            }
        }

        public override uint ReadFlags() { RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 4); return MemoryMarshal.Cast<byte, uint>(Bytes.AsSpan().Slice(0, 4))[0]; }
        public override int ReadInt() { RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 4); return MemoryMarshal.Cast<byte, int>(Bytes.AsSpan().Slice(0, 4))[0]; }
        public override uint ReadUInt() { RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 4); return MemoryMarshal.Cast<byte, uint>(Bytes.AsSpan().Slice(0, 4))[0]; }
        public override float ReadFloat() { RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 4); return MemoryMarshal.Cast<byte, float>(Bytes.AsSpan().Slice(0, 4))[0]; }
        public ushort ReadUInt16() { RemainingBytes -= (uint)InputStream.Read(Bytes, 0, 2); return MemoryMarshal.Cast<byte, ushort>(Bytes.AsSpan().Slice(0, 2))[0]; }
        public override string ReadString()
        {
            string s = "";
            ushort count = ReadUInt16();
            if (count > 0)
            {
                if (count * 2 < Bytes.Length)
                {
                    RemainingBytes -= (uint)InputStream.Read(Bytes, 0, count * 2);
                    s = System.Text.Encoding.Unicode.GetString(Bytes, 0, count * 2);
                }
                else
                {
                    var b = new byte[count * 2]; // var b = (Span<byte>)stackalloc byte[count * 2]; in .NET6
                    RemainingBytes -= (uint)InputStream.Read(b);
                    s = System.Text.Encoding.Unicode.GetString(b);
                }
            }
            return s;
        }

        public override void TraceInformation(string message)
        {
            SBRException.TraceInformation(this, message);
        }

        public override void TraceWarning(string message)
        {
            SBRException.TraceWarning(this, message);
        }

        public override void ThrowException(string message)
        {
            throw new SBRException(this, message);
        }
    }

    public class SBRException : Exception
    {
        public static void TraceWarning(BinaryBlockReader sbr, string message)
        {
            Trace.TraceWarning("{2} in {0}:byte {1}", sbr.Filename, sbr.InputStream.Position, message);
        }

        public static void TraceInformation(BinaryBlockReader sbr, string message)
        {
            Trace.TraceInformation("{2} in {0}:byte {1}", sbr.Filename, sbr.InputStream.Position, message);
        }

        public SBRException(BinaryBlockReader sbr, string message)
            : base(String.Format("{2} in {0}:byte {1}\n", sbr.Filename, sbr.InputStream.Position, message))
        {
        }
    }
}
