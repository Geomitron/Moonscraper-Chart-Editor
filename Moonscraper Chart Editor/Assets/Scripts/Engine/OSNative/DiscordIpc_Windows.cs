// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

/// <summary>
/// Shows an activity (Rich Presence) on the user's Discord profile through the Discord app on this computer, which listens on a local pipe.
/// A background thread connects, tries again every few seconds while Discord isn't running, answers Discord's pings,
/// and sends only the latest activity, at most once every few seconds to stay within Discord's rate limit.
/// Discord clears the activity when the pipe closes: on Dispose, or when Moonscraper exits.
/// </summary>
public sealed class DiscordIpc_Windows : IDisposable
{
    const string DiscordPipePrefix = @"\\?\pipe\discord-ipc-";
    const int OpHandshake = 0;
    const int OpFrame = 1;
    const int OpClose = 2;
    const int OpPing = 3;
    const int OpPong = 4;
    const int MaxFrameLength = 64 * 1024;
    const int ReconnectDelayMs = 10000;
    const int ReadyTimeoutMs = 10000;
    const int MinSendIntervalMs = 4000;     // Discord accepts 5 activity updates every 20 seconds
    const int PollIntervalMs = 250;

    static readonly Regex c_readyEvent = new Regex("\"evt\"\\s*:\\s*\"READY\"");
    static readonly Regex c_errorEvent = new Regex("\"evt\"\\s*:\\s*\"ERROR\"");

    readonly string applicationId;
    readonly string pipePrefix;
    readonly int processId;
    readonly Thread thread;
    readonly AutoResetEvent wake = new AutoResetEvent(false);
    readonly object activityLock = new object();

    volatile bool disposed;
    string activityJson;         // null shows no activity
    int activityVersion;

    // Only the background thread uses these
    IntPtr pipe = INVALID_HANDLE_VALUE;
    bool ready;
    DateTime connectedAt;
    DateTime lastSentAt = DateTime.MinValue;
    int sentVersion;
    int nonce;
    string lastError;

    public DiscordIpc_Windows(string applicationId) : this(applicationId, DiscordPipePrefix)
    {
    }

    /// <param name="pipePrefix">The pipe's name without its number, which Discord picks from 0 to 9</param>
    public DiscordIpc_Windows(string applicationId, string pipePrefix)
    {
        this.applicationId = applicationId;
        this.pipePrefix = pipePrefix;
        processId = System.Diagnostics.Process.GetCurrentProcess().Id;

        thread = new Thread(Run);
        thread.IsBackground = true;
        thread.Name = "Discord status";
        thread.Start();
    }

    /// <summary>
    /// Sets the activity to show, as a JSON object of Discord's activity fields, or null to show none.
    /// </summary>
    public void SetActivity(string activityJson)
    {
        lock (activityLock)
        {
            if (this.activityJson == activityJson)
                return;

            this.activityJson = activityJson;
            ++activityVersion;
        }

        wake.Set();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        wake.Set();
        thread.Join(1000);
    }

