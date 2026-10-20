using UnityEngine;

/// <summary>
/// Плёночные эффекты поверх мира: виньетка тревоги, вспышки провалов,
/// субтитры диктофона, строка подсказок, экраны смерти и конца смены.
/// Это не игровой UI — взаимодействие целиком на физических экранах.
/// </summary>
public class OverlayUI : MonoBehaviour
{
    NightDirector dir;
    Font font;

    Texture2D vignette;
    Texture2D white;

    GUIStyle center, title, endStyle, callName, callLine, callInput, hintStyle, subtitleStyle;
    GUIStyle actionGray, actionTyped, actionError;

    public void Init(NightDirector director, Font uiFont)
    {
        dir = director;
        font = uiFont;

        vignette = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        vignette.wrapMode = TextureWrapMode.Clamp;
        float cx = 127.5f, cy = 127.5f, max = 181f;
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / max;
                float t = Mathf.InverseLerp(0.42f, 1.0f, d);
                vignette.SetPixel(x, y, new Color(0f, 0f, 0f, t * t));
            }
        }
        vignette.Apply();

        white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        white.SetPixel(0, 0, Color.white);
        white.Apply();
    }

    void EnsureStyles()
    {
        if (center != null) return;

        center = new GUIStyle(GUI.skin.label);
        center.font = font;
        center.fontSize = Mathf.RoundToInt(Screen.height * 0.026f);
        center.alignment = TextAnchor.MiddleCenter;
        center.wordWrap = true;
        center.normal.textColor = Color.white;

        title = new GUIStyle(center);
        title.fontSize = Mathf.RoundToInt(Screen.height * 0.052f);
        title.normal.textColor = new Color(0.90f, 0.96f, 0.92f);

        endStyle = new GUIStyle(center);
        endStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.021f);
        endStyle.normal.textColor = new Color(0.80f, 0.86f, 0.82f);

        callName = new GUIStyle(center);
        callName.fontSize = Mathf.RoundToInt(Screen.height * 0.019f);
        callName.alignment = TextAnchor.MiddleLeft;
        callName.normal.textColor = new Color(1f, 0.76f, 0.29f);

        callLine = new GUIStyle(center);
        callLine.fontSize = Mathf.RoundToInt(Screen.height * 0.021f);
        callLine.alignment = TextAnchor.MiddleLeft;
        callLine.normal.textColor = new Color(0.90f, 0.93f, 0.90f);

        callInput = new GUIStyle(center);
        callInput.fontSize = Mathf.RoundToInt(Screen.height * 0.021f);
        callInput.alignment = TextAnchor.MiddleLeft;
        callInput.normal.textColor = Color.white;

        hintStyle = new GUIStyle(center);
        hintStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.017f);
        hintStyle.normal.textColor = new Color(0.62f, 0.66f, 0.63f, 0.85f);

        subtitleStyle = new GUIStyle(center);
        subtitleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.022f);
        subtitleStyle.normal.textColor = new Color(0.92f, 0.95f, 0.92f);

        actionGray = new GUIStyle(center);
        actionGray.fontSize = Mathf.RoundToInt(Screen.height * 0.023f);
        actionGray.alignment = TextAnchor.MiddleLeft;
        actionGray.wordWrap = false;
        actionGray.normal.textColor = new Color(0.55f, 0.57f, 0.55f, 0.95f);

        actionTyped = new GUIStyle(actionGray);
        actionTyped.normal.textColor = new Color(0.96f, 0.98f, 0.96f);

        actionError = new GUIStyle(actionGray);
        actionError.normal.textColor = new Color(1f, 0.36f, 0.36f, 0.95f);
    }

    void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width;
        float h = Screen.height;

        if (dir == null) return;

        // виньетка тревоги
        if (dir.VignetteAlpha > 0.005f)
        {
            GUI.color = new Color(1f, 1f, 1f, dir.VignetteAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), vignette);
            GUI.color = Color.white;
        }

        // вспышка провала / смены личности
        if (dir.FlashAlpha > 0.005f)
        {
            GUI.color = new Color(dir.FlashColor.r, dir.FlashColor.g, dir.FlashColor.b, dir.FlashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
            GUI.color = Color.white;
        }

        if (dir.Running &&
            (dir.Modal == ModalKind.None || dir.Modal == ModalKind.Call))
        {
            DrawCrosshair(w, h);
            if (dir.ActionVisible) DrawAction(w, h);
        }

        if (dir.Modal == ModalKind.Intro)
            DrawIntro(w, h);
        else if (dir.Modal == ModalKind.Rest)
            DrawRest(w, h);
        else if (dir.Modal == ModalKind.Call)
            DrawCall(w, h);
        else if (dir.Modal == ModalKind.Death || dir.Modal == ModalKind.End)
            DrawEnd(w, h);

        if ((dir.Modal == ModalKind.None || dir.Modal == ModalKind.Dictaphone)
            && !string.IsNullOrEmpty(dir.Subtitle))
        {
            Rect band = new Rect(w * 0.5f - Mathf.Min(460f, w * 0.44f), h - h * 0.16f,
                                 Mathf.Min(920f, w * 0.88f), h * 0.10f);
            DrawBand(band, 0.62f);
            GUI.Label(band, dir.Subtitle, subtitleStyle);
        }

        if (dir.Modal == ModalKind.None && dir.Running && !dir.ActionVisible &&
            !string.IsNullOrEmpty(dir.Hint))
        {
            var r = new Rect(0f, h - h * 0.035f, w, h * 0.03f);
            GUI.Label(r, dir.Hint, hintStyle);
        }
    }

    void DrawCrosshair(float w, float h)
    {
        float s = Mathf.Max(3f, Mathf.Round(h * 0.0035f));
        float x = w * 0.5f - s * 0.5f;
        float y = h * 0.5f - s * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, s + 2f, s + 2f), white);
        GUI.color = new Color(0.92f, 0.95f, 0.92f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, s, s), white);
        GUI.color = Color.white;
    }

    void DrawAction(float w, float h)
    {
        string prompt = dir.ActionPrompt;
        if (string.IsNullOrEmpty(prompt)) return;

        int typed = Mathf.Clamp(dir.ActionTyped, 0, prompt.Length);
        int prefix = FilledPrefix(prompt, typed);
        GUIStyle gray = dir.ActionRejected ? actionError : actionGray;

        float charW = gray.CalcSize(new GUIContent("M")).x;
        float charH = gray.lineHeight > 1f ? gray.lineHeight : h * 0.03f;
        float textW = charW * prompt.Length;
        float x = w * 0.5f - textW * 0.5f;
        float y = h * 0.5f + h * 0.035f;

        DrawBand(new Rect(x - 16f, y - 6f, textW + 32f, charH + 12f), 0.55f);

        GUI.Label(new Rect(x, y, textW, charH), prompt, gray);
        if (prefix > 0)
            GUI.Label(new Rect(x, y, textW, charH), prompt.Substring(0, prefix), actionTyped);
    }

    /// <summary>Сколько символов подсказки закрашено, если набрано typed букв.</summary>
    static int FilledPrefix(string prompt, int typed)
    {
        int n = 0;
        for (int i = 0; i < prompt.Length; i++)
        {
            if (prompt[i] == ' ') continue;
            if (n >= typed) return i;
            n++;
        }
        return prompt.Length;
    }

    void DrawIntro(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, dir.IntroAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
        GUI.color = Color.white;

        if (dir.IntroAlpha > 0.35f)
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(dir.IntroAlpha));
            GUI.Label(new Rect(0f, h * 0.38f, w, h * 0.12f), "VOID: NO RETURN", title);
            GUI.Label(new Rect(0f, h * 0.52f, w, h * 0.06f), "НОЧЬ 1 · «СМЕНА»", center);
            GUI.color = Color.white;
        }
    }

    void DrawRest(float w, float h)
    {
        float a = 0.75f * dir.RestProgress;
        GUI.color = new Color(0f, 0f, 0f, a);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
        GUI.color = Color.white;

        GUI.Label(new Rect(0f, h * 0.44f, w, h * 0.05f), "ОТДЫХ", title);
        GUI.Label(new Rect(0f, h * 0.52f, w, h * 0.05f),
            Mathf.RoundToInt(dir.RestProgress * 100f) + "%", center);
    }

    void DrawCall(float w, float h)
    {
        float bw = Mathf.Min(900f, w * 0.9f);
        var band = new Rect(w * 0.5f - bw * 0.5f, h - h * 0.34f, bw, h * 0.26f);
        DrawBand(band, 0.82f);

        var nameR = new Rect(band.x + 24f, band.y + 14f, band.width - 48f, 30f);
        GUI.Label(nameR, "ТЕЛЕФОН · ВИКТОР", callName);

        var lineR = new Rect(band.x + 24f, band.y + 48f, band.width - 48f, 54f);
        GUI.Label(lineR, dir.CallLine, callLine);

        if (dir.CallAwaitingAnswer)
        {
            string shown = "ОТВЕТ: " + dir.CallAnswer +
                           (dir.CallTypo ? " ▌" : (Time.time % 1f < 0.55f ? "_" : " "));
            var inR = new Rect(band.x + 24f, band.y + 104f, band.width - 48f, 34f);
            GUI.Label(inR, shown, dir.CallTypo
                ? Tinted(callInput, new Color(1f, 0.36f, 0.36f))
                : callInput);

            GUI.Label(new Rect(band.x + 24f, band.y + band.height - 38f, band.width - 48f, 28f),
                      "ENTER — сказать", hintStyle);
        }
        else
        {
            GUI.Label(new Rect(band.x + 24f, band.y + 110f, band.width - 48f, 34f),
                      "…", callLine);
        }
    }

    GUIStyle Tinted(GUIStyle src, Color c)
    {
        var s = new GUIStyle(src);
        s.normal.textColor = c;
        return s;
    }

    void DrawEnd(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.93f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
        GUI.color = Color.white;

        GUI.Label(new Rect(0f, h * 0.16f, w, h * 0.09f), dir.EndTitle, title);

        string body = "";
        for (int i = 0; i < dir.EndLines.Length; i++)
            body += dir.EndLines[i] + (i + 1 < dir.EndLines.Length ? "\n" : "");

        GUI.Label(new Rect(w * 0.12f, h * 0.29f, w * 0.76f, h * 0.58f), body, endStyle);
    }

    void DrawBand(Rect r, float alpha)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(r, white);
        GUI.color = Color.white;
    }
}
