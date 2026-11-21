using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Row = GameSettings.SongIniSaveSetting.Row;

/// <summary>
/// The list in Settings > Editor of the song.ini keys and values File > New sets, below the tab's other settings and in their
/// column. Each row has a box for the key, one for the value and a button that removes the row; a button below the rows adds one.
/// Each time the tab is shown, the rows are rebuilt from what config.ini keeps, so they show what a restart would.
/// </summary>
public class NewSongIniList
{
    public const string Heading = "New Chart song.ini-";
    public const string KeyPlaceholder = "Key";
    public const string ValuePlaceholder = "Value";
    public const string RemoveText = "X";
    public const string AddText = "Add key";

    const float HeadingHeight = 30;
    const float RowHeight = 20;
    const float RowSpacing = 26;
    const float Gap = 4;
    const float KeyWidth = 130;
    const float RemoveWidth = 22;
    const float AddWidth = 80;
    const float BottomMargin = 10;

    class RowObjects
    {
        public RectTransform rect;
        public InputField key;
        public InputField value;
    }

    // Calls back each time its object, the tab's content, is shown
    class ShownListener : MonoBehaviour
    {
        public Action onShown;

        void OnEnable()
        {
            if (onShown != null)
                onShown();
        }
    }

    readonly GameSettings.SongIniSaveSetting m_setting;
    readonly RectTransform m_group;
    readonly InputField m_inputTemplate;
    readonly Button m_buttonTemplate;
    readonly Action m_onResized;
    readonly float m_left;
    readonly float m_width;
    readonly float m_rowsTop;
    readonly List<RowObjects> m_rowObjects = new List<RowObjects>();
    readonly RectTransform m_addButton;

    /// <param name="group">The tab's content, whose height sets how far the tab scrolls</param>
    /// <param name="label">A label of the tab's column, whose left edge the list starts at</param>
    /// <param name="input">A text box of the tab's column, whose right edge the list ends at</param>
    /// <param name="onResized">Called when the list's height changes</param>
    public NewSongIniList(GameSettings.SongIniSaveSetting setting, RectTransform group, Text label, InputField input, Button buttonTemplate, Action onResized)
    {
        m_setting = setting;
        m_group = group;
        m_inputTemplate = input;
        m_buttonTemplate = buttonTemplate;
        m_onResized = onResized;

        m_left = LocalX(label.rectTransform, 0);
        m_width = LocalX((RectTransform)input.transform, 1) - m_left;
        float top = group.rect.height;

        Text heading = UnityEngine.Object.Instantiate(label, group, false);
        heading.name = "New Song Ini Heading";
        heading.text = Heading;
        Place(heading.rectTransform, m_left, top, m_width, HeadingHeight);
        m_rowsTop = top + HeadingHeight;

        m_addButton = CreateButton(group, "New Song Ini Add", AddText, AddWidth, AddRow);
        m_addButton.anchoredPosition = new Vector2(m_left, 0);

        Rebuild();
        group.gameObject.AddComponent<ShownListener>().onShown = Rebuild;
    }

