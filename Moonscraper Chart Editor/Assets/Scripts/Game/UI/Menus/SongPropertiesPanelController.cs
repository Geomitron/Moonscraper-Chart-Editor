// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

//#undef UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System;
using MoonscraperEngine;
using MoonscraperChartEditor.Song;
using Row = SongPropertiesRows.Row;

public class SongPropertiesPanelController : TabMenu
{
    [Header("Song Settings")]
    public Toggle showAllKeysToggle;
    public ScrollRect rowsScrollRect;
    public RectTransform rowsContent;
    public MS_TMPro_InputField iniPreview;

    [Header("Row Templates")]
    public Text labelTemplate;
    public InputField inputTemplate;
    public Toggle toggleTemplate;
    public Dropdown dropdownTemplate;
    public Button buttonTemplate;

    [Header("Audio Settings")]
    public Text musicStream;
    public Text guitarStream;
    public Text bassStream;
    public Text rhythmStream;
	public Text keysStream;
	public Text vocalStream;
    public Text drum1Stream;
	public Text drum2Stream;
	public Text drum3Stream;
	public Text drum4Stream;
    public Text crowdStream;

    static readonly string[] validAudioExtensions = { "ogg", "wav", "mp3", "opus" };
    readonly ExtensionFilter audioExFilter = new ExtensionFilter("Audio files", validAudioExtensions);

    Dictionary<Song.AudioInstrument, Text> m_audioStreamTextLookup;

    // "Show all keys" stays as the charter left it while Moonscraper is open
    static bool s_showAllKeys = false;

    static readonly Color c_groupColor = new Color(0.16f, 0.16f, 0.16f, 0.9f);
    static readonly Color c_hintColor = new Color(0.72f, 0.72f, 0.72f);
    static readonly Color c_subheadingColor = new Color(0.68f, 0.68f, 0.68f);
    static readonly Color c_recommendedEmptyColor = new Color(1f, 0.914f, 0.722f);
    const float c_labelWidth = 212;
    const float c_numberWidth = 80;
    const float c_controlHeight = 24;

    SongPropertiesRows.ChartFacts m_chartFacts;
    HashSet<string> m_rowKeys;
    bool m_settingShowAllKeys = false;

    protected override void Awake()
    {
        base.Awake();
        showAllKeysToggle.onValueChanged.AddListener(OnShowAllKeysChanged);
    }

    protected override void OnEnable()
    {
        ResetToInitialMenuItem();

        m_audioStreamTextLookup = new Dictionary<Song.AudioInstrument, Text>()
        {
            { Song.AudioInstrument.Song, musicStream },
            { Song.AudioInstrument.Guitar, guitarStream },
            { Song.AudioInstrument.Bass, bassStream },
            { Song.AudioInstrument.Rhythm, rhythmStream },
            { Song.AudioInstrument.Keys, keysStream },
            { Song.AudioInstrument.Vocals, vocalStream },
            { Song.AudioInstrument.Drum, drum1Stream },
            { Song.AudioInstrument.Drums_2, drum2Stream },
            { Song.AudioInstrument.Drums_3, drum3Stream },
            { Song.AudioInstrument.Drums_4, drum4Stream },
            { Song.AudioInstrument.Crowd, crowdStream },
        };

        bool edit = ChartEditor.isDirty;

        m_chartFacts = SongPropertiesRows.ChartFacts.Of(editor.currentSong);
        if (m_rowKeys == null)
            m_rowKeys = new HashSet<string>(SongPropertiesRows.RowKeys());

        m_settingShowAllKeys = true;
        showAllKeysToggle.isOn = s_showAllKeys;
        m_settingShowAllKeys = false;
        BuildRows();

        base.OnEnable();

        // Init audio names
        setAudioTextLabels();

        ChartEditor.isDirty = edit;
        StartCoroutine(ScrollSetDelay());
    }

    IEnumerator ScrollSetDelay()
    {
        yield return null;
        rowsScrollRect.verticalNormalizedPosition = 1;
    }

    void OnShowAllKeysChanged(bool showAll)
    {
        if (m_settingShowAllKeys)
            return;

        s_showAllKeys = showAll;
        BuildRows();
    }

    #region Song rows

