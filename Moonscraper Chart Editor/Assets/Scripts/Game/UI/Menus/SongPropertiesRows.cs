using System.Collections.Generic;
using MoonscraperEngine;
using MoonscraperChartEditor.Song;

/// <summary>
/// The song.ini keys Song Properties lists, in the spec's order and under its groups, and which of them show for a chart.
/// </summary>
public static class SongPropertiesRows
{
    public enum Input
    {
        Text,
        Integer,
        Seconds,        // Shown in seconds, saved in milliseconds
        Bool,
        Select,
        DrumType,       // five_lane_drums and pro_drums
        Measured,       // song_length, which only the song.ini preview shows
    }

    public enum Shown
    {
        Always,
        Charted,                    // When the row's part has notes
        Band,                       // When more than one part has notes, otherwise with "Show all keys"
        ShowAll,                    // With "Show all keys"
        ShowAllCharted,             // With "Show all keys", when the row's part has notes
        ShowAllFrets,               // With "Show all keys", when a part other than drums has notes
        ShowAllLyrics,              // With "Show all keys", when the chart has lyrics
        ShowAllLowerDifficulties,   // With "Show all keys", when a part has notes below Expert
        WhenSet,                    // Only when the file sets it
    }

    public enum Recommended
    {
        No,
        Required,
        Always,
        Charted,    // When the row's part has notes
        Band,       // When more than one part has notes
    }

    public enum DrumType
    {
        FourLane,
        Pro,
        FiveLane,
    }

    public static readonly string[] DrumTypeNames = { "4-lane drums", "Pro drums", "5-lane drums" };

    public class Row
    {
        public string key;
        public string label;
        public Input input;
        public string hintType;     // The type the hint names, which for the year isn't its box's
        public string range;
        public Recommended recommended = Recommended.No;
        public Shown shown = Shown.ShowAll;
        public Song.Instrument part = Song.Instrument.Unrecognised;
        public string[] optionValues;
        public string[] optionLabels;
        public bool playbackPositionButton;
        public int lines = 1;       // The text box's height in lines, for values that are often longer than one line
        public string subheading;   // A row that's only a subheading over the rows after it
    }

    public class Group
    {
        public string title;
        public bool otherKeys;      // The group that also lists the keys no row has
        public Row[] rows;
    }

    static Row MakeRow(string key, string label, Input input, string hintType, Shown shown, Recommended recommended, string range)
    {
        return new Row() { key = key, label = label, input = input, hintType = hintType, shown = shown, recommended = recommended, range = range };
    }

    static Row Text(string key, string label, Shown shown = Shown.ShowAll, Recommended recommended = Recommended.No, string range = null)
    {
        return MakeRow(key, label, Input.Text, "Text", shown, recommended, range);
    }

    static Row Integer(string key, string label, Shown shown = Shown.ShowAll, Recommended recommended = Recommended.No, string range = null)
    {
        return MakeRow(key, label, Input.Integer, "Integer", shown, recommended, range);
    }

    static Row Seconds(string key, string label, Shown shown = Shown.ShowAll, Recommended recommended = Recommended.No, bool playbackPositionButton = false)
    {
        Row row = MakeRow(key, label, Input.Seconds, "Seconds", shown, recommended, null);
        row.playbackPositionButton = playbackPositionButton;
        return row;
    }

    static Row Bool(string key, string label, Shown shown = Shown.ShowAll)
    {
        return MakeRow(key, label, Input.Bool, null, shown, Recommended.No, null);
    }

    static Row Select(string key, string label, string[] values, string[] labels)
    {
        Row row = MakeRow(key, label, Input.Select, null, Shown.ShowAll, Recommended.No, null);
        row.optionValues = values;
        row.optionLabels = labels;
        return row;
    }

    static Row Difficulty(string key, string label, Song.Instrument part)
    {
        Row row = Integer(key, label, Shown.Charted, Recommended.Charted, "Number 1 - 6");
        row.part = part;
        return row;
    }

    static Row PartRow(Row row, Song.Instrument part)
    {
        row.part = part;
        return row;
    }

    static Row MultiLine(Row row, int lines)
    {
        row.lines = lines;
        return row;
    }

    static Row Subheading(string title)
    {
        return new Row() { subheading = title };
    }

    const string DifficultyRange = "Number 1 - 6";

