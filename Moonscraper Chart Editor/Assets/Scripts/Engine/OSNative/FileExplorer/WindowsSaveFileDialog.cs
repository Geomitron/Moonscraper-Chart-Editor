// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;

/// <summary>
/// The Windows save dialog. Each file type can suggest its own file name: choosing another type shows that type's name, unless the user typed a name of their own.
/// A suggested name is shortened to fit the folder the dialog is in.
/// </summary>
public static class WindowsSaveFileDialog
{
    const int MaxFilenameLength = 255;      // NTFS
    const int MaxPathLength = 259;          // MAX_PATH, without the terminating null

    public static bool Show(IntPtr owner, string title, ExtensionFilter[] filters, string defaultFileName, out string resultPath)
    {
        resultPath = string.Empty;
        IFileDialog dialog = new NativeFileSaveDialog();

        try
        {
            var fileTypes = new COMDLG_FILTERSPEC[filters.Length];
            for (int i = 0; i < filters.Length; ++i)
            {
                string[] patterns = Array.ConvertAll(filters[i].extensions, extension => "*." + extension);
                fileTypes[i] = new COMDLG_FILTERSPEC() { pszName = string.Format("{0} ({1})", filters[i].name, string.Join(", ", patterns)), pszSpec = string.Join(";", patterns) };
            }

            dialog.SetFileTypes((uint)fileTypes.Length, fileTypes);
            dialog.SetFileTypeIndex(1);

            // With a default extension, the dialog adds the chosen file type's extension to a name typed without one
            dialog.SetDefaultExtension(filters[0].extensions[0]);

            uint options;
            dialog.GetOptions(out options);
            dialog.SetOptions(options | FOS_OVERWRITEPROMPT | FOS_PATHMUSTEXIST | FOS_FORCEFILESYSTEM);
            dialog.SetTitle(title);

            var events = new SuggestedNames(filters, defaultFileName);
            events.Suggest(dialog);

            uint cookie;
            dialog.Advise(events, out cookie);
            int result = dialog.Show(owner);
            dialog.Unadvise(cookie);

            if (result == ERROR_CANCELLED)
                return false;

            Marshal.ThrowExceptionForHR(result);

            IShellItem item;
            dialog.GetResult(out item);
            item.GetDisplayName(SIGDN_FILESYSPATH, out resultPath);

            return !string.IsNullOrEmpty(resultPath);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    /// <summary>
    /// Shows the chosen file type's suggested name whenever the file type or folder changes, while the name is still a suggestion.
    /// </summary>
    class SuggestedNames : IFileDialogEvents
    {
        readonly ExtensionFilter[] m_filters;
        readonly string m_defaultFileName;
        string m_suggestedName = null;

        public SuggestedNames(ExtensionFilter[] filters, string defaultFileName)
        {
            m_filters = filters;
            m_defaultFileName = defaultFileName;
        }

        public void Suggest(IFileDialog dialog)
        {
            try
            {
                string currentName = GetFileName(dialog);
                if (m_suggestedName != null && !IsSuggestedName(currentName))
                    return;

                uint fileTypeIndex;
                dialog.GetFileTypeIndex(out fileTypeIndex);
                ExtensionFilter filter = m_filters[Math.Max(0, Math.Min(m_filters.Length - 1, (int)fileTypeIndex - 1))];

                string name = FitName(filter.defaultFileName ?? m_defaultFileName, "." + filter.extensions[0], GetFolder(dialog));
                if (name != currentName)
                {
                    m_suggestedName = name;
                    dialog.SetFileName(name);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Unable to suggest a file name: " + e.Message);
            }
        }

        // The dialog may show the name with the file type's extension added
        bool IsSuggestedName(string name)
        {
            if (name == m_suggestedName)
                return true;

            foreach (ExtensionFilter filter in m_filters)
            {
                foreach (string extension in filter.extensions)
                {
                    if (name == m_suggestedName + "." + extension)
                        return true;
                }
            }

            return false;
        }

        static string GetFileName(IFileDialog dialog)
        {
            try
            {
                string name;
                dialog.GetFileName(out name);
                return name ?? string.Empty;
            }
            catch (COMException)
            {
                return string.Empty;
            }
        }

        static string GetFolder(IFileDialog dialog)
        {
            try
            {
                IShellItem folder;
                dialog.GetFolder(out folder);
                string path;
                folder.GetDisplayName(SIGDN_FILESYSPATH, out path);
                return path;
            }
            catch (Exception)
            {
                // Not a folder on disk, such as This PC
                return null;
            }
        }

        public int OnFileOk(IFileDialog pfd) { return S_OK; }
        public int OnFolderChanging(IFileDialog pfd, IShellItem psiFolder) { return S_OK; }
        public void OnFolderChange(IFileDialog pfd) { Suggest(pfd); }
        public void OnSelectionChange(IFileDialog pfd) { }
        public void OnShareViolation(IFileDialog pfd, IShellItem psi, out int pResponse) { pResponse = 0; }
        public void OnTypeChange(IFileDialog pfd) { Suggest(pfd); }
        public void OnOverwrite(IFileDialog pfd, IShellItem psi, out int pResponse) { pResponse = 0; }
    }

    /// <summary>
    /// Shortens a file name so the file fits the limits on file name and path length.
    /// </summary>
    static string FitName(string name, string extension, string folder)
    {
        int maxLength = MaxFilenameLength - extension.Length;
        if (folder != null)
            maxLength = Math.Min(maxLength, MaxPathLength - folder.TrimEnd('\\').Length - 1 - extension.Length);

        if (name.Length <= maxLength || maxLength <= 0)
            return name;

        name = name.Substring(0, char.IsHighSurrogate(name[maxLength - 1]) ? maxLength - 1 : maxLength);

        // Windows drops trailing dots and spaces from file names
        return name.TrimEnd('.', ' ');
    }

    #region Interop

    const int S_OK = 0;
    const int ERROR_CANCELLED = unchecked((int)0x800704C7);
    const uint FOS_OVERWRITEPROMPT = 0x2;
    const uint FOS_FORCEFILESYSTEM = 0x40;
    const uint FOS_PATHMUSTEXIST = 0x800;
    const uint SIGDN_FILESYSPATH = 0x80058000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B"), ClassInterface(ClassInterfaceType.None), TypeLibType(TypeLibTypeFlags.FCanCreate)]
    class FileSaveDialogRCW
    {
    }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), CoClass(typeof(FileSaveDialogRCW))]
    interface NativeFileSaveDialog : IFileDialog
    {
    }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
        void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int fdap);
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter([MarshalAs(UnmanagedType.Interface)] object pFilter);
    }

    [ComImport, Guid("973510DB-7D7F-452B-8975-74A85828D354"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialogEvents
    {
        [PreserveSig] int OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);
        [PreserveSig] int OnFolderChanging([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder);
        void OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);
        void OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);
        void OnShareViolation([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out int pResponse);
        void OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);
        void OnOverwrite([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out int pResponse);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, uint hint, out int piOrder);
    }

    #endregion
}

#endif
