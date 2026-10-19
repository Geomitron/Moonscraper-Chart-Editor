public struct ExtensionFilter
{
    public string name;
    public string[] extensions;

    /// <summary>
    /// In a save dialog, the file name to suggest when this file type is chosen, instead of the dialog's default name. Null to use that.
    /// </summary>
    public string defaultFileName;

    public ExtensionFilter(string filterName, params string[] filterExtensions)
    {
        name = filterName;
        extensions = filterExtensions;
        defaultFileName = null;
    }
}
