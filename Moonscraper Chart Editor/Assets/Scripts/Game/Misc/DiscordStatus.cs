// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using UnityEngine;
using MoonscraperChartEditor.Song;

/// <summary>
/// Shows what's being charted on the user's Discord profile: the song and its artist, the instrument and difficulty,
/// and how long the song has been open. Discord shows it under "Playing" and the name of the Discord application below.
/// </summary>
public class DiscordStatus : MonoBehaviour
{
    // The "Moonscraper Chart Editor" application on Discord's Developer Portal
    const string ApplicationId = "1573347764937555988";
    // The application's Rich Presence art asset, shown beside the status
    const string ImageKey = "moonscraper";
    const int MaxTextLength = 128;      // Discord refuses an activity with longer text
    const float CheckIntervalSeconds = 1;

#if UNITY_STANDALONE_WIN
    DiscordIpc_Windows discord;
    Song timedSong;
    long songOpenedAt;
    float nextCheckTime;

    void Update()
    {
        if (Time.unscaledTime < nextCheckTime)
            return;

        nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;

        if (!Globals.gameSettings.discordStatus)
        {
            Stop();
            return;
        }

        ChartEditor editor = ChartEditor.Instance;
        Song song = editor.currentSong;
        if (song != timedSong)
        {
            timedSong = song;
            songOpenedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        if (discord == null)
            discord = new DiscordIpc_Windows(ApplicationId);

        discord.SetActivity(ActivityJson(song.name, song.iniProperties.Get("artist"), editor.currentInstrument, editor.currentDifficulty, songOpenedAt));
    }

    void OnDestroy()
    {
        Stop();
    }

    void Stop()
    {
        if (discord != null)
        {
            discord.Dispose();
            discord = null;
        }
    }

    /// <summary>
    /// The activity Discord shows, as JSON: "Charting {song} by {artist}" and "{instrument} ({difficulty})", timed from when the song was opened.
    /// </summary>
    public static string ActivityJson(string songName, string artist, Song.Instrument instrument, Song.Difficulty difficulty, long openedAtUnixSeconds)
    {
        songName = songName == null ? string.Empty : songName.Trim();
        artist = artist == null ? string.Empty : artist.Trim();

        string details;
        if (songName.Length == 0)
            details = "Charting a new song";
        else if (artist.Length == 0)
            details = "Charting " + songName;
        else
            details = "Charting " + songName + " by " + artist;

        System.Text.StringBuilder json = new System.Text.StringBuilder();
        json.Append("{\"details\":").Append(DiscordIpc_Windows.Quote(Shorten(details)));

        string instrumentName = InstrumentName(instrument);
        if (instrumentName != null)
            json.Append(",\"state\":").Append(DiscordIpc_Windows.Quote(Shorten(instrumentName + " (" + difficulty + ")")));

        json.Append(",\"timestamps\":{\"start\":").Append(openedAtUnixSeconds).Append('}');
        json.Append(",\"assets\":{\"large_image\":").Append(DiscordIpc_Windows.Quote(ImageKey)).Append('}');
        json.Append(",\"instance\":false}");
        return json.ToString();
    }

    static string InstrumentName(Song.Instrument instrument)
    {
        switch (instrument)
        {
            case Song.Instrument.Guitar: return "Guitar";
            case Song.Instrument.GuitarCoop: return "Guitar Co-op";
            case Song.Instrument.Bass: return "Bass";
            case Song.Instrument.Rhythm: return "Rhythm";
            case Song.Instrument.Keys: return "Keys";
            case Song.Instrument.Drums: return "Drums";
            case Song.Instrument.GHLiveGuitar: return "GHL Guitar";
            case Song.Instrument.GHLiveBass: return "GHL Bass";
            case Song.Instrument.GHLiveRhythm: return "GHL Rhythm";
            case Song.Instrument.GHLiveCoop: return "GHL Co-op";
            case Song.Instrument.GHLiveKeys: return "GHL Keys";
            default: return null;
        }
    }

    // Ends text that's too long for Discord with an ellipsis, without splitting a character made of two UTF-16 units
    static string Shorten(string text)
    {
        if (text.Length <= MaxTextLength)
            return text;

        int length = MaxTextLength - 1;
        if (char.IsHighSurrogate(text[length - 1]))
            --length;

        return text.Substring(0, length) + "…";
    }
#endif
}
