using System.Collections.Generic;

/// <summary>
/// A song's song.ini keys, each key that has a value once, in the order <see cref="SongIniKeys.Normalize"/> writes them. The song's
/// offset is <c>Song.offset</c>, which song.ini writes as <c>delay</c>, so it isn't one of these keys.
/// </summary>
public class SongIni
{
    List<KeyValuePair<string, string>> m_pairs = new List<KeyValuePair<string, string>>();

    public SongIni()
    {
    }

    public SongIni(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        m_pairs = SongIniKeys.Normalize(pairs);
        m_pairs.RemoveAll(pair => pair.Key == "delay");
    }

    public SongIni(SongIni songIni)
    {
        m_pairs = new List<KeyValuePair<string, string>>(songIni.m_pairs);
    }

    public IList<KeyValuePair<string, string>> pairs { get { return m_pairs.AsReadOnly(); } }

    public bool IsEmpty { get { return m_pairs.Count == 0; } }

    /// <summary>
    /// The key's value, or an empty string if it isn't set.
    /// </summary>
    public string Get(string key)
    {
        key = key.Trim().ToLowerInvariant();
        foreach (var pair in m_pairs)
        {
            if (pair.Key == key)
                return pair.Value;
        }

        return string.Empty;
    }

    /// <summary>
    /// Sets the key's value, keeping its place. A value that means not set, such as an empty one, removes the key.
    /// </summary>
    public void Set(string key, string value)
    {
        key = key.Trim().ToLowerInvariant();
        var pairs = new List<KeyValuePair<string, string>>(m_pairs);
        int index = pairs.FindIndex(pair => pair.Key == key);
        var newPair = new KeyValuePair<string, string>(key, value ?? string.Empty);

        if (index >= 0)
            pairs[index] = newPair;
        else
            pairs.Add(newPair);

        m_pairs = SongIniKeys.Normalize(pairs);
    }

    public void Remove(string key)
    {
        Set(key, string.Empty);
    }
}