    // The x of the rect's left (0) or right (1) edge, from the group's left edge
    float LocalX(RectTransform rect, int edge)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 local = m_group.InverseTransformPoint(corners[edge == 0 ? 0 : 2]);
        return local.x - m_group.rect.xMin;
    }

    // Puts the rect's top left corner at x and y from the parent's top left corner
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    // Replaces the rows with the ones config.ini keeps: keys normalized, one row per key, and no row that saves nothing
    void Rebuild()
    {
        foreach (RowObjects rowObjects in m_rowObjects)
        {
            UnityEngine.Object.Destroy(rowObjects.rect.gameObject);
        }
        m_rowObjects.Clear();

        m_setting.Normalize();
        foreach (Row row in m_setting.rows)
        {
            CreateRow(row);
        }

        Layout();
    }

    void AddRow()
    {
        Row row = new Row();
        m_setting.rows.Add(row);
        InputField key = CreateRow(row);
        Layout();
        key.Select();
        key.ActivateInputField();
    }

    // The row's objects, returning its key box
    InputField CreateRow(Row row)
    {
        GameObject rowObject = new GameObject("New Song Ini Row", typeof(RectTransform));
        RowObjects rowObjects = new RowObjects();
        rowObjects.rect = (RectTransform)rowObject.transform;
        rowObjects.rect.SetParent(m_group, false);
        Place(rowObjects.rect, m_left, 0, m_width, RowHeight);
        m_rowObjects.Add(rowObjects);

        float valueWidth = m_width - KeyWidth - RemoveWidth - Gap * 2;
        rowObjects.key = CreateInput(rowObjects.rect, "Key", KeyPlaceholder, row.key, 0, KeyWidth);
        rowObjects.key.onValueChanged.AddListener(text => row.key = text);
        rowObjects.value = CreateInput(rowObjects.rect, "Value", ValuePlaceholder, row.value, KeyWidth + Gap, valueWidth);
        rowObjects.value.onValueChanged.AddListener(text => row.value = text);

        RectTransform remove = CreateButton(rowObjects.rect, "Remove", RemoveText, RemoveWidth, () =>
        {
            m_setting.rows.Remove(row);
            m_rowObjects.Remove(rowObjects);
            UnityEngine.Object.Destroy(rowObject);
            Layout();
        });
        remove.anchoredPosition = new Vector2(m_width - RemoveWidth, 0);

        return rowObjects.key;
    }

    InputField CreateInput(RectTransform rowRect, string name, string placeholder, string text, float x, float width)
    {
        InputField input = UnityEngine.Object.Instantiate(m_inputTemplate, rowRect, false);
        input.name = name;
        input.gameObject.SetActive(true);
        input.onValueChanged = new InputField.OnChangeEvent();
        input.onEndEdit = new InputField.SubmitEvent();
        input.contentType = InputField.ContentType.Standard;
        input.lineType = InputField.LineType.SingleLine;
        input.characterValidation = InputField.CharacterValidation.None;
        input.characterLimit = 0;
        input.onValidateInput = SongPropertiesPanelController.ValidateStringMetadataInput;
        input.text = text;

        Text placeholderText = input.placeholder as Text;
        if (placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.gameObject.SetActive(true);
        }

        Place((RectTransform)input.transform, x, 0, width, RowHeight);
        return input;
    }

    RectTransform CreateButton(RectTransform parent, string name, string text, float width, UnityEngine.Events.UnityAction onClick)
    {
        Button button = UnityEngine.Object.Instantiate(m_buttonTemplate, parent, false);
        button.name = name;
        button.gameObject.SetActive(true);
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        button.GetComponentInChildren<Text>(true).text = text;

        RectTransform rect = (RectTransform)button.transform;
        Place(rect, 0, 0, width, RowHeight);
        return rect;
    }

    // Places the rows and the button below them, and links the boxes for Tab (down) and Shift+Tab (up): key, value, next row's key
    void Layout()
    {
        for (int i = 0; i < m_rowObjects.Count; ++i)
        {
            RowObjects rowObjects = m_rowObjects[i];
            rowObjects.rect.anchoredPosition = new Vector2(m_left, -(m_rowsTop + i * RowSpacing));

            Navigation keyNavigation = new Navigation() { mode = Navigation.Mode.Explicit };
            keyNavigation.selectOnDown = rowObjects.value;
            keyNavigation.selectOnUp = i > 0 ? m_rowObjects[i - 1].value : null;
            rowObjects.key.navigation = keyNavigation;

            Navigation valueNavigation = new Navigation() { mode = Navigation.Mode.Explicit };
            valueNavigation.selectOnDown = i + 1 < m_rowObjects.Count ? m_rowObjects[i + 1].key : null;
            valueNavigation.selectOnUp = rowObjects.key;
            rowObjects.value.navigation = valueNavigation;
        }

        float addTop = m_rowsTop + m_rowObjects.Count * RowSpacing;
        m_addButton.anchoredPosition = new Vector2(m_left, -addTop);
        m_group.sizeDelta = new Vector2(m_group.sizeDelta.x, addTop + RowHeight + BottomMargin);
        m_onResized();
    }
}
