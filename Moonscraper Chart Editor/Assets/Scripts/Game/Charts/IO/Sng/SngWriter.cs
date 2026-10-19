// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace MoonscraperChartEditor.Song.IO
{
    /// <summary>
    /// One file to write into a .sng, and where its contents come from.
    /// </summary>
    public class SngFileContents
    {
        public readonly string name;
        public readonly long length;

        readonly SngFile.FileEntry m_packagedFile;
        readonly byte[] m_bytes;
        readonly string m_diskFile;

        SngFileContents(string name, long length, SngFile.FileEntry packagedFile, byte[] bytes, string diskFile)
        {
            this.name = name;
            this.length = length;
            m_packagedFile = packagedFile;
            m_bytes = bytes;
            m_diskFile = diskFile;
        }

        /// <summary>
        /// A file kept from the .sng being written over, copied as stored. The new .sng must use the same xorMask.
        /// </summary>
        public static SngFileContents Packaged(SngFile.FileEntry file)
        {
            return new SngFileContents(file.name, file.length, file, null, null);
        }

        public static SngFileContents FromBytes(string name, byte[] contents)
        {
            return new SngFileContents(name, contents.Length, null, contents, null);
        }

        public static SngFileContents FromDiskFile(string name, string path)
        {
            return new SngFileContents(name, new FileInfo(path).Length, null, null, path);
        }

        public bool isPackaged { get { return m_packagedFile != null; } }
        public SngFile.FileEntry packagedFile { get { return m_packagedFile; } }
        public byte[] bytes { get { return m_bytes; } }

        public void WriteMaskedTo(Stream output, Stream sourcePackage, byte[] keyTable, byte[] buffer)
        {
            if (m_packagedFile != null)
            {
                sourcePackage.Position = m_packagedFile.offset;
                CopyExactly(sourcePackage, output, length, buffer, null);
            }
            else if (m_bytes != null)
            {
                for (int position = 0; position < m_bytes.Length; position += buffer.Length)
                {
                    int count = Math.Min(buffer.Length, m_bytes.Length - position);
                    Buffer.BlockCopy(m_bytes, position, buffer, 0, count);
                    SngFile.ApplyMask(keyTable, buffer, 0, count, position);
                    output.Write(buffer, 0, count);
                }
            }
            else
            {
                using (FileStream input = new FileStream(m_diskFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (input.Length != length)
                        throw new IOException(string.Format("\"{0}\" changed while saving.", m_diskFile));

                    CopyExactly(input, output, length, buffer, keyTable);
                }
            }
        }

        static void CopyExactly(Stream input, Stream output, long length, byte[] buffer, byte[] keyTable)
        {
            for (long position = 0; position < length;)
            {
                int count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, length - position));
                if (count <= 0)
                    throw new EndOfStreamException("A file ended early while saving.");

                if (keyTable != null)
                    SngFile.ApplyMask(keyTable, buffer, 0, count, position);

                output.Write(buffer, 0, count);
                position += count;
            }
        }
    }

    /// <summary>
    /// Byte ranges to write over an existing file, and the file's length afterwards.
    /// </summary>
    public class SngPatch
    {
        public long length;
        public List<KeyValuePair<long, byte[]>> writes = new List<KeyValuePair<long, byte[]>>();

        public void ApplyTo(FileStream file)
        {
            file.SetLength(length);
            foreach (var write in writes)
            {
                file.Position = write.Key;
                file.Write(write.Value, 0, write.Value.Length);
            }

            file.Flush(true);
        }
    }

    public static class SngWriter
    {
        /// <summary>
        /// Writes a whole .sng with the files back to back in the given order.
        /// </summary>
        /// <param name="sourcePackage">The .sng that packaged files are copied from, or null if there are none</param>
        public static void Write(Stream output, Stream sourcePackage, byte[] xorMask, byte[] metadataSection, IList<SngFileContents> files)
        {
            var entries = new List<SngFile.FileEntry>(files.Count);
            foreach (SngFileContents file in files)
            {
                entries.Add(new SngFile.FileEntry() { name = file.name, length = file.length });
            }

            byte[] header = SngFile.CreateHeader(xorMask, metadataSection, entries);
            output.Write(header, 0, header.Length);

            byte[] keyTable = SngFile.CreateKeyTable(xorMask);
            byte[] buffer = new byte[SngFile.CopyBufferSize];
            foreach (SngFileContents file in files)
            {
                file.WriteMaskedTo(output, sourcePackage, keyTable, buffer);
            }
        }

        /// <summary>
        /// The writes that turn a .sng into the given one when they differ only in the last file's contents, so nothing else moves.
        /// </summary>
        /// <param name="files">The files of the .sng to write. Every file but the last must be a packaged file of current, and the last must be given as bytes.</param>
        /// <returns>Null if anything else differs: the metadata, any other file, the order, or a layout that isn't back to back.</returns>
        public static SngPatch CreateLastFilePatch(SngFile current, byte[] metadataSection, IList<SngFileContents> files)
        {
            if (!current.hasContiguousFileData || files.Count == 0 || files.Count != current.files.Count || !BytesEqual(metadataSection, current.metadataSection))
                return null;

            int last = files.Count - 1;
            byte[] lastFileContents = files[last].bytes;
            if (lastFileContents == null || files[last].name != current.files[last].name)
                return null;

            var entries = new List<SngFile.FileEntry>(files.Count);
            for (int i = 0; i < last; ++i)
            {
                SngFile.FileEntry currentFile = current.files[i];
                if (!files[i].isPackaged || files[i].packagedFile != currentFile)
                    return null;

                entries.Add(new SngFile.FileEntry() { name = currentFile.name, length = currentFile.length });
            }

            entries.Add(new SngFile.FileEntry() { name = files[last].name, length = lastFileContents.Length });

            byte[] header = SngFile.CreateHeader(current.xorMask, metadataSection, entries);
            if (header.Length != current.headerLength)
                return null;

            byte[] maskedContents = (byte[])lastFileContents.Clone();
            SngFile.ApplyMask(SngFile.CreateKeyTable(current.xorMask), maskedContents, 0, maskedContents.Length, 0);

            SngPatch patch = new SngPatch() { length = entries[last].offset + maskedContents.Length };
            patch.writes.Add(new KeyValuePair<long, byte[]>(0, header));
            patch.writes.Add(new KeyValuePair<long, byte[]>(entries[last].offset, maskedContents));

            return patch;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}
