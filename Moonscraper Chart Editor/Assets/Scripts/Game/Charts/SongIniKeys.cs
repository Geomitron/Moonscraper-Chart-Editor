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

    /// <summary>
    /// The keys to write: each key that has a value once, named in lowercase, with its first value as games read it; the keys of
    /// <see cref="RecommendedOrder"/> first, in that order, then the other keys in the order given.
    /// </summary>
    public static List<KeyValuePair<string, string>> Normalize(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var known = new List<KeyValuePair<string, string>>();
        var other = new List<KeyValuePair<string, string>>();
        var written = new HashSet<string>();

        foreach (var pair in pairs)
        {
            string key = pair.Key == null ? string.Empty : pair.Key.Trim().ToLowerInvariant();
            if (key == string.Empty || IsUnset(key, pair.Value) || !written.Add(key))
                continue;

            var normalized = new KeyValuePair<string, string>(key, pair.Value.Trim());
            if (s_rank.ContainsKey(key))
                known.Add(normalized);
            else
                other.Add(normalized);
        }

        // List.Sort isn't stable, but each known key is in the list once
        known.Sort((a, b) => s_rank[a.Key].CompareTo(s_rank[b.Key]));
        known.AddRange(other);
        return known;
    }

    /// <summary>
    /// The song.ini file for these keys, as <see cref="Normalize"/> orders them, with " = " between each key and its value.
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