    void Run()
    {
        try
        {
            while (!disposed)
            {
                if (pipe == INVALID_HANDLE_VALUE && !Connect())
                {
                    wake.WaitOne(ReconnectDelayMs);
                    continue;
                }

                if (!ReadFrames() || (!ready && (DateTime.UtcNow - connectedAt).TotalMilliseconds > ReadyTimeoutMs))
                {
                    Disconnect();
                    wake.WaitOne(ReconnectDelayMs);
                    continue;
                }

                if (ready && !SendActivityIfChanged())
                {
                    Disconnect();
                    continue;
                }

                wake.WaitOne(PollIntervalMs);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Discord status stopped: " + e);
        }
        finally
        {
            Disconnect();
        }
    }

    bool Connect()
    {
        for (int i = 0; i < 10; ++i)
        {
            IntPtr handle = CreateFileW(pipePrefix + i, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == INVALID_HANDLE_VALUE)
                continue;

            pipe = handle;
            ready = false;
            connectedAt = DateTime.UtcNow;
            sentVersion = -1;

            if (WriteFrame(OpHandshake, "{\"v\":1,\"client_id\":" + Quote(applicationId) + "}"))
                return true;

            Disconnect();
        }

        return false;
    }

    void Disconnect()
    {
        if (pipe == INVALID_HANDLE_VALUE)
            return;

        CloseHandle(pipe);
        pipe = INVALID_HANDLE_VALUE;

        if (ready)
            Debug.Log("Discord status: disconnected from Discord");

        ready = false;
    }

    bool SendActivityIfChanged()
    {
        string activity;
        int version;
        lock (activityLock)
        {
            activity = activityJson;
            version = activityVersion;
        }

        if (version == sentVersion || (DateTime.UtcNow - lastSentAt).TotalMilliseconds < MinSendIntervalMs)
            return true;

        string args = "{\"pid\":" + processId + (activity != null ? ",\"activity\":" + activity : "") + "}";
        string command = "{\"cmd\":\"SET_ACTIVITY\",\"args\":" + args + ",\"nonce\":\"" + (++nonce) + "\"}";

        lastSentAt = DateTime.UtcNow;
        sentVersion = version;
        return WriteFrame(OpFrame, command);
    }

    /// <summary>
    /// Reads every whole frame waiting in the pipe, without blocking. False when the connection has closed.
    /// </summary>
    bool ReadFrames()
    {
        byte[] header = new byte[8];
        while (true)
        {
            uint available;
            if (!PeekNamedPipe(pipe, null, 0, IntPtr.Zero, out available, IntPtr.Zero))
                return false;

            if (available < header.Length)
                return true;

            if (!ReadExactly(header))
                return false;

            int opcode = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > MaxFrameLength)
                return false;

            byte[] payload = new byte[length];
            if (!ReadExactly(payload))
                return false;

            string message = Encoding.UTF8.GetString(payload);
            switch (opcode)
            {
                case OpFrame:
                    if (!ready && c_readyEvent.IsMatch(message))
                    {
                        ready = true;
                        lastError = null;
                        Debug.Log("Discord status: connected to Discord");
                    }
                    else if (c_errorEvent.IsMatch(message))
                    {
                        WarnOnce("Discord status: Discord refused the status: " + message);
                    }
                    break;

                case OpPing:
                    if (!WriteFrame(OpPong, message))
                        return false;
                    break;

                case OpClose:
                    WarnOnce("Discord status: Discord closed the connection: " + message);
                    return false;
            }
        }
    }

    // Logs a warning unless it repeats the last one, since a refused application ID is refused again at every reconnection
    void WarnOnce(string warning)
    {
        if (warning != lastError)
            Debug.LogWarning(warning);

        lastError = warning;
    }

    bool ReadExactly(byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            uint read;
            if (!ReadFile(pipe, buffer, offset, (uint)(buffer.Length - offset), out read) || read == 0)
                return false;

            offset += (int)read;
        }

        return true;
    }

    bool WriteFrame(int opcode, string message)
    {
        // Discord needs each frame in a single write
        byte[] payload = Encoding.UTF8.GetBytes(message);
        byte[] frame = new byte[8 + payload.Length];
        BitConverter.GetBytes(opcode).CopyTo(frame, 0);
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 4);
        payload.CopyTo(frame, 8);

        uint written;
        return WriteFile(pipe, frame, (uint)frame.Length, out written, IntPtr.Zero) && written == frame.Length;
    }

    /// <summary>
    /// The text as a JSON string, with its quotes.
    /// </summary>
    public static string Quote(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length + 2);
        sb.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool PeekNamedPipe(IntPtr pipe, byte[] buffer, uint bufferSize, IntPtr bytesRead, out uint totalBytesAvailable, IntPtr bytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr file, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr file, IntPtr buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

    static bool ReadFile(IntPtr file, byte[] buffer, int offset, uint count, out uint read)
    {
        GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return ReadFile(file, pinned.AddrOfPinnedObject() + offset, count, out read, IntPtr.Zero);
        }
        finally
        {
            pinned.Free();
        }
    }
}

#endif