    void BuildRows()
    {
        foreach (Transform child in rowsContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        defaultSelectable = null;
        SongIni ini = editor.currentSong.iniProperties;

        foreach (SongPropertiesRows.Group group in SongPropertiesRows.Groups)
        {
            var otherKeys = new List<string>();
            if (group.otherKeys)
            {
                foreach (var pair in ini.pairs)
                {
                    if (!m_rowKeys.Contains(pair.Key))
                        otherKeys.Add(pair.Key);
                }
            }

            var shownRows = new List<Row>();
            string pendingSubheading = null;
            var subheadings = new Dictionary<Row, string>();
            foreach (Row row in group.rows)
            {
                if (row.subheading != null)
                {
                    pendingSubheading = row.subheading;
                    continue;
                }

                if (!SongPropertiesRows.IsVisible(row, HasValue(row), s_showAllKeys, m_chartFacts))
                    continue;

                if (pendingSubheading != null)
                {
                    subheadings[row] = pendingSubheading;
                    pendingSubheading = null;
                }

                shownRows.Add(row);
            }

            if (shownRows.Count == 0 && otherKeys.Count == 0 && !(group.otherKeys && s_showAllKeys))
                continue;

            AddGroupHeading(group.title);
            foreach (Row row in shownRows)
            {
                string subheading;
                if (subheadings.TryGetValue(row, out subheading))
                    AddSubheading(subheading);

                AddRow(row);
            }

            foreach (string key in otherKeys)
                AddOtherKeyRow(key);

            if (group.otherKeys && s_showAllKeys)
                AddNewKeyRow();
        }

        RefreshIniPreview();
    }

    bool HasValue(Row row)
    {
        Song song = editor.currentSong;
        switch (row.input)
        {
            case SongPropertiesRows.Input.DrumType:
                return SongPropertiesRows.IniDrumType(song.iniProperties).HasValue;
            case SongPropertiesRows.Input.Seconds:
                if (row.key == "delay")
                    return Mathf.RoundToInt(song.offset * 1000) != 0;
                break;
        }

        return song.iniProperties.Get(row.key) != string.Empty;
    }

    RectTransform AddLayoutObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return (RectTransform)gameObject.transform;
    }

    static LayoutElement SetLayoutSize(Component component, float preferredWidth, float preferredHeight, float flexibleWidth = 0)
    {
        LayoutElement layout = component.GetComponent<LayoutElement>();
        if (!layout)
            layout = component.gameObject.AddComponent<LayoutElement>();

        layout.preferredWidth = preferredWidth;
        layout.minWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;
        layout.flexibleWidth = flexibleWidth;
        // Above a text box's own size, which it measures from its text
        layout.layoutPriority = 2;
        return layout;
    }