    public static readonly Group[] Groups =
    {
        new Group() { title = "Song", rows = new Row[] {
            Text("name", "Name", Shown.Always, Recommended.Required),
            Text("artist", "Artist", Shown.Always, Recommended.Always),
            Text("album", "Album", Shown.Always, Recommended.Always),
            Text("genre", "Genre", Shown.Always, Recommended.Always),
            MakeRow("year", "Year", Input.Text, "Integer", Shown.Always, Recommended.Always, "4 digits"),
            Text("charter", "Charter", Shown.Always, Recommended.Always),
            MakeRow("song_length", "Song length", Input.Measured, null, Shown.WhenSet, Recommended.No, null),
        }},
        new Group() { title = "Difficulties", rows = new Row[] {
            Integer("diff_band", "Whole band", Shown.Band, Recommended.Band, DifficultyRange),
            Difficulty("diff_guitar", "Guitar", Song.Instrument.Guitar),
            Difficulty("diff_guitar_coop", "Guitar co-op", Song.Instrument.GuitarCoop),
            Difficulty("diff_rhythm", "Rhythm", Song.Instrument.Rhythm),
            Difficulty("diff_bass", "Bass", Song.Instrument.Bass),
            Difficulty("diff_drums", "Drums", Song.Instrument.Drums),
            PartRow(Integer("diff_drums_real", "Pro drums", Shown.ShowAllCharted, Recommended.No, DifficultyRange), Song.Instrument.Drums),
            Difficulty("diff_keys", "Keys", Song.Instrument.Keys),
            Difficulty("diff_keys_ghl", "GHL keys", Song.Instrument.GHLiveKeys),
            Difficulty("diff_guitarghl", "GHL guitar", Song.Instrument.GHLiveGuitar),
            Difficulty("diff_guitar_coop_ghl", "GHL co-op", Song.Instrument.GHLiveCoop),
            Difficulty("diff_rhythm_ghl", "GHL rhythm", Song.Instrument.GHLiveRhythm),
            Difficulty("diff_bassghl", "GHL bass", Song.Instrument.GHLiveBass),
            Integer("diff_vocals", "Vocals", Shown.WhenSet, Recommended.No, DifficultyRange),
        }},
        new Group() { title = "Song list", rows = new Row[] {
            Seconds("preview_start_time", "Preview start", Shown.Always, Recommended.Always, true),
            Seconds("preview_end_time", "Preview end", Shown.Always, Recommended.No, true),
            Text("icon", "Icon", Shown.Always),
            MultiLine(Text("loading_phrase", "Loading phrase", Shown.Always, Recommended.Always), 4),
            Integer("album_track", "Album track", Shown.Always, Recommended.Always, "from 1"),
            Integer("playlist_track", "Playlist track", Shown.ShowAll, Recommended.No, "from 1"),
            MultiLine(Text("playlist", "Playlist"), 2),
            Text("sub_playlist", "Sub-playlist"),
            Bool("modchart", "Modchart"),
            Bool("explicit_lyrics", "Explicit lyrics", Shown.Always),
        }},
        new Group() { title = "Read by YARG only", rows = new Row[] {
            Subheading("Preview and video"),
            Text("preview", "Preview (start end)", Shown.WhenSet, Recommended.No, "milliseconds"),
            Seconds("video_end_time", "Video end"),
            Bool("video_loop", "Loop video"),
            Subheading("Files"),
            Text("background", "Background image", Shown.ShowAll, Recommended.No, "file name"),
            Text("video", "Video", Shown.ShowAll, Recommended.No, "file name"),
            Text("cover", "Album art", Shown.ShowAll, Recommended.No, "file name"),
            Subheading("Rating and tags"),
            Select("rating", "Rating", new string[] { "1", "2", "3", "4", "5" },
                new string[] { "1 Family Friendly", "2 Supervision Recommended", "3 Mature", "4 No Rating", "5 Sensitive Content" }),
            Text("tags", "Tags"),
            Subheading("Difficulties"),
            Integer("diff_vocals_harm", "Harmonies", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_guitar_real", "Pro guitar (17-fret)", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_guitar_real_22", "Pro guitar (22-fret)", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_bass_real", "Pro bass (17-fret)", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_bass_real_22", "Pro bass (22-fret)", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_keys_real", "Pro keys", Shown.WhenSet, Recommended.No, DifficultyRange),
            Integer("diff_elite_drums", "Elite drums", Shown.WhenSet, Recommended.No, DifficultyRange),
            Subheading("About the song"),
            Text("sub_genre", "Sub-genre"),
            Text("covered_by", "Covered by"),
            Text("date_released", "Date released", Shown.ShowAll, Recommended.No, "yyyy-MM-dd or yyyy-MM"),
            Text("location", "Location"),
            Subheading("Credits"),
            Text("credit_album_art_designed_by", "Album art"),
            Text("credit_arranged_by", "Arranged by"),
            Text("credit_background", "Background"),
            Text("credit_composed_by", "Composed by"),
            Text("credit_courtesy_of", "Courtesy of"),
            Text("credit_engineered_by", "Engineered by"),
            Text("credit_license", "License"),
            Text("credit_mastered_by", "Mastered by"),
            Text("credit_mixed_by", "Mixed by"),
            Text("credit_other", "Other"),
            Text("credit_performed_by", "Performed by"),
            Text("credit_produced_by", "Produced by"),
            Text("credit_published_by", "Published by"),
            Text("credit_written_by", "Written by"),
            Subheading("Charters of each part"),
            PartRow(Text("charter_guitar", "Guitar", Shown.ShowAllCharted), Song.Instrument.Guitar),
            PartRow(Text("charter_rhythm", "Rhythm", Shown.ShowAllCharted), Song.Instrument.Rhythm),
            PartRow(Text("charter_bass", "Bass", Shown.ShowAllCharted), Song.Instrument.Bass),
            PartRow(Text("charter_drums", "Drums", Shown.ShowAllCharted), Song.Instrument.Drums),
            Text("charter_elite_drums", "Elite drums", Shown.WhenSet),
            PartRow(Text("charter_keys", "Keys", Shown.ShowAllCharted), Song.Instrument.Keys),
            Text("charter_pro_guitar", "Pro guitar", Shown.WhenSet),
            Text("charter_pro_bass", "Pro bass", Shown.WhenSet),
            Text("charter_pro_keys", "Pro keys", Shown.WhenSet),
            PartRow(Text("charter_guitar_6f", "GHL guitar", Shown.ShowAllCharted), Song.Instrument.GHLiveGuitar),
            PartRow(Text("charter_rhythm_6f", "GHL rhythm", Shown.ShowAllCharted), Song.Instrument.GHLiveRhythm),
            PartRow(Text("charter_bass_6f", "GHL bass", Shown.ShowAllCharted), Song.Instrument.GHLiveBass),
            Text("charter_vocals", "Vocals", Shown.ShowAllLyrics),
            Text("charter_lower_diff", "Lower difficulties", Shown.ShowAllLowerDifficulties),
            Text("charter_venue", "Venue", Shown.WhenSet),
            Text("charter_audio", "Audio"),
            Subheading("Links"),
            Text("link_bandcamp", "Bandcamp"),
            Text("link_bluesky", "Bluesky"),
            Text("link_facebook", "Facebook"),
            Text("link_instagram", "Instagram"),
            Text("link_spotify", "Spotify"),
            Text("link_twitter", "Twitter"),
            Text("link_other", "Other"),
            Text("link_youtube", "YouTube"),
            Subheading("Vocals and venue"),
            Select("vocal_gender", "Vocal gender", new string[] { "female", "male", "nonbinary", "other" }, null),
            Integer("vocal_scroll_speed", "Vocal scroll speed", Shown.WhenSet, Recommended.No, "percent"),
            Bool("clean_vocals", "Clean vocals"),
            Text("venue_hint", "Venue hint"),
            Text("vocal_character_hint", "Vocal character hint"),
            Integer("tuning_offset_cents", "Tuning offset", Shown.WhenSet, Recommended.No, "cents"),
        }},
        new Group() { title = "Deprecated", rows = new Row[] {
            Seconds("delay", "Offset (delay)"),
            Integer("hopo_frequency", "HOPO frequency", Shown.ShowAllFrets, Recommended.No, "ticks"),
            Bool("eighthnote_hopo", "Eighth-note HOPOs", Shown.ShowAllFrets),
            Integer("multiplier_note", "Star power note", Shown.WhenSet, Recommended.No, "103 or 116"),
        }},
        new Group() { title = "Settings", rows = new Row[] {
            Integer("sustain_cutoff_threshold", "Sustain cutoff", Shown.ShowAll, Recommended.No, "ticks"),
            Integer("chord_snap_threshold", "Chord snap", Shown.ShowAll, Recommended.No, "ticks"),
            Seconds("video_start_time", "Video start"),
            PartRow(MakeRow(DrumTypeKeys, "Drum type", Input.DrumType, null, Shown.ShowAllCharted, Recommended.No, null), Song.Instrument.Drums),
        }},
        new Group() { title = "Other keys", otherKeys = true, rows = new Row[] {
            Text("ignored_issues", "Ignored issues", Shown.ShowAll, Recommended.No, "issue codes, separated by commas"),
        }},
    };

    // The drum type row's key, which stands for both of its keys
    public const string DrumTypeKeys = "five_lane_drums, pro_drums";

    /// <summary>
    /// The song.ini keys the rows edit, in their order.
    /// </summary>
    public static List<string> RowKeys()
    {
        var keys = new List<string>();
        foreach (Group group in Groups)
        {
            foreach (Row row in group.rows)
            {
                if (row.subheading != null)
                    continue;

                if (row.input == Input.DrumType)
                {
                    keys.Add("five_lane_drums");
                    keys.Add("pro_drums");
                }
                else
                    keys.Add(row.key);
            }
        }

        return keys;
    }

    /// <summary>
    /// What the rows shown for a chart depend on.
    /// </summary>
    public class ChartFacts
    {
        public HashSet<Song.Instrument> chartedParts = new HashSet<Song.Instrument>();
        public bool hasLyrics;
        public bool hasLowerDifficulties;
        public DrumType notesDrumType = DrumType.FourLane;

        public static ChartFacts Of(Song song)
        {
            var facts = new ChartFacts();

            foreach (Song.Instrument instrument in EnumX<Song.Instrument>.Values)
            {
                if (instrument == Song.Instrument.Unrecognised)
                    continue;

                foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
                {
                    if (song.GetChart(instrument, difficulty).notes.Count == 0)
                        continue;

                    facts.chartedParts.Add(instrument);
                    if (difficulty != Song.Difficulty.Expert)
                        facts.hasLowerDifficulties = true;
                }
            }

            foreach (MoonscraperChartEditor.Song.Event songEvent in song.events)
            {
                if (songEvent.title.StartsWith(LyricHelper.LYRIC_EVENT_PREFIX))
                {
                    facts.hasLyrics = true;
                    break;
                }
            }

            facts.notesDrumType = NotesDrumType(song);
            return facts;
        }

        public bool IsCharted(Song.Instrument part)
        {
            return chartedParts.Contains(part);
        }

        // Whether a part other than drums has notes
        public bool HasFretsPart()
        {
            foreach (Song.Instrument part in chartedParts)
            {
                if (part != Song.Instrument.Drums)
                    return true;
            }

            return false;
        }
    }

    // As scan-chart reads a chart's drum type without song.ini: a cymbal marker makes it pro, else a fifth-lane note makes it 5-lane
    static DrumType NotesDrumType(Song song)
    {
        bool fiveLane = false;
        foreach (Song.Difficulty difficulty in EnumX<Song.Difficulty>.Values)
        {
            foreach (Note note in song.GetChart(Song.Instrument.Drums, difficulty).notes)
            {
                if (note.flags.HasFlag(Note.Flags.ProDrums_Cymbal) && !note.IsOpenNote())
                    return DrumType.Pro;

                if (note.drumPad == Note.DrumPad.Green)
                    fiveLane = true;
            }
        }

        return fiveLane ? DrumType.FiveLane : DrumType.FourLane;
    }

    /// <summary>
    /// The drum type song.ini sets, or null if it leaves the type to the notes.
    /// </summary>
    public static DrumType? IniDrumType(SongIni ini)
    {
        if (ini.Get("five_lane_drums") == "True")
            return DrumType.FiveLane;
        if (ini.Get("pro_drums") == "True")
            return DrumType.Pro;
        return null;
    }

    public static bool IsVisible(Row row, bool hasValue, bool showAll, ChartFacts facts)
    {
        if (row.subheading != null || row.input == Input.Measured)
            return false;

        if (hasValue)
            return true;

        switch (row.shown)
        {
            case Shown.Always: return true;
            case Shown.Charted: return facts.IsCharted(row.part);
            case Shown.Band: return facts.chartedParts.Count > 1 || showAll;
            case Shown.ShowAll: return showAll;
            case Shown.ShowAllCharted: return showAll && facts.IsCharted(row.part);
            case Shown.ShowAllFrets: return showAll && facts.HasFretsPart();
            case Shown.ShowAllLyrics: return showAll && facts.hasLyrics;
            case Shown.ShowAllLowerDifficulties: return showAll && facts.hasLowerDifficulties;
            default: return false;
        }
    }

    public static bool IsRecommended(Row row, ChartFacts facts)
    {
        switch (row.recommended)
        {
            case Recommended.Required:
            case Recommended.Always:
                return true;
            case Recommended.Charted: return facts.IsCharted(row.part);
            case Recommended.Band: return facts.chartedParts.Count > 1;
            default: return false;
        }
    }

    /// <summary>
    /// The hint under a row's label, such as "Integer (Number 1 - 6, Recommended)", or empty for a checkbox or a dropdown.
    /// </summary>
    public static string HintText(Row row, bool recommended)
    {
        if (row.hintType == null)
            return string.Empty;

        var extras = new List<string>();
        if (row.range != null)
            extras.Add(row.range);
        if (recommended)
            extras.Add(row.recommended == Recommended.Required ? "Required" : "Recommended");

        return extras.Count > 0 ? row.hintType + " (" + string.Join(", ", extras.ToArray()) + ")" : row.hintType;
    }
}
