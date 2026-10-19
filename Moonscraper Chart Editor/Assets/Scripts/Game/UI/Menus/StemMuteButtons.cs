// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MoonscraperChartEditor.Song;

/// <summary>
/// Mute and Solo buttons beside each song audio volume slider in Settings > Audio, like Audacity's.
/// Clicking Solo plays only that audio; Shift-clicking it adds to the soloed audio.
/// The rows of audio the song doesn't have are dimmed.
/// </summary>
public class StemMuteButtons
{
    const float ButtonWidth = 22;
    const float ButtonHeight = 20;
    const float ButtonGap = 4;
    const float DimmedAlpha = 0.35f;

    static readonly Color c_offColour = new Color(0.45f, 0.45f, 0.45f);
    static readonly Color c_mutedColour = new Color(0.85f, 0.25f, 0.25f);
    static readonly Color c_soloedColour = new Color(0.95f, 0.75f, 0.2f);
    static readonly Color c_offTextColour = Color.white;
    static readonly Color c_mutedTextColour = Color.white;
    static readonly Color c_soloedTextColour = Color.black;

    class Row
    {
        public Song.AudioInstrument audio;
        public Image muteImage;
        public Text muteText;
        public Image soloImage;
        public Text soloText;
        public List<CanvasGroup> dimmed = new List<CanvasGroup>();
    }

    readonly List<Row> m_rows = new List<Row>();

    public StemMuteButtons(IDictionary<Song.AudioInstrument, Slider> sliders)
    {
        foreach (var slider in sliders)
        {
            m_rows.Add(CreateRow(slider.Key, slider.Value));
        }
    }

    /// <summary>
    /// Shows each audio's mute and solo state, and dims the audio the current song doesn't have.
    /// </summary>
    public void Refresh()
    {
        SongAudioManager songAudio = ChartEditor.Instance.currentSongAudio;

        foreach (Row row in m_rows)
        {
            bool muted = StemMute.IsMuted(row.audio);
            bool soloed = StemMute.IsSoloed(row.audio);

            row.muteImage.color = muted ? c_mutedColour : c_offColour;
            row.muteText.color = muted ? c_mutedTextColour : c_offTextColour;
            row.soloImage.color = soloed ? c_soloedColour : c_offColour;
            row.soloText.color = soloed ? c_soloedTextColour : c_offTextColour;

            float alpha = songAudio.GetAudioIsLoaded(row.audio) ? 1 : DimmedAlpha;
            foreach (CanvasGroup group in row.dimmed)
            {
                group.alpha = alpha;
            }
        }
    }

    Row CreateRow(Song.AudioInstrument audio, Slider slider)
    {
        Row row = new Row() { audio = audio };
        RectTransform sliderRect = (RectTransform)slider.transform;
        Text label = FindLabel(sliderRect);
        Image sliderBackground = slider.transform.Find("Background").GetComponent<Image>();

        float x = sliderRect.anchoredPosition.x + sliderRect.sizeDelta.x * (1 - sliderRect.pivot.x) + ButtonGap + ButtonWidth / 2;
        GameObject mute = CreateButton(sliderRect, "M", x, sliderBackground, label, () => { StemMute.ToggleMute(audio); }, out row.muteImage, out row.muteText);
        GameObject solo = CreateButton(sliderRect, "S", x + ButtonWidth + ButtonGap, sliderBackground, label, () => { StemMute.ToggleSolo(audio, Globals.secondaryInputActive); }, out row.soloImage, out row.soloText);

        foreach (GameObject dimmed in new GameObject[] { slider.gameObject, label != null ? label.gameObject : null, mute, solo })
        {
            if (dimmed == null)
                continue;

            CanvasGroup group = dimmed.GetComponent<CanvasGroup>();
            row.dimmed.Add(group != null ? group : dimmed.AddComponent<CanvasGroup>());
        }

        return row;
    }

    GameObject CreateButton(RectTransform sliderRect, string text, float x, Image style, Text font, UnityEngine.Events.UnityAction onClick, out Image image, out Text label)
    {
        GameObject button = new GameObject(sliderRect.name.Trim() + " " + text, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(sliderRect.parent, false);
        rect.anchorMin = sliderRect.anchorMin;
        rect.anchorMax = sliderRect.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rect.anchoredPosition = new Vector2(x, sliderRect.anchoredPosition.y);

        image = button.GetComponent<Image>();
        image.sprite = style.sprite;
        image.type = style.type;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 14;
        label.fontStyle = FontStyle.Bold;
        label.font = font != null ? font.font : Resources.GetBuiltinResource<Font>("Arial.ttf");

        Button buttonComponent = button.GetComponent<Button>();
        buttonComponent.targetGraphic = image;
        buttonComponent.onClick.AddListener(() =>
        {
            onClick();
            Refresh();
        });

        return button;
    }

    // The slider's label is the text beside it on the same row
    static Text FindLabel(RectTransform sliderRect)
    {
        foreach (Transform sibling in sliderRect.parent)
        {
            Text text = sibling.GetComponent<Text>();
            if (text != null && Mathf.Abs(((RectTransform)sibling).anchoredPosition.y - sliderRect.anchoredPosition.y) < 1)
                return text;
        }

        return null;
    }
}
