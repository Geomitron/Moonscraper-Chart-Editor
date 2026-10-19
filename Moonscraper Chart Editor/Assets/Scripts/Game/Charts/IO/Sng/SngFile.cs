// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MoonscraperChartEditor.Song.IO
{
    /// <summary>
    /// The layout of a .sng song package: one file holding a song's metadata (its song.ini values) and its files (chart, audio, images, video).
    /// Every file is masked with a key made from the package's xorMask and the byte's position within that file.
    /// Format: https://github.com/mdsitton/SngFileFormat
    /// </summary>
    public class SngFile
    {
        public const string FileExtension = ".sng";
        public const uint SupportedVersion = 1;
        public const int XorMaskLength = 16;

        // parse-sng, used by Bridge and Chorus Encore, reads file name lengths as signed bytes
        public const int MaxFilenameBytes = 127;

        static readonly byte[] c_identifier = Encoding.ASCII.GetBytes("SNGPKG");
        static readonly UTF8Encoding c_utf8 = new UTF8Encoding(false);

        public class FileEntry
        {
            public string name;
            public long length;
            public long offset;     // Of the masked contents, from the start of the .sng
        }

        public byte[] xorMask { get; private set; }

        /// <summary>
        /// The metadata section as stored, from the pair count to the last pair. Writing it back unchanged keeps the metadata byte-identical.
        /// </summary>
        public byte[] metadataSection { get; private set; }
        public List<KeyValuePair<string, string>> metadata { get; private set; }
        public List<FileEntry> files { get; private set; }

        public long headerLength { get; private set; }      // Where the file data starts
        public long fileDataLength { get; private set; }
        public long packageLength { get; private set; }

        public class DamagedException : Exception
        {
            public DamagedException(string message) : base(message) { }
        }

        SngFile() { }

        public static SngFile Read(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream);
            }
        }

        /// <summary>
        /// Reads the header, metadata and file index. The files' contents are left unread.
        /// </summary>
        /// <exception cref="DamagedException">The stream isn't a .sng this can read, or its index points past its end.</exception>
        public static SngFile Read(Stream stream)
        {
            SngFile sng = new SngFile();
            sng.packageLength = stream.Length;
            stream.Position = 0;

            byte[] identifier = ReadExactly(stream, c_identifier.Length, "the header");
            for (int i = 0; i < c_identifier.Length; ++i)
            {
                if (identifier[i] != c_identifier[i])
                    throw new DamagedException("This file isn't a .sng song package.");
            }

            uint version = BitConverter.ToUInt32(ReadExactly(stream, 4, "the header"), 0);
            if (version != SupportedVersion)
                throw new DamagedException(string.Format("This .sng uses version {0} of the format. Moonscraper can only open version {1}.", version, SupportedVersion));

            sng.xorMask = ReadExactly(stream, XorMaskLength, "the header");

            sng.metadataSection = ReadSection(stream, "the metadata");
            sng.metadata = ParseMetadata(sng.metadataSection);

            byte[] fileIndex = ReadSection(stream, "the file index");
            sng.files = ParseFileIndex(fileIndex);

            sng.fileDataLength = ReadInt64(stream, "the file data length");
            sng.headerLength = stream.Position;

            long totalFileLength = 0;
            foreach (FileEntry file in sng.files)
            {
                if (file.offset < sng.headerLength || file.length > sng.packageLength - file.offset)
                    throw new DamagedException(string.Format("This .sng is damaged: \"{0}\" ends past the end of the .sng.", file.name));

                totalFileLength += file.length;
            }

            if (totalFileLength > sng.fileDataLength)
                throw new DamagedException("This .sng is damaged: its files are longer than its file data.");

            return sng;
        }

        public FileEntry FindFile(string name)
        {
            return files.Find(file => file.name == name);
        }

        /// <summary>
        /// Whether the files are stored back to back in index order right after the header, which every writer and parse-sng expect.
        /// Only then can the last file be rewritten in place.
        /// </summary>
        public bool hasContiguousFileData
        {
            get
            {
                long position = headerLength;
                foreach (FileEntry file in files)
                {
                    if (file.offset != position)
                        return false;

                    position += file.length;
                }

                return position - headerLength == fileDataLength && position == packageLength;
            }
        }

        #region Masking

        /// <summary>
        /// The masking key repeats every 256 bytes: xorMask[i % 16] ^ (i & 0xFF), where i is the byte's position within its file.
        /// </summary>
        public static byte[] CreateKeyTable(byte[] xorMask)
        {
            byte[] keyTable = new byte[256];
            for (int i = 0; i < keyTable.Length; ++i)
            {
                keyTable[i] = (byte)(xorMask[i % XorMaskLength] ^ i);
            }

            return keyTable;
        }

        /// <summary>
        /// Masks or unmasks bytes in place. Masking and unmasking are the same operation.
        /// </summary>
        /// <param name="filePosition">The position of buffer[offset] within its file</param>
        public static void ApplyMask(byte[] keyTable, byte[] buffer, int offset, int count, long filePosition)
        {
            int keyIndex = (int)(filePosition & 0xFF);
            for (int i = offset, end = offset + count; i < end; ++i)
            {
                buffer[i] ^= keyTable[keyIndex];
                keyIndex = (keyIndex + 1) & 0xFF;
            }
        }

        public static byte[] CreateXorMask()
        {
            byte[] xorMask = new byte[XorMaskLength];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                random.GetBytes(xorMask);
            }

            return xorMask;
        }

        public byte[] ReadFile(Stream package, FileEntry file)
        {
            if (file.length > int.MaxValue)
                throw new IOException(string.Format("\"{0}\" is too large to read into memory.", file.name));

            using (MemoryStream output = new MemoryStream((int)file.length))
            {
                UnmaskFileTo(package, file, output);
                return output.ToArray();
            }
        }

        public void UnmaskFileTo(Stream package, FileEntry file, Stream output)
        {
            byte[] keyTable = CreateKeyTable(xorMask);
            byte[] buffer = new byte[CopyBufferSize];
            package.Position = file.offset;

            for (long position = 0; position < file.length;)
            {
                int count = package.Read(buffer, 0, (int)Math.Min(buffer.Length, file.length - position));
                if (count <= 0)
                    throw new EndOfStreamException(string.Format("\"{0}\" ends early.", file.name));

                ApplyMask(keyTable, buffer, 0, count, position);
                output.Write(buffer, 0, count);
                position += count;
            }
        }

        #endregion

        #region Writing

        public const int CopyBufferSize = 1024 * 1024;

        public static byte[] CreateMetadataSection(IList<KeyValuePair<string, string>> metadata)
        {
            using (MemoryStream section = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(section))
            {
                writer.Write((ulong)metadata.Count);
                foreach (var pair in metadata)
                {
                    WriteString(writer, pair.Key);
                    WriteString(writer, pair.Value);
                }

                writer.Flush();
                return section.ToArray();
            }
        }

        /// <summary>
        /// The header, metadata, file index and file data length for files stored back to back in the given order.
        /// </summary>
        /// <param name="files">Each file's name and length. Their offsets are set here.</param>
        public static byte[] CreateHeader(byte[] xorMask, byte[] metadataSection, IList<FileEntry> files)
        {
            using (MemoryStream fileIndex = new MemoryStream())
            using (BinaryWriter indexWriter = new BinaryWriter(fileIndex))
            {
                long headerLength = c_identifier.Length + 4 + XorMaskLength + 8 + metadataSection.Length + 8 + 8 + 8;
                var names = new List<byte[]>(files.Count);
                foreach (FileEntry file in files)
                {
                    byte[] name = c_utf8.GetBytes(file.name);
                    if (name.Length > MaxFilenameBytes)
                        throw new IOException(string.Format("The file name \"{0}\" is too long for a .sng.", file.name));

                    names.Add(name);
                    headerLength += 1 + name.Length + 8 + 8;
                }

                long offset = headerLength;
                indexWriter.Write((ulong)files.Count);
                for (int i = 0; i < files.Count; ++i)
                {
                    files[i].offset = offset;
                    indexWriter.Write((byte)names[i].Length);
                    indexWriter.Write(names[i]);
                    indexWriter.Write((ulong)files[i].length);
                    indexWriter.Write((ulong)files[i].offset);
                    offset += files[i].length;
                }

                indexWriter.Flush();

                using (MemoryStream header = new MemoryStream((int)headerLength))
                using (BinaryWriter writer = new BinaryWriter(header))
                {
                    writer.Write(c_identifier);
                    writer.Write(SupportedVersion);
                    writer.Write(xorMask);
                    writer.Write((ulong)metadataSection.Length);
                    writer.Write(metadataSection);
                    writer.Write((ulong)fileIndex.Length);
                    writer.Write(fileIndex.ToArray());
                    writer.Write((ulong)(offset - headerLength));
                    writer.Flush();

                    return header.ToArray();
                }
            }
        }

        #endregion

        #region Parsing helpers

        static byte[] ReadExactly(Stream stream, long count, string part)
        {
            if (count < 0 || count > stream.Length - stream.Position)
                throw new DamagedException(string.Format("This .sng is damaged: {0} is longer than the file.", part));

            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, (int)count - read);
                if (n <= 0)
                    throw new DamagedException(string.Format("This .sng is damaged: {0} is longer than the file.", part));

                read += n;
            }

            return buffer;
        }

        static long ReadInt64(Stream stream, string part)
        {
            ulong value = BitConverter.ToUInt64(ReadExactly(stream, 8, part), 0);
            if (value > long.MaxValue)
                throw new DamagedException(string.Format("This .sng is damaged: {0} is longer than the file.", part));

            return (long)value;
        }

        static byte[] ReadSection(Stream stream, string part)
        {
            return ReadExactly(stream, ReadInt64(stream, part), part);
        }

        static List<KeyValuePair<string, string>> ParseMetadata(byte[] section)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(section)))
            {
                ulong count = ReadSectionUInt64(reader, "the metadata");
                var metadata = new List<KeyValuePair<string, string>>();

                for (ulong i = 0; i < count; ++i)
                {
                    string key = ReadSectionString(reader, ReadSectionInt32(reader, "the metadata"), "the metadata");
                    string value = ReadSectionString(reader, ReadSectionInt32(reader, "the metadata"), "the metadata");
                    metadata.Add(new KeyValuePair<string, string>(key, value));
                }

                return metadata;
            }
        }

        static List<FileEntry> ParseFileIndex(byte[] section)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(section)))
            {
                ulong count = ReadSectionUInt64(reader, "the file index");
                var files = new List<FileEntry>();

                for (ulong i = 0; i < count; ++i)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        throw new DamagedException("This .sng is damaged: the file index ends early.");

                    string name = ReadSectionString(reader, reader.ReadByte(), "the file index");
                    ulong length = ReadSectionUInt64(reader, "the file index");
                    ulong offset = ReadSectionUInt64(reader, "the file index");

                    if (length > long.MaxValue || offset > long.MaxValue)
                        throw new DamagedException(string.Format("This .sng is damaged: \"{0}\" ends past the end of the .sng.", name));

                    files.Add(new FileEntry() { name = name, length = (long)length, offset = (long)offset });
                }

                return files;
            }
        }

        static ulong ReadSectionUInt64(BinaryReader reader, string part)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 8)
                throw new DamagedException(string.Format("This .sng is damaged: {0} ends early.", part));

            return reader.ReadUInt64();
        }

        static int ReadSectionInt32(BinaryReader reader, string part)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 4)
                throw new DamagedException(string.Format("This .sng is damaged: {0} ends early.", part));

            return reader.ReadInt32();
        }

        static string ReadSectionString(BinaryReader reader, int length, string part)
        {
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new DamagedException(string.Format("This .sng is damaged: {0} ends early.", part));

            return c_utf8.GetString(reader.ReadBytes(length));
        }

        static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = c_utf8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        #endregion
    }
}
