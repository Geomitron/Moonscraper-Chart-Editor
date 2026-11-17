using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The song.ini keys games read, and how a song.ini is written: each key that has a value once, in the order the chart spec recommends
/// (https://chart-spec.enchor.us/ini/reading-rules#key-order), then the other keys in their own order.
/// </summary>
public static class SongIniKeys
{
    public static readonly string[] RecommendedOrder =
    {
        "name", "artist", "album", "genre", "year", "charter", "song_length",
        "diff_band", "diff_guitar", "diff_guitar_coop", "diff_rhythm", "diff_bass", "diff_drums", "diff_drums_real", "diff_keys",
        "diff_keys_ghl", "diff_guitarghl", "diff_guitar_coop_ghl", "diff_rhythm_ghl", "diff_bassghl", "diff_vocals",
        "preview_start_time", "preview_end_time", "icon", "loading_phrase", "album_track", "playlist_track", "playlist", "sub_playlist",
        "modchart", "explicit_lyrics",

        // Read by YARG only
        "preview", "video_end_time", "video_loop", "background", "video", "cover", "rating", "tags",
        "diff_vocals_harm", "diff_guitar_real", "diff_guitar_real_22", "diff_bass_real", "diff_bass_real_22", "diff_keys_real", "diff_elite_drums",
        "sub_genre", "covered_by", "date_released", "location",
        "credit_album_art_designed_by", "credit_arranged_by", "credit_background", "credit_composed_by", "credit_courtesy_of",
        "credit_engineered_by", "credit_license", "credit_mastered_by", "credit_mixed_by", "credit_other", "credit_performed_by",
        "credit_produced_by", "credit_published_by", "credit_written_by",
        "charter_guitar", "charter_rhythm", "charter_bass", "charter_drums", "charter_elite_drums", "charter_keys", "charter_pro_guitar",
        "charter_pro_bass", "charter_pro_keys", "charter_guitar_6f", "charter_rhythm_6f", "charter_bass_6f", "charter_vocals",
        "charter_lower_diff", "charter_venue", "charter_audio",
        "link_bandcamp", "link_bluesky", "link_facebook", "link_instagram", "link_spotify", "link_twitter", "link_other", "link_youtube",
        "vocal_gender", "vocal_scroll_speed", "clean_vocals", "venue_hint", "vocal_character_hint", "tuning_offset_cents",

        // Deprecated
        "delay", "hopo_frequency", "eighthnote_hopo", "multiplier_note",

        // Settings
        "sustain_cutoff_threshold", "chord_snap_threshold", "video_start_time", "five_lane_drums", "pro_drums",
    };

    // Keys whose value is a number, where -1 means not set. Every diff_ key is one too.
    static readonly HashSet<string> s_numberKeys = new HashSet<string>
    {
        "song_length", "preview_start_time", "preview_end_time", "album_track", "playlist_track", "video_end_time", "rating",
        "vocal_scroll_speed", "tuning_offset_cents", "delay", "hopo_frequency", "multiplier_note", "sustain_cutoff_threshold",
        "chord_snap_threshold", "video_start_time",
    };

    static readonly Dictionary<string, int> s_rank = CreateRanks();

    static Dictionary<string, int> CreateRanks()
    {
        var ranks = new Dictionary<string, int>();
        for (int i = 0; i < RecommendedOrder.Length; ++i)
        {
            ranks[RecommendedOrder[i]] = i;
        }

        return ranks;
    }

    public static bool IsNumberKey(string key)
    {
        key = key.Trim().ToLowerInvariant();
        return key.StartsWith("diff_") || s_numberKeys.Contains(key);
    }

    /// <summary>
    /// A blank value, or -1 in a number key, means the key isn't set, so it isn't written.
    /// </summary>
    public static bool IsUnset(string key, string value)
    {
        value = value == null ? string.Empty : value.Trim();
        return value == string.Empty || (value == "-1" && IsNumberKey(key));
    }

    // Keys whose value is True or False
    static readonly HashSet<string> s_boolKeys = new HashSet<string>
    {
        "modchart", "explicit_lyrics", "video_loop", "clean_vocals", "eighthnote_hopo", "five_lane_drums", "pro_drums",
    };

    // The older name of each key that has one, which games read only when the file doesn't have the key, whatever its value
    static readonly KeyValuePair<string, string>[] s_olderNames =
    {
        new KeyValuePair<string, string>("charter", "frets"),
        new KeyValuePair<string, string>("album_track", "track"),
        new KeyValuePair<string, string>("pro_drums", "pro_drum"),
        new KeyValuePair<string, string>("multiplier_note", "star_power_note"),
    };

