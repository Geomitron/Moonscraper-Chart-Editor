// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MoonscraperChartEditor.Song.IO
{
    /// <summary>
    /// Saves into a .sng so that a crash at any moment leaves it either as it was or fully saved.
    /// Writes in place are recorded in a journal, flushed to disk before the .sng is touched, and finished when Moonscraper next starts if they were interrupted.
    /// A whole new .sng is written beside the old one and swapped in once complete; the journal records it so a leftover copy can be removed.
    /// </summary>
    public static class SngSaveJournal
    {
        static readonly byte[] c_identifier = Encoding.ASCII.GetBytes("MSCESNGJ");
        const int c_version = 1;
        const int c_hashLength = 32;
        static readonly UTF8Encoding c_utf8 = new UTF8Encoding(false);

        enum Kind : byte
        {
            Patch = 0,      // Writes to finish in the .sng
            Swap = 1,       // A new .sng beside the .sng, to remove if it wasn't swapped in
        }

        /// <summary>
        /// Writes a patch into a .sng in place.
        /// </summary>
        /// <param name="expectedLength">The .sng's length the patch was made for. A different length means the .sng changed since, and nothing is written.</param>
        public static void ApplyPatch(string journalPath, string packagePath, long expectedLength, SngPatch patch)
        {
            // Opening the .sng first makes a save fail before anything is written if another program, such as Clone Hero, has it open
            using (FileStream package = new FileStream(packagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                if (package.Length != expectedLength)
                    throw new IOException("The .sng changed on disk while saving.");

                using (FileStream journal = CreateJournal(journalPath, Kind.Patch, packagePath, expectedLength, patch, null))
                {
                    patch.ApplyTo(package);
                }
            }

            File.Delete(journalPath);
        }

        /// <summary>
        /// Writes a whole .sng beside packagePath, then replaces packagePath with it.
        /// </summary>
        /// <param name="writePackage">Writes the new .sng's contents. It must close any stream it holds on packagePath before returning.</param>
        public static void WriteAndSwap(string journalPath, string packagePath, Action<FileStream> writePackage)
        {
            // A short name, so a .sng named as long as the file system allows still has room beside it
            string newPackagePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(packagePath)), Guid.NewGuid().ToString("N").Substring(0, 8) + ".saving");

            // For a swap, the expected length only records whether there was a .sng to replace
            using (FileStream journal = CreateJournal(journalPath, Kind.Swap, packagePath, File.Exists(packagePath) ? 1 : 0, null, newPackagePath))
            {
                try
                {
                    using (FileStream newPackage = new FileStream(newPackagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        writePackage(newPackage);
                        newPackage.Flush(true);
                    }

                    if (File.Exists(packagePath))
                        File.Replace(newPackagePath, packagePath, null);
                    else
                        File.Move(newPackagePath, packagePath);
                }
                catch
                {
                    TryDelete(newPackagePath);
                    journal.Dispose();

                    // The journal stays to remove the new copy later if it couldn't be removed now
                    if (!File.Exists(newPackagePath))
                        TryDelete(journalPath);

                    throw;
                }
            }

            File.Delete(journalPath);
        }

        /// <summary>
        /// Finishes or cleans up a save that was interrupted, such as by a crash. Does nothing if there is no journal, or if another Moonscraper is still saving.
        /// </summary>
        /// <returns>What went wrong, if the .sng couldn't be finished. Otherwise null.</returns>
        public static string Recover(string journalPath)
        {
            Kind kind;
            string packagePath;
            long expectedLength;
            SngPatch patch;
            string newPackagePath;

            try
            {
                if (!File.Exists(journalPath))
                    return null;

                // A save still in progress keeps its journal open
                using (FileStream journal = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (!TryReadJournal(journal, out kind, out packagePath, out expectedLength, out patch, out newPackagePath))
                    {
                        // Interrupted while writing the journal, before the .sng was touched
                        kind = Kind.Swap;
                        packagePath = null;
                        newPackagePath = null;
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }

            if (kind == Kind.Swap)
            {
                // The swap either happened or didn't, so the .sng is whole either way.
                // If a .sng that was there is now missing, the new copy may be all that's left of it, so it stays.
                if (newPackagePath != null && (File.Exists(packagePath) || expectedLength == 0))
                    TryDelete(newPackagePath);

                TryDelete(journalPath);
                return null;
            }

            try
            {
                using (FileStream package = new FileStream(packagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // Applying a patch sets the length first, so any other length means something else changed the .sng
                    if (package.Length == expectedLength || package.Length == patch.length)
                        patch.ApplyTo(package);
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception e)
            {
                return string.Format("Moonscraper was interrupted while saving \"{0}\", and couldn't finish the save: {1}" + Environment.NewLine + "It will try again the next time it starts. Until then, the .sng may not open.", packagePath, e.Message);
            }

            TryDelete(journalPath);
            return null;
        }

        static FileStream CreateJournal(string journalPath, Kind kind, string packagePath, long expectedLength, SngPatch patch, string newPackagePath)
        {
            FileStream journal = new FileStream(journalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            try
            {
                using (MemoryStream contents = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(contents))
                {
                    writer.Write(c_identifier);
                    writer.Write(c_version);
                    writer.Write((byte)kind);
                    WriteString(writer, Path.GetFullPath(packagePath));

                    writer.Write(expectedLength);

                    if (kind == Kind.Patch)
                    {
                        writer.Write(patch.length);
                        writer.Write(patch.writes.Count);
                        foreach (var write in patch.writes)
                        {
                            writer.Write(write.Key);
                            writer.Write(write.Value.Length);
                            writer.Write(write.Value);
                        }
                    }
                    else
                    {
                        WriteString(writer, Path.GetFullPath(newPackagePath));
                    }

                    writer.Flush();

                    // The journal only counts once its hash is written, so a journal cut short is never applied
                    byte[] bytes = contents.ToArray();
                    byte[] hash;
                    using (SHA256 sha = SHA256.Create())
                    {
                        hash = sha.ComputeHash(bytes);
                    }

                    journal.Write(bytes, 0, bytes.Length);
                    journal.Write(hash, 0, hash.Length);
                    journal.Flush(true);
                }

                return journal;
            }
            catch
            {
                journal.Dispose();
                TryDelete(journalPath);
                throw;
            }
        }

        static bool TryReadJournal(FileStream journal, out Kind kind, out string packagePath, out long expectedLength, out SngPatch patch, out string newPackagePath)
        {
            kind = Kind.Swap;
            packagePath = null;
            expectedLength = 0;
            patch = null;
            newPackagePath = null;

            if (journal.Length < c_identifier.Length + c_hashLength || journal.Length > int.MaxValue)
                return false;

            byte[] bytes = new byte[journal.Length];
            for (int read = 0; read < bytes.Length;)
            {
                int count = journal.Read(bytes, read, bytes.Length - read);
                if (count <= 0)
                    return false;

                read += count;
            }

            int contentsLength = bytes.Length - c_hashLength;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes, 0, contentsLength);
                for (int i = 0; i < c_hashLength; ++i)
                {
                    if (hash[i] != bytes[contentsLength + i])
                        return false;
                }
            }

            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes, 0, contentsLength)))
            {
                byte[] identifier = reader.ReadBytes(c_identifier.Length);
                for (int i = 0; i < c_identifier.Length; ++i)
                {
                    if (identifier[i] != c_identifier[i])
                        return false;
                }

                if (reader.ReadInt32() != c_version)
                    return false;

                kind = (Kind)reader.ReadByte();
                packagePath = ReadString(reader);
                expectedLength = reader.ReadInt64();

                if (kind == Kind.Patch)
                {
                    patch = new SngPatch() { length = reader.ReadInt64() };
                    int writeCount = reader.ReadInt32();
                    for (int i = 0; i < writeCount; ++i)
                    {
                        long offset = reader.ReadInt64();
                        byte[] data = reader.ReadBytes(reader.ReadInt32());
                        patch.writes.Add(new KeyValuePair<long, byte[]>(offset, data));
                    }
                }
                else
                {
                    newPackagePath = ReadString(reader);
                }
            }

            return true;
        }

        static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = c_utf8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        static string ReadString(BinaryReader reader)
        {
            return c_utf8.GetString(reader.ReadBytes(reader.ReadInt32()));
        }

        static void TryDelete(string path)
        {
            try
            {
                if (path != null && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }
}
