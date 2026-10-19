// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song.IO
{
    /// <summary>
    /// A song opened from a .sng, or saved into one. Remembers what came from the .sng, so a save changes only what the user changed.
    /// The chart, audio and metadata are found the way Clone Hero finds them in a .sng: by exact file name, and the metadata only from the .sng's metadata section.
    /// </summary>
    public class SngSong
    {
        public const string ChartFilename = "notes.chart";
        public const string MidiFilename = "notes.mid";

        // Clone Hero reads these with bool.TryParse, which only accepts true and false
        static readonly HashSet<string> c_boolMetadataKeys = new HashSet<string>() { "modchart", "pro_drums", "five_lane_drums", "explicit_lyrics", "eighthnote_hopo" };

        // The registered names a song folder is packed with, besides the chart and song.ini. Audio is also found through CloneHeroAudioFiles.
        static readonly string[] c_packedAudioNames = { "song", "guitar", "bass", "rhythm", "keys", "vocals", "vocals_1", "vocals_2", "drums", "drums_1", "drums_2", "drums_3", "drums_4", "crowd", "preview" };
        static readonly string[] c_packedAudioExtensions = { ".opus", ".ogg", ".mp3", ".wav" };
        static readonly string[] c_packedImageNames = { "album", "background", "highway" };
        static readonly string[] c_packedImageExtensions = { ".png", ".jpg", ".jpeg" };
        static readonly string[] c_packedVideoExtensions = { ".mp4", ".avi", ".webm", ".vp8", ".ogv", ".mpeg" };

        /// <summary>
        /// A file in the .sng that gives an audio instrument its audio, and the song's audio location with the same contents.
        /// A different location means the user chose other audio.
        /// </summary>
        class PackagedAudio
        {
            public string filename;
            public string location;
        }

        public string path { get; private set; }

        /// <summary>
        /// The .sng holds notes.mid, so it is saved as a new .sng holding notes.chart.
        /// </summary>
        public bool chartIsMidi { get; private set; }

        readonly List<KeyValuePair<string, string>> m_metadata;
        readonly Dictionary<Song.AudioInstrument, PackagedAudio> m_audio;

        public class SaveException : Exception
        {
            public SaveException(string message) : base(message) { }
        }

        SngSong(string path, bool chartIsMidi, List<KeyValuePair<string, string>> metadata, Dictionary<Song.AudioInstrument, PackagedAudio> audio)
        {
            this.path = path;
            this.chartIsMidi = chartIsMidi;
            m_metadata = metadata;
            m_audio = audio;
        }

        public static bool IsSngPath(string path)
        {
            return string.Equals(Path.GetExtension(path), SngFile.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        #region Opening

        /// <summary>
        /// Opens a song from a .sng. Its audio is unpacked into unpackDirectory, which the song's audio locations point to.
        /// </summary>
        public static SngSong Open(string path, string unpackDirectory, ref MidReader.CallbackState callBackState, out Song song)
        {
            path = Path.GetFullPath(path);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SngFile package = SngFile.Read(stream);

                SngFile.FileEntry chart = package.FindFile(MidiFilename) ?? package.FindFile(ChartFilename);
                if (chart == null)
                    throw new Exception("This .sng has no notes.chart or notes.mid.");

                bool chartIsMidi = chart.name == MidiFilename;
                byte[] chartContents = package.ReadFile(stream, chart);
                song = chartIsMidi ? MidReader.ReadMidi(chartContents, ref callBackState) : ChartReader.ReadChart(chartContents, Path.GetDirectoryName(path));

                song.iniProperties = IniFromMetadata(package.metadata);

                var audio = UnpackAudio(package, stream, unpackDirectory);

                // Clone Hero ignores audio file names saved in a chart
                foreach (Song.AudioInstrument audioInstrument in EnumX<Song.AudioInstrument>.Values)
                {
                    PackagedAudio packagedAudio;
                    song.SetAudioLocation(audioInstrument, audio.TryGetValue(audioInstrument, out packagedAudio) ? packagedAudio.location : string.Empty);
                }

                return new SngSong(path, chartIsMidi, NormaliseLoadedMetadata(package.metadata), audio);
            }
        }

        static Dictionary<Song.AudioInstrument, PackagedAudio> UnpackAudio(SngFile package, Stream stream, string unpackDirectory)
        {
            var audio = new Dictionary<Song.AudioInstrument, PackagedAudio>();
            var filenames = new List<string>();
            foreach (SngFile.FileEntry file in package.files)
            {
                filenames.Add(file.name);
            }

            foreach (var audioFile in CloneHeroAudioFiles.FindPackagedAudioFiles(filenames))
            {
                SngFile.FileEntry file = package.FindFile(audioFile.Value);
                string location = Path.GetFullPath(Path.Combine(unpackDirectory, file.name));

                using (FileStream output = new FileStream(location, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    package.UnmaskFileTo(stream, file, output);
                }

                audio.Add(audioFile.Key, new PackagedAudio() { filename = file.name, location = location });
            }

            return audio;
        }

        #endregion

        #region Saving

        /// <summary>
        /// Saves the song into this .sng, or into a new .sng at targetPath.
        /// The chart becomes notes.chart, the last file, so the next save only rewrites the end of the .sng.
        /// </summary>
        /// <returns>The saved .sng</returns>
        /// <exception cref="SaveException">The song can't be saved into a .sng. Nothing was written.</exception>
        public SngSong Save(Song song, ExportOptions exportOptions, string targetPath, string journalPath)
        {
            targetPath = Path.GetFullPath(targetPath);
            RecoverInterruptedSave(journalPath);

            byte[] chart = CreateChart(song, exportOptions, targetPath);
            var metadata = MetadataFromIni(song.iniProperties);

            using (FileStream sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SngFile package = SngFile.Read(sourceStream);
                byte[] metadataSection = MetadataEquals(metadata, m_metadata) ? package.metadataSection : CreateMetadataSection(metadata);

                var files = new List<SngFileContents>();
                foreach (SngFile.FileEntry file in package.files)
                {
                    if (file.name != ChartFilename && file.name != MidiFilename)
                        files.Add(SngFileContents.Packaged(file));
                }

                var audio = ApplyAudio(song, files, m_audio);
                files.Add(SngFileContents.FromBytes(ChartFilename, chart));

                SngPatch patch = PathsEqual(targetPath, path) ? SngWriter.CreateLastFilePatch(package, metadataSection, files) : null;
                if (patch != null)
                {
                    sourceStream.Dispose();
                    SngSaveJournal.ApplyPatch(journalPath, targetPath, package.packageLength, patch);
                }
                else
                {
                    SngSaveJournal.WriteAndSwap(journalPath, targetPath, output =>
                    {
                        SngWriter.Write(output, sourceStream, package.xorMask, metadataSection, files);
                        sourceStream.Dispose();
                    });
                }

                return new SngSong(targetPath, false, metadata, audio);
            }
        }

        /// <summary>
        /// Packs a song from a folder into a new .sng: the chart, song.ini as the metadata, and the folder's audio, images and video.
        /// </summary>
        /// <param name="songDirectory">The song's folder, or empty if it has none. Its other files are left out.</param>
        /// <returns>The saved .sng</returns>
        /// <exception cref="SaveException">The song can't be saved into a .sng. Nothing was written.</exception>
        public static SngSong SaveFolder(Song song, ExportOptions exportOptions, string songDirectory, string targetPath, string journalPath)
        {
            targetPath = Path.GetFullPath(targetPath);
            RecoverInterruptedSave(journalPath);

            byte[] chart = CreateChart(song, exportOptions, targetPath);
            var metadata = MetadataFromIni(song.iniProperties);
            byte[] metadataSection = CreateMetadataSection(metadata);

            var files = new List<SngFileContents>();
            var folderAudio = new Dictionary<Song.AudioInstrument, PackagedAudio>();

            if (!string.IsNullOrEmpty(songDirectory) && Directory.Exists(songDirectory))
            {
                var packedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddNames(packedNames, c_packedAudioNames, c_packedAudioExtensions);
                AddNames(packedNames, c_packedImageNames, c_packedImageExtensions);
                AddNames(packedNames, new string[] { "video" }, c_packedVideoExtensions);

                var folderFiles = new List<string>(Directory.GetFiles(songDirectory));
                folderFiles.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string file in folderFiles)
                {
                    string filename = Path.GetFileName(file);
                    if (packedNames.Contains(filename))
                        files.Add(SngFileContents.FromDiskFile(filename.ToLowerInvariant(), file));
                }

                foreach (var audioFile in CloneHeroAudioFiles.FindAudioFiles(songDirectory))
                {
                    folderAudio.Add(audioFile.Key, new PackagedAudio() { filename = Path.GetFileName(audioFile.Value).ToLowerInvariant(), location = audioFile.Value });
                }
            }

            var audio = ApplyAudio(song, files, folderAudio);
            files.Add(SngFileContents.FromBytes(ChartFilename, chart));

            SngSaveJournal.WriteAndSwap(journalPath, targetPath, output =>
            {
                SngWriter.Write(output, null, SngFile.CreateXorMask(), metadataSection, files);
            });

            return new SngSong(targetPath, false, metadata, audio);
        }

        static void RecoverInterruptedSave(string journalPath)
        {
            string error = SngSaveJournal.Recover(journalPath);
            if (error != null)
                throw new SaveException(error);
        }

        static byte[] CreateChart(Song song, ExportOptions exportOptions, string targetPath)
        {
            ChartWriter.ErrorReport errorReport;
            string chartText = new ChartWriter(targetPath).CreateChartText(song, exportOptions, out errorReport);

            if (errorReport.resultantFileType == ChartIOHelper.FileSubType.MoonscraperPropriety)
            {
                throw new SaveException("This chart has events that a .chart file can't hold, so it can't be saved into a .sng. Nothing was saved." + Globals.LINE_ENDING + errorReport.GetFullReport());
            }

            return ChartWriter.EncodeChartText(chartText);
        }

        /// <summary>
        /// Updates the files for the song's audio. A file the user replaced goes in under its audio instrument's name, and a cleared audio instrument's files are removed, so Clone Hero plays what Moonscraper plays.
        /// </summary>
        /// <returns>The audio of the .sng that will be written</returns>
        static Dictionary<Song.AudioInstrument, PackagedAudio> ApplyAudio(Song song, List<SngFileContents> files, Dictionary<Song.AudioInstrument, PackagedAudio> packagedAudio)
        {
            var audio = new Dictionary<Song.AudioInstrument, PackagedAudio>();

            foreach (Song.AudioInstrument audioInstrument in EnumX<Song.AudioInstrument>.Values)
            {
                string location = song.GetAudioLocation(audioInstrument);
                PackagedAudio packaged;
                packagedAudio.TryGetValue(audioInstrument, out packaged);

                if (packaged != null && PathsEqual(location, packaged.location))
                {
                    audio.Add(audioInstrument, packaged);
                    continue;
                }

                if (packaged == null && location == string.Empty)
                    continue;

                IEnumerable<string> oldNames;
                string name = packaged != null ? Path.GetFileNameWithoutExtension(packaged.filename) : CloneHeroAudioFiles.GetFilename(audioInstrument);
                if (location == string.Empty)
                    oldNames = CloneHeroAudioFiles.GetFilenames(audioInstrument);
                else
                    oldNames = new string[] { name };

                files.RemoveAll(file => CloneHeroAudioFiles.HasAudioFilename(file.name, oldNames));

                if (location != string.Empty)
                {
                    string filename = name + Path.GetExtension(location).ToLowerInvariant();
                    files.Add(SngFileContents.FromDiskFile(filename, location));
                    audio.Add(audioInstrument, new PackagedAudio() { filename = filename, location = location });
                }
            }

            return audio;
        }

        static void AddNames(HashSet<string> names, string[] baseNames, string[] extensions)
        {
            foreach (string baseName in baseNames)
            {
                foreach (string extension in extensions)
                {
                    names.Add(baseName + extension);
                }
            }
        }

        static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);

            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Metadata

        // Song Properties shows the metadata as song.ini text
        static INIParser IniFromMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            StringBuilder text = new StringBuilder();
            foreach (var pair in metadata)
            {
                text.Append(pair.Key).Append(" = ").Append(pair.Value).Append('\n');
            }

            return SongIniFunctions.SongIniFromString(text.ToString());
        }

        /// <summary>
        /// The metadata to save, in the order Song Properties shows it. Clone Hero's true/false settings are written as True or False, and empty values are left out, as SngCli does.
        /// </summary>
        static List<KeyValuePair<string, string>> MetadataFromIni(INIParser ini)
        {
            var metadata = new List<KeyValuePair<string, string>>();
            var keys = new HashSet<string>();

            foreach (string line in SongIniFunctions.IniTextFromSongProperties(ini).Split('\n'))
            {
                string[] keyValue = line.Split(new char[] { '=' }, 2);
                string key = keyValue[0].Trim();
                string value = keyValue.Length > 1 ? keyValue[1].Trim() : string.Empty;

                if (key == string.Empty || value == string.Empty || !keys.Add(key))
                    continue;

                if (c_boolMetadataKeys.Contains(key))
                {
                    if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
                        value = "True";
                    else if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
                        value = "False";
                }

                metadata.Add(new KeyValuePair<string, string>(key, value));
            }

            return metadata;
        }

        // The loaded metadata as it reads back from Song Properties, to tell whether the user changed it
        static List<KeyValuePair<string, string>> NormaliseLoadedMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            return MetadataFromIni(IniFromMetadata(metadata));
        }

        static bool MetadataEquals(List<KeyValuePair<string, string>> a, List<KeyValuePair<string, string>> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; ++i)
            {
                if (a[i].Key != b[i].Key || a[i].Value != b[i].Value)
                    return false;
            }

            return true;
        }

        static byte[] CreateMetadataSection(List<KeyValuePair<string, string>> metadata)
        {
            // Semicolons start comments in song.ini, which .sng metadata must round-trip to
            foreach (var pair in metadata)
            {
                if (pair.Key.Contains(";"))
                    throw new SaveException(string.Format("The song.ini key \"{0}\" has a semicolon, which a .sng can't hold. Remove it in Song Properties, then save again.", pair.Key));

                if (pair.Value.Contains(";"))
                    throw new SaveException(string.Format("The song.ini value of \"{0}\" has a semicolon, which a .sng can't hold. Remove it in Song Properties, then save again.", pair.Key));
            }

            return SngFile.CreateMetadataSection(metadata);
        }

        #endregion

        #region Autosave

        /// <summary>
        /// What the autosave needs, besides the chart, to restore a song opened from a .sng after a crash.
        /// </summary>
        [Serializable]
        public class Autosave
        {
            public string path;
            public bool chartIsMidi;
            public string metadata;     // As song.ini text
            public List<AutosaveAudio> audio = new List<AutosaveAudio>();
        }

        [Serializable]
        public class AutosaveAudio
        {
            public string audioInstrument;
            public bool packaged;       // The audio in the .sng
            public string location;     // A file the user chose instead, or empty if the user cleared it
        }

        public Autosave CreateAutosave(Song song)
        {
            Autosave autosave = new Autosave() { path = path, chartIsMidi = chartIsMidi, metadata = SongIniFunctions.IniTextFromSongProperties(song.iniProperties) };

            foreach (Song.AudioInstrument audioInstrument in EnumX<Song.AudioInstrument>.Values)
            {
                string location = song.GetAudioLocation(audioInstrument);
                PackagedAudio packaged;
                bool isPackaged = m_audio.TryGetValue(audioInstrument, out packaged) && PathsEqual(location, packaged.location);

                autosave.audio.Add(new AutosaveAudio() { audioInstrument = audioInstrument.ToString(), packaged = isPackaged, location = isPackaged ? string.Empty : location });
            }

            return autosave;
        }

        /// <summary>
        /// Reopens the .sng an autosave was made from, with the autosaved chart, metadata and audio choices.
        /// </summary>
        /// <param name="song">The song read from the autosaved chart. Its metadata and audio are set from the autosave.</param>
        public static SngSong Recover(Autosave autosave, string unpackDirectory, Song song)
        {
            string path = Path.GetFullPath(autosave.path);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SngFile package = SngFile.Read(stream);
                var audio = UnpackAudio(package, stream, unpackDirectory);

                song.iniProperties = SongIniFunctions.SongIniFromString(autosave.metadata);

                foreach (Song.AudioInstrument audioInstrument in EnumX<Song.AudioInstrument>.Values)
                {
                    AutosaveAudio autosaveAudio = autosave.audio.Find(a => a.audioInstrument == audioInstrument.ToString());
                    PackagedAudio packaged;
                    audio.TryGetValue(audioInstrument, out packaged);

                    // The autosaved chart's audio file names point into an audio cache that no longer exists
                    song.SetAudioLocation(audioInstrument, string.Empty);

                    if (autosaveAudio == null || autosaveAudio.packaged)
                        song.SetAudioLocation(audioInstrument, packaged != null ? packaged.location : string.Empty);
                    else
                        song.SetAudioLocation(audioInstrument, autosaveAudio.location);
                }

                return new SngSong(path, autosave.chartIsMidi, NormaliseLoadedMetadata(package.metadata), audio);
            }
        }

        #endregion
    }
}