    Text AddText(string text, Transform parent, int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
    {
        Text label = Instantiate(labelTemplate, parent, false);
        label.gameObject.SetActive(true);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = fontStyle;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        return label;
    }

    void AddGroupHeading(string title)
    {
        RectTransform heading = AddLayoutObject(title, rowsContent);
        heading.gameObject.AddComponent<Image>().color = c_groupColor;
        var layout = heading.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddText(title, heading, 15, Color.white, FontStyle.Bold);
    }

    void AddSubheading(string title)
    {
        RectTransform subheading = AddLayoutObject(title, rowsContent);
        var layout = subheading.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddText(title.ToUpperInvariant(), subheading, 11, c_subheadingColor);
    }

    // A row: its label with the hint under it, then its control
    RectTransform AddRowLayout(string name, string label, string hint)
    {
        RectTransform row = AddLayoutObject(name, rowsContent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 3, 3);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform labels = AddLayoutObject("Labels", row);
        var labelsLayout = labels.gameObject.AddComponent<VerticalLayoutGroup>();
        labelsLayout.childControlWidth = true;
        labelsLayout.childControlHeight = true;
        labelsLayout.childForceExpandWidth = true;
        labelsLayout.childForceExpandHeight = false;
        labelsLayout.childAlignment = TextAnchor.MiddleLeft;
        SetLayoutSize(labels, c_labelWidth, -1);

        AddText(label, labels, 14, Color.white);
        if (!string.IsNullOrEmpty(hint))
            AddText(hint, labels, 11, c_hintColor);

        return row;
    }

    InputField AddInput(Transform row, string text, float width)
    {
        InputField input = Instantiate(inputTemplate, row, false);
        input.gameObject.SetActive(true);
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit.RemoveAllListeners();
        SetPlaceholder(input, string.Empty);
        input.text = text;
        if (width > 0)
            SetLayoutSize(input, width, c_controlHeight);
        else
            SetLayoutSize(input, 0, c_controlHeight, 1);

        return input;
    }

    // A text box that wraps its value over a few lines. Enter still ends editing, and the box's validation keeps line breaks
    // out, since a song.ini value is one line.
    static void MakeMultiLine(InputField input, int lines)
    {
        input.lineType = InputField.LineType.MultiLineSubmit;
        Text text = input.textComponent;
        text.alignment = TextAnchor.UpperLeft;
        var placeholder = input.placeholder as Text;
        if (placeholder != null)
            placeholder.alignment = TextAnchor.UpperLeft;

        // The height of each line after the first, from the font's own line height (the size it's imported at), since a text
        // generator measures with the wrong size until the font has been drawn at the box's size
        float lineHeight = text.font.lineHeight * text.fontSize / text.font.fontSize * text.lineSpacing;
        SetLayoutSize(input, 0, c_controlHeight + (lines - 1) * lineHeight, 1);
    }

    Button AddButton(Transform row, string text, UnityEngine.Events.UnityAction onClick)
    {
        Button button = Instantiate(buttonTemplate, row, false);
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        Text label = button.GetComponentInChildren<Text>();
        label.text = text;
        label.fontSize = 12;
        SetLayoutSize(button, label.preferredWidth + 16, c_controlHeight);
        return button;
    }

    void AddFlexibleSpace(Transform row)
    {
        SetLayoutSize(AddLayoutObject("Space", row), 0, 0, 1);
    }

    void AddRow(Row row)
    {
        bool recommended = SongPropertiesRows.IsRecommended(row, m_chartFacts);
        RectTransform rowObject = AddRowLayout(row.key, row.label, SongPropertiesRows.HintText(row, recommended));
        SongIni ini = editor.currentSong.iniProperties;

        switch (row.input)
        {
            case SongPropertiesRows.Input.Bool:
                {
                    Toggle toggle = Instantiate(toggleTemplate, rowObject, false);
                    toggle.gameObject.SetActive(true);
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.isOn = ini.Get(row.key) == "True";
                    toggle.onValueChanged.AddListener(isOn => SetIniValue(row.key, isOn ? "True" : string.Empty));
                    SetLayoutSize(toggle, 20, 20);
                    AddFlexibleSpace(rowObject);
                    break;
                }

            case SongPropertiesRows.Input.Select:
                AddSelectDropdown(row, rowObject);
                break;

            case SongPropertiesRows.Input.DrumType:
                AddDrumTypeDropdown(rowObject);
                break;

            case SongPropertiesRows.Input.Seconds:
                {
                    InputField input = AddInput(rowObject, SecondsText(row), c_numberWidth);
                    input.onValidateInput = LocalesManager.ValidateDecimalInput;
                    MarkRecommendedEmpty(input, recommended);
                    input.onValueChanged.AddListener(text =>
                    {
                        SetSeconds(row, text);
                        MarkRecommendedEmpty(input, recommended);
                    });

                    if (row.playbackPositionButton)
                        AddButton(rowObject, "Use current playback position", () => input.text = FormatSeconds(CurrentPlaybackPosition()));

                    AddFlexibleSpace(rowObject);
                    break;
                }

            default:
                {
                    bool number = row.input == SongPropertiesRows.Input.Integer;
                    InputField input = AddInput(rowObject, ini.Get(row.key), number ? c_numberWidth : -1);
                    if (number)
                        input.contentType = InputField.ContentType.IntegerNumber;
                    else
                        input.onValidateInput = ValidateStringMetadataInput;

                    if (row.lines > 1)
                        MakeMultiLine(input, row.lines);

                    MarkRecommendedEmpty(input, recommended);
                    input.onValueChanged.AddListener(text =>
                    {
                        SetIniValue(row.key, text);
                        MarkRecommendedEmpty(input, recommended);
                        if (row.key == "name")
                            UpdateSongNameDisplays();
                    });

                    if (number)
                        AddFlexibleSpace(rowObject);
                    if (row.key == "name")
                        defaultSelectable = input;
                    break;
                }
        }
    }

    void AddSelectDropdown(Row row, Transform rowObject)
    {
        string value = editor.currentSong.iniProperties.Get(row.key);
        var values = new List<string>() { string.Empty };
        var options = new List<string>() { "Not set" };
        for (int i = 0; i < row.optionValues.Length; ++i)
        {
            values.Add(row.optionValues[i]);
            options.Add(row.optionLabels != null ? row.optionLabels[i] : row.optionValues[i]);
        }

        // A value the file sets that isn't one of the options stays as it is
        if (!values.Contains(value))
        {
            values.Add(value);
            options.Add(value);
        }

        AddDropdown(rowObject, options, values.IndexOf(value), index => SetIniValue(row.key, values[index]));
    }

    void AddDrumTypeDropdown(Transform rowObject)
    {
        SongIni ini = editor.currentSong.iniProperties;
        SongPropertiesRows.DrumType notesType = m_chartFacts.notesDrumType;
        SongPropertiesRows.DrumType? iniType = SongPropertiesRows.IniDrumType(ini);

        var types = new List<SongPropertiesRows.DrumType?>() { null };
        var options = new List<string>() { "From the notes (" + SongPropertiesRows.DrumTypeNames[(int)notesType] + ")" };
        foreach (SongPropertiesRows.DrumType type in EnumX<SongPropertiesRows.DrumType>.Values)
        {
            // Setting the type the notes show changes nothing, so it's a choice only when the file sets it
            if (type == SongPropertiesRows.DrumType.FourLane || (type == notesType && iniType != type))
                continue;

            types.Add(type);
            options.Add(SongPropertiesRows.DrumTypeNames[(int)type]);
        }

        AddDropdown(rowObject, options, Math.Max(0, types.IndexOf(iniType)), index =>
        {
            SetIniValue("pro_drums", types[index] == SongPropertiesRows.DrumType.Pro ? "True" : string.Empty);
            SetIniValue("five_lane_drums", types[index] == SongPropertiesRows.DrumType.FiveLane ? "True" : string.Empty);
        });
    }

    void AddDropdown(Transform rowObject, List<string> options, int selected, UnityEngine.Events.UnityAction<int> onChanged)
    {
        Dropdown dropdown = Instantiate(dropdownTemplate, rowObject, false);
        dropdown.gameObject.SetActive(true);
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.value = selected;
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(onChanged);
        SetLayoutSize(dropdown, 230, c_controlHeight);
        AddFlexibleSpace(rowObject);
    }

    void AddOtherKeyRow(string key)
    {
        RectTransform rowObject = AddRowLayout(key, key, "Text");
        // The rows' font draws no underscore, which key names have
        rowObject.Find("Labels").GetComponentInChildren<Text>().font = inputTemplate.textComponent.font;
        InputField input = AddInput(rowObject, editor.currentSong.iniProperties.Get(key), -1);
        input.onValidateInput = ValidateStringMetadataInput;
        input.onValueChanged.AddListener(text => SetIniValue(key, text));
        AddButton(rowObject, "X", () =>
        {
            editor.currentSong.iniProperties.Remove(key);
            ChartEditor.isDirty = true;
            BuildRows();
        });
    }

    void AddNewKeyRow()
    {
        RectTransform rowObject = AddLayoutObject("New key", rowsContent);
        var layout = rowObject.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 3, 6);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        InputField key = AddInput(rowObject, string.Empty, c_labelWidth);
        key.onValidateInput = ValidateStringMetadataInput;
        SetPlaceholder(key, "Key");
        InputField value = AddInput(rowObject, string.Empty, -1);
        value.onValidateInput = ValidateStringMetadataInput;
        SetPlaceholder(value, "Value");
        AddButton(rowObject, "Add key", () =>
        {
            string newKey = key.text.Trim().ToLowerInvariant();
            if (newKey == string.Empty || newKey.Contains("="))
                return;

            SetIniValue(newKey, value.text);
            BuildRows();
        });
    }

