// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using UnityEngine;

/// <summary>
/// Lists this copy of Moonscraper in Windows' "Open with" for the files it opens, under a name of its own, for the current user.
/// Every Moonscraper is named "Moonscraper Chart Editor.exe", and Windows keeps one "Open with" entry per program file name,
/// so picking this copy by its file name can open another one, such as an official install. This entry points at this copy's full path.
/// Only the user can make it the default.
/// </summary>
public static class FileTypeRegistration_Windows
{
    const string ApplicationName = "Moonscraper Chart Editor (Geomitron)";
    const string ProgIdPrefix = "MoonscraperChartEditorGeomitron";

    // Each file type, and the name Explorer shows for it when this copy opens it
    static readonly string[,] c_fileTypes =
    {
        { ".chart", "Chart File" },
        { ".mid", "MIDI Sequence" },
        { ".msce", "Moonscraper Chart File" },
        { ".sng", "Song Package" },
    };

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetModuleFileName(IntPtr module, StringBuilder filename, int size);
    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    const int SHCNE_ASSOCCHANGED = 0x08000000;
    const uint SHCNF_IDLIST = 0;

    // Registered on every start, so the entry follows this copy if it moves, and points at the copy that ran last.
    // Unity finds this method in the editor's build of the scripts, so the editor compiles it too.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        if (Application.isEditor)
            return;

        StringBuilder exe = new StringBuilder(1024);
        if (GetModuleFileName(IntPtr.Zero, exe, exe.Capacity) == 0)
            return;

        string exePath = exe.ToString();
        ThreadPool.QueueUserWorkItem(state => RegisterFileTypes(exePath));
    }

    static void RegisterFileTypes(string exePath)
    {
        try
        {
            bool changed = false;
            string command = string.Format("\"{0}\" \"%1\"", exePath);
            string icon = exePath + ",0";

            using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
            {
                for (int i = 0; i < c_fileTypes.GetLength(0); ++i)
                {
                    string extension = c_fileTypes[i, 0];
                    string progId = ProgIdPrefix + extension;

                    changed |= SetValue(classes, progId, string.Empty, c_fileTypes[i, 1]);
                    changed |= SetValue(classes, progId + @"\DefaultIcon", string.Empty, icon);
                    changed |= SetValue(classes, progId + @"\shell\open\command", string.Empty, command);
                    changed |= SetValue(classes, progId + @"\Application", "ApplicationName", ApplicationName);
                    changed |= SetValue(classes, extension + @"\OpenWithProgids", progId, string.Empty);
                }
            }

            // Explorer rereads file types when told they changed
            if (changed)
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception e)
        {
            Debug.LogError("Unable to list Moonscraper in Open with: " + e.Message);
        }
    }

    // Writes a string value only if it differs, and returns whether it did
    static bool SetValue(RegistryKey root, string subKey, string name, string value)
    {
        using (RegistryKey key = root.CreateSubKey(subKey))
        {
            if (value.Equals(key.GetValue(name) as string))
                return false;

            key.SetValue(name, value, RegistryValueKind.String);
            return true;
        }
    }
}

#endif
