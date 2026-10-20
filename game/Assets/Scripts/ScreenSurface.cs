using System.Collections.Generic;
using UnityEngine;

public struct ScreenLine
{
    public string text;
    public Color color;

    public ScreenLine(string text, Color color)
    {
        this.text = text;
        this.color = color;
    }
}

/// <summary>
/// Текст на физическом экране: набор строк-TextMesh, лежащих на кваде монитора.
/// Никакого Canvas/HUD — надписи живут в мире, их можно прочитать только
/// наведя взгляд на соответствующий экран.
/// </summary>
public class ScreenSurface : MonoBehaviour
{
    public Transform screen;
    public float characterSize = 0.0105f;
    public float lineHeight = 0.0152f;
    public float padX = 0.012f;
    public float padY = 0.010f;
    public Font font;

    public int Cols { get; private set; }
    public int Rows { get; private set; }

    readonly List<TextMesh> pool = new List<TextMesh>();
    readonly List<string> lastText = new List<string>();
    readonly List<Color> lastColor = new List<Color>();

    float width, height;

    public static ScreenSurface Create(Transform screenQuad, Font font,
                                       float characterSize = 0.0105f,
                                       float lineHeight = 0.0152f)
    {
        var go = new GameObject("Surface_" + screenQuad.name);
        go.transform.SetPositionAndRotation(
            screenQuad.position - screenQuad.forward * 0.004f,
            screenQuad.rotation);

        var s = go.AddComponent<ScreenSurface>();
        s.screen = screenQuad;
        s.font = font;
        s.characterSize = characterSize;
        s.lineHeight = lineHeight;
        s.Init();
        return s;
    }

    void Init()
    {
        width = Mathf.Abs(screen.lossyScale.x);
        height = Mathf.Abs(screen.lossyScale.y);

        float charWidth = characterSize * 0.602f; // DejaVu Sans Mono
        Cols = Mathf.Max(8, Mathf.FloorToInt((width - 2f * padX) / charWidth));
        Rows = Mathf.Max(4, Mathf.FloorToInt((height - 2f * padY) / lineHeight));

        for (int i = 0; i < Rows; i++)
        {
            var go = new GameObject("L" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                -width * 0.5f + padX,
                height * 0.5f - padY - i * lineHeight,
                0f);

            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 128;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = Color.white;
            tm.text = "";
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && font != null) mr.sharedMaterial = font.material;

            pool.Add(tm);
            lastText.Add(null);
            lastColor.Add(Color.clear);
        }
    }

    public string Fit(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= Cols) return text;
        return text.Substring(0, Cols);
    }

    /// <summary>Обновить содержимое экрана. Дешёвый: трогает только изменившиеся строки.</summary>
    public void Set(List<ScreenLine> lines)
    {
        for (int i = 0; i < Rows; i++)
        {
            TextMesh tm = pool[i];
            bool on = lines != null && i < lines.Count;
            if (tm.gameObject.activeSelf != on)
                tm.gameObject.SetActive(on);
            if (!on) continue;

            string t = Fit(lines[i].text);
            if (lastText[i] != t)
            {
                tm.text = t;
                lastText[i] = t;
            }
            if (lastColor[i] != lines[i].color)
            {
                tm.color = lines[i].color;
                lastColor[i] = lines[i].color;
            }
        }
    }
}