    public static bool IsBoolKey(string key)
    {
        return s_boolKeys.Contains(key.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// The keys to write: each key that has a value once, named in lowercase, with the value of its last line, which games read; the
    /// keys of <see cref="RecommendedOrder"/> first, in that order, then the other keys in the order of their last lines. A True or
    /// False value is written as True or False.
    /// </summary>
    public static List<KeyValuePair<string, string>> Normalize(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var lastValues = new List<KeyValuePair<string, string>>();
        foreach (var pair in pairs)
        {
            string key = pair.Key == null ? string.Empty : pair.Key.Trim().ToLowerInvariant();
            if (key == string.Empty)
                continue;

            lastValues.RemoveAll(p => p.Key == key);
            lastValues.Add(new KeyValuePair<string, string>(key, pair.Value == null ? string.Empty : pair.Value.Trim()));
        }

        var known = new List<KeyValuePair<string, string>>();
        var other = new List<KeyValuePair<string, string>>();
        foreach (var pair in lastValues)
        {
            if (IsUnset(pair.Key, pair.Value))
                continue;

            var normalized = new KeyValuePair<string, string>(pair.Key, FormatValue(pair.Key, pair.Value));
            if (s_rank.ContainsKey(pair.Key))
                known.Add(normalized);
            else
                other.Add(normalized);
        }

        // List.Sort isn't stable, but each known key is in the list once
        known.Sort((a, b) => s_rank[a.Key].CompareTo(s_rank[b.Key]));
        known.AddRange(other);
        return known;
    }

    static string FormatValue(string key, string value)
    {
        bool? b = ParseBool(value);
        return s_boolKeys.Contains(key) && b.HasValue ? (b.Value ? "True" : "False") : value;
    }

    static bool? ParseBool(string value)
    {
        value = value.Trim();
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }

    static int? ParseInteger(string value)
    {
        int number;
        return int.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number) ? number : (int?)null;
    }

    static bool IsAtMostZero(string value)
    {
        int? number = ParseInteger(value);
        return number.HasValue && number.Value <= 0;
    }

    static bool IsPositiveInteger(string value)
    {
        int? number = ParseInteger(value);
        return number.HasValue && number.Value > 0;
    }

    static bool IsZero(string value)
    {
        double number;
        return double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number) && number == 0;
    }

    /// <summary>
    /// The [song] section's lines of a song.ini file, in the file's order, read as games read them: section names and keys in any
    /// letter case, and lines starting with ; or # skipped.
    /// </summary>
    public static List<KeyValuePair<string, string>> Parse(string text)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        bool inSong = false;

        foreach (string rawLine in text.TrimStart('﻿').Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inSong = line.Substring(1, line.Length - 2).Trim().Equals("song", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            int equals = line.IndexOf('=');
            if (inSong && equals >= 0)
                pairs.Add(new KeyValuePair<string, string>(line.Substring(0, equals).Trim().ToLowerInvariant(), line.Substring(equals + 1).Trim()));
        }

        return pairs;
    }

    /// <summary>
    /// The keys of a song.ini that's opened, as <see cref="Normalize"/> orders them, without the values that change nothing: an older
    /// key name is read as its current one, and default values that games read the same as no line, and Clone Hero's 16000 track
    /// numbers, are left out. Moonscraper saves a .chart, so the values that change nothing in a .chart are left out too.
    /// </summary>
    public static List<KeyValuePair<string, string>> NormalizeOpened(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var lines = new List<KeyValuePair<string, string>>();
        foreach (var pair in pairs)
            lines.Add(new KeyValuePair<string, string>((pair.Key ?? string.Empty).Trim().ToLowerInvariant(), pair.Value ?? string.Empty));

        foreach (var names in s_olderNames)
        {
            bool hasKey = lines.Exists(p => p.Key == names.Key);
            for (int i = 0; i < lines.Count; ++i)
            {
                if (lines[i].Key == names.Value)
                    lines[i] = new KeyValuePair<string, string>(hasKey ? string.Empty : names.Key, lines[i].Value);
            }
        }

        var normalized = Normalize(lines);
        string hopoFrequency = null;
        foreach (var pair in normalized)
        {
            if (pair.Key == "hopo_frequency")
                hopoFrequency = pair.Value;
        }

        normalized.RemoveAll(pair => ChangesNothing(pair.Key, pair.Value, hopoFrequency));
        return normalized;
    }

    // Whether games read the song the same without this line (scan-chart's defaultIniValue check), in a .chart
    static bool ChangesNothing(string key, string value, string hopoFrequency)
    {
        switch (key)
        {
            case "delay":
            case "video_start_time":
            case "rating":
            case "tuning_offset_cents":
                return IsZero(value);

            case "hopo_frequency":
            case "sustain_cutoff_threshold":
            case "chord_snap_threshold":
                return IsAtMostZero(value);

            case "eighthnote_hopo":
                return ParseBool(value) == false || (hopoFrequency != null && IsPositiveInteger(hopoFrequency));

            case "modchart":
            case "explicit_lyrics":
            case "video_loop":
            case "clean_vocals":
            case "pro_drums":
            case "five_lane_drums":
                return ParseBool(value) == false;

            case "album_track":
            case "playlist_track":
                return value == "16000";

            // A .chart has no star power note number, and no game reads end_events
            case "multiplier_note":
            case "end_events":
                return true;
        }

        return false;
    }

    /// <summary>
    /// The song.ini file for these keys, as <see cref="Normalize"/> writes them, with " = " between each key and its value.
    /// </summary>
    public static string IniText(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var text = new StringBuilder("[song]\r\n");
        foreach (var pair in Normalize(pairs))
        {
            text.Append(pair.Key).Append(" = ").Append(pair.Value).Append("\r\n");
        }

        return text.ToString();
    }
}
