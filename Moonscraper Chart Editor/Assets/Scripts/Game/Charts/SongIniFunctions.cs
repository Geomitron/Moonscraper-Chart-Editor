using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using MoonscraperChartEditor.Song;

public static class SongIniFunctions
{
    public const string INI_FILENAME = "song.ini";

    /// <summary>
    /// Sets the song's song.ini keys from the song.ini opened with it, as <see cref="SongIniKeys.NormalizeOpened"/> reads them. A key
    /// it doesn't set is filled from the chart's [Song] section, as games read [Song] without a song.ini. The song's offset is
    /// song.ini's delay.
    /// </summary>
    /// <param name="iniPairs">The song.ini's [song] lines, or none if the song has no song.ini</param>
    public static void OpenSongIni(Song song, IEnumerable<KeyValuePair<string, string>> iniPairs)
    {
        var pairs = SongIniKeys.NormalizeOpened(iniPairs);
        var setKeys = new HashSet<string>();
        foreach (var pair in pairs)
            setKeys.Add(pair.Key);

        foreach (var pair in ChartSongPairs(song))
        {
            if (!setKeys.Contains(pair.Key))
                pairs.Add(pair);
        }

        pairs = SongIniKeys.NormalizeOpened(pairs);

        song.offset = 0;
        foreach (var pair in pairs)
        {
            float delayMs;
            if (pair.Key == "delay" && float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out delayMs))
                song.offset = delayMs / 1000;
        }

        song.iniProperties = new SongIni(pairs);
    }

    /// <summary>
    /// The song.ini file for a song from a song.ini's text, for <see cref="OpenSongIni"/>.
    /// </summary>
    public static void OpenSongIni(Song song, string iniText)
    {
        OpenSongIni(song, SongIniKeys.Parse(iniText));
    }

    // The [Song] values games read when there's no song.ini, as song.ini keys: times in milliseconds as Clone Hero rounds them
    static List<KeyValuePair<string, string>> ChartSongPairs(Song song)
    {
        Metadata metaData = song.metaData;
        var pairs = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("name", metaData.name),
            new KeyValuePair<string, string>("artist", metaData.artist),
            new KeyValuePair<string, string>("album", metaData.album),
            new KeyValuePair<string, string>("genre", metaData.genre),
            new KeyValuePair<string, string>("year", metaData.year),
            new KeyValuePair<string, string>("charter", metaData.charter),
            new KeyValuePair<string, string>("delay", SecondsToMs(song.offset).ToString(CultureInfo.InvariantCulture)),
        };

        if (metaData.hasDifficulty)
            pairs.Add(new KeyValuePair<string, string>("diff_band", metaData.difficulty.ToString(CultureInfo.InvariantCulture)));

        // Moonscraper wrote a preview start of 0 whether or not the charter chose one, and a preview end of 0 when there was none
        if (metaData.previewStart.HasValue && SecondsToMs(metaData.previewStart.Value) != 0)
            pairs.Add(new KeyValuePair<string, string>("preview_start_time", SecondsToMs(metaData.previewStart.Value).ToString(CultureInfo.InvariantCulture)));
        if (metaData.previewEnd.HasValue && SecondsToMs(metaData.previewEnd.Value) > 0)
            pairs.Add(new KeyValuePair<string, string>("preview_end_time", SecondsToMs(metaData.previewEnd.Value).ToString(CultureInfo.InvariantCulture)));

        return pairs;
    }

    // As Clone Hero reads a [Song] time: (int)Math.Ceiling(float.Parse(value) * 1000), in single precision
    static int SecondsToMs(float seconds)
    {
        float ms = seconds * 1000;
        return (int)System.Math.Ceiling(ms);
    }

    /// <summary>
    /// The song.ini keys a save writes: the song's keys, and its offset as delay.
    /// </summary>
    public static List<KeyValuePair<string, string>> SavedPairs(Song song)
    {
        var pairs = new List<KeyValuePair<string, string>>(song.iniProperties.pairs);
        pairs.RemoveAll(pair => pair.Key == "delay");

        int delayMs = Mathf.RoundToInt(song.offset * 1000);
        if (delayMs != 0)
            pairs.Add(new KeyValuePair<string, string>("delay", delayMs.ToString(CultureInfo.InvariantCulture)));

        return SongIniKeys.Normalize(pairs);
    }

    /// <summary>
    /// The song.ini file a save writes, as <see cref="SavedPairs"/> has it.
    /// </summary>
    public static string SavedText(Song song)
    {
        return SongIniKeys.IniText(SavedPairs(song));
    }

    /// <summary>
    /// Writes the song's song.ini into a folder, replacing the one there. A song without keys has no song.ini, so one there is deleted.
    /// </summary>
    public static void WriteSongIni(Song song, string directory)
    {
        string iniPath = Path.Combine(directory, INI_FILENAME);
        if (SavedPairs(song).Count > 0)
            File.WriteAllText(iniPath, SavedText(song), new UTF8Encoding(false));
        else if (File.Exists(iniPath))
            File.Delete(iniPath);
    }
}