    static void SetPlaceholder(InputField input, string text)
    {
        Text placeholder = input.placeholder as Text;
        if (placeholder)
        {
            placeholder.text = text;
            placeholder.gameObject.SetActive(true);
        }
    }

    static void MarkRecommendedEmpty(InputField input, bool recommended)
    {
        Graphic background = input.targetGraphic;
        if (background)
            background.color = recommended && input.text.Trim() == string.Empty ? c_recommendedEmptyColor : Color.white;
    }

    void SetIniValue(string key, string value)
    {
        editor.currentSong.iniProperties.Set(key, value);
        ChartEditor.isDirty = true;
        RefreshIniPreview();
    }

    // A seconds row's box shows seconds; song.ini has milliseconds, and the offset is the song's
    string SecondsText(Row row)
    {
        if (row.key == "delay")
            return editor.currentSong.offset == 0 ? string.Empty : FormatSeconds(editor.currentSong.offset);

        int ms;
        string value = editor.currentSong.iniProperties.Get(row.key);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
            return FormatSeconds(ms / 1000.0f);

        return value;
    }

    void SetSeconds(Row row, string text)
    {
        float seconds;
        bool isNumber = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds);

        if (row.key == "delay")
        {
            editor.currentSong.offset = isNumber ? seconds : 0;
            ChartEditor.isDirty = true;
            RefreshIniPreview();
            return;
        }

        string ms = isNumber ? Mathf.RoundToInt(seconds * 1000).ToString(CultureInfo.InvariantCulture) : string.Empty;
        SetIniValue(row.key, ms);
    }

    static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.###", CultureInfo.CurrentCulture);
    }

    // The audio's position at the strikeline, which song.ini's preview times count in
    float CurrentPlaybackPosition()
    {
        return Mathf.Max(0, editor.currentVisibleTime + editor.currentSong.offset);
    }

    void UpdateSongNameDisplays()
    {
        editor.uiServices.editorPanels.displayProperties.UpdateSongNameText();
        editor.RepaintWindowText();
    }

    void RefreshIniPreview()
    {
        iniPreview.text = SongIniFunctions.SavedText(editor.CopyIniPropertiesForSaving(), editor.currentSong.offset);
    }

    #endregion

    void ClipText(Text text)
    {
        float maxWidth = text.rectTransform.rect.width;
        if (text.preferredWidth > maxWidth)
        {
            int removePos = text.text.Length - 1;
            text.text += "...";

            while (removePos > 0 && text.preferredWidth > maxWidth)
            {
                text.text = text.text.Remove(removePos--, 1);
            }
        }
    }

   void setAudioTextLabels()
    {
        Song song = editor.currentSong;

        foreach (var audio in EnumX<Song.AudioInstrument>.Values)
        {
            Text audioStreamText;

            if (!m_audioStreamTextLookup.TryGetValue(audio, out audioStreamText))
            {
                Debug.Assert(false, "Audio stream UI Text not linked to an Audio Instrument for instrument " + audio.ToString());
                continue;
            }

            if (editor.currentSongAudio.GetAudioIsLoaded(audio))
            {
                audioStreamText.color = Color.white;
                audioStreamText.text = song.GetAudioName(audio);
                ClipText(audioStreamText);
            }
            else
            {
                audioStreamText.color = Color.red;
                audioStreamText.text = "No audio";
            }
        }

        // song_length is measured from the audio
        RefreshIniPreview();

        ChartEditor.isDirty = true;
    }

    string GetAudioFile()
    {
        string audioFilepath = string.Empty;
        string defExt = string.Empty;
        foreach(string extention in validAudioExtensions)
        {
            if (defExt != string.Empty)
                defExt += ",";

            defExt += extention;
        }

        FileExplorer.OpenFilePanel(audioExFilter, defExt, out audioFilepath);
        return audioFilepath;
    }

    void LoadAudioStream(Song.AudioInstrument audioInstrument)
    {
        try
        {
            string filepath = GetAudioFile();
            if (editor.currentSongAudio.LoadAudio(filepath, audioInstrument))
            {
                // Record the filepath
                editor.currentSong.SetAudioLocation(audioInstrument, filepath);
                editor.RecordAudioOverrides();
                StartCoroutine(SetAudio());
            }
        }
        catch (Exception e)
        {
            Logger.LogException(e, "Could not open audio");
        }
    }

    void ClearAudioStream(Song.AudioInstrument audio)
    {
        editor.currentSongAudio.Clear(audio);
        editor.currentSong.SetAudioLocation(audio, string.Empty);
        editor.RecordAudioOverrides();

        setAudioTextLabels();
    }

    public void RefreshAllAudioStreams()
    {
        StartCoroutine(_RefreshAllAudioStreams());
    }

    IEnumerator _RefreshAllAudioStreams()
    {
        editor.ReloadAudio();

        LoadingTasksManager tasksManager = editor.services.loadingTasksManager;

        while (tasksManager.isRunningTask)
            yield return null;

        setAudioTextLabels();
    }

    IEnumerator SetAudio()
    {
        LoadingTasksManager tasksManager = editor.services.loadingTasksManager;

        List<LoadingTask> tasks = new List<LoadingTask>()
        {
            new LoadingTask("Loading audio", () =>
            {
                while (editor.currentSongAudio.isAudioLoading) ;
            })
        };

        tasksManager.KickTasks(tasks);

        while (tasksManager.isRunningTask)
            yield return null;

        setAudioTextLabels();
    }

    // song.ini has a key on each line
    public static char ValidateStringMetadataInput(string text, int charIndex, char addedChar)
    {
        if (char.IsControl(addedChar))
        {
            return '\0';
        }

        return addedChar;
    }

    // Unity doesn't support calling methods with enum parameters
    // From button click handlers, so we define load/clear audio
    // implementations for each case here.
    #region Unity button click handlers
    public void LoadMusicStream()
    {
        LoadAudioStream(Song.AudioInstrument.Song);
    }

    public void ClearMusicStream()
    {
        ClearAudioStream(Song.AudioInstrument.Song);
    }

    public void LoadGuitarStream()
    {
        LoadAudioStream(Song.AudioInstrument.Guitar);
    }

    public void ClearGuitarStream()
    {
        ClearAudioStream(Song.AudioInstrument.Guitar);
    }

    public void LoadBassStream()
    {
        LoadAudioStream(Song.AudioInstrument.Bass);
    }

    public void ClearBassStream()
    {
        ClearAudioStream(Song.AudioInstrument.Bass);
    }

    public void LoadRhythmStream()
    {
        LoadAudioStream(Song.AudioInstrument.Rhythm);
    }

    public void ClearRhythmStream()
    {
        ClearAudioStream(Song.AudioInstrument.Rhythm);
    }

    public void LoadVocalStream()
    {
        LoadAudioStream(Song.AudioInstrument.Vocals);
    }

    public void ClearVocalStream()
    {
        ClearAudioStream(Song.AudioInstrument.Vocals);
    }

    public void LoadKeysStream()
    {
        LoadAudioStream(Song.AudioInstrument.Keys);
    }

    public void ClearKeysStream()
    {
        ClearAudioStream(Song.AudioInstrument.Keys);
    }

    public void LoadDrum1Stream()
    {
        LoadAudioStream(Song.AudioInstrument.Drum);
    }

    public void ClearDrum1Stream()
    {
        ClearAudioStream(Song.AudioInstrument.Drum);
    }

    public void LoadDrum2Stream()
    {
        LoadAudioStream(Song.AudioInstrument.Drums_2);
    }

    public void ClearDrum2Stream()
    {
        ClearAudioStream(Song.AudioInstrument.Drums_2);
    }

    public void LoadDrum3Stream()
    {
        LoadAudioStream(Song.AudioInstrument.Drums_3);
    }

    public void ClearDrum3Stream()
    {
        ClearAudioStream(Song.AudioInstrument.Drums_3);
    }

    public void LoadDrum4Stream()
    {
        LoadAudioStream(Song.AudioInstrument.Drums_4);
    }

    public void ClearDrum4Stream()
    {
        ClearAudioStream(Song.AudioInstrument.Drums_4);
    }

    public void LoadCrowdStream()
    {
        LoadAudioStream(Song.AudioInstrument.Crowd);
    }

    public void ClearCrowdStream()
    {
        ClearAudioStream(Song.AudioInstrument.Crowd);
    }
    #endregion
}
