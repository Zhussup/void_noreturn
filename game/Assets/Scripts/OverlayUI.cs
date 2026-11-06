using UnityEngine;

/// <summary>
/// Плёночные эффекты поверх мира: виньетка тревоги, вспышки провалов,
/// субтитры диктофона, строка подсказок, экраны смерти и конца смены.
/// Это не игровой UI — взаимодействие целиком на физических экранах.
/// </summary>
public class OverlayUI : MonoBehaviour
{
    NightDirector dir;
    Font font;        // шрифт мониторов (OS-шрифт) — в IMGUI непригоден
    Font imguiFont;   // встроенный шрифт для оверлеев IMGUI

    Texture2D vignette;
    Texture2D white;

    GUIStyle center, title, endStyle, callName, callLine, callInput, hintStyle, subtitleStyle;
    GUIStyle interactionLabel;
    float styledScale = -1f;
    float styledTypeInScale = -1f;
    int styledHeight = -1;

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
        if (imguiFont == null)
        {
            // Шрифт из CreateDynamicFontFromOSFont рисуется на мониторах-мешах,
            // но IMGUI не может загрузить его лицо — каждый GUI.Label даёт
            // «Unable to load font face» и текст невидим (пустой экран отчёта
            // выглядел как зависание). Оверлеям — встроенный шрифт Unity.
            try { imguiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { imguiFont = null; }
            if (imguiFont == null)
                try { imguiFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { imguiFont = null; }
            if (imguiFont == null) imguiFont = GUI.skin.label.font;
        }

        float s = dir != null ? dir.UiScale : 1f;
        float ti = dir != null ? dir.TypeInScale : 1f;
        int sh = Screen.height;
        if (center != null && Mathf.Abs(styledScale - s) < 0.0001f &&
            Mathf.Abs(styledTypeInScale - ti) < 0.0001f && styledHeight == sh) return;
        styledScale = s;
        styledTypeInScale = ti;
        styledHeight = sh; // после смены размера Game view шрифты пересобираются

        center = new GUIStyle(GUI.skin.label);
        center.font = imguiFont;
        center.fontSize = Mathf.RoundToInt(Screen.height * 0.026f * s);
        center.alignment = TextAnchor.MiddleCenter;
        center.wordWrap = true;
        center.normal.textColor = Color.white;

        title = new GUIStyle(center);
        title.fontSize = Mathf.Max(28, Mathf.RoundToInt(Screen.height * 0.052f));
        title.normal.textColor = new Color(0.90f, 0.96f, 0.92f);

        endStyle = new GUIStyle(center);
        endStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.026f * s));
        endStyle.wordWrap = true;
        endStyle.normal.textColor = new Color(0.88f, 0.93f, 0.90f);

        callName = new GUIStyle(center);
        callName.fontSize = Mathf.RoundToInt(Screen.height * 0.019f * s);
        callName.alignment = TextAnchor.MiddleLeft;
        callName.normal.textColor = new Color(1f, 0.76f, 0.29f);

        callLine = new GUIStyle(center);
        callLine.fontSize = Mathf.RoundToInt(Screen.height * 0.021f * s);
        callLine.alignment = TextAnchor.MiddleLeft;
        callLine.normal.textColor = new Color(0.90f, 0.93f, 0.90f);

        callInput = new GUIStyle(center);
        callInput.fontSize = Mathf.RoundToInt(Screen.height * 0.021f * s * ti);
        callInput.alignment = TextAnchor.MiddleLeft;
        callInput.normal.textColor = Color.white;

        hintStyle = new GUIStyle(center);
        // Мелкие подсказки внутри оверлеев (звонок); сами хинты станции
        // теперь живут на мониторах (TerminalApp / MessengerApp).
        hintStyle.fontSize = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.021f));
        hintStyle.normal.textColor = new Color(0.78f, 0.83f, 0.80f, 0.95f);

        subtitleStyle = new GUIStyle(center);
        subtitleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.022f);
        subtitleStyle.normal.textColor = new Color(0.92f, 0.95f, 0.92f);

        interactionLabel = new GUIStyle(center);
        // Подпись действия читается поверх мира с рабочего места — крупнее прочего текста.
        interactionLabel.fontSize = Mathf.RoundToInt(Screen.height * 0.030f);
        interactionLabel.alignment = TextAnchor.MiddleCenter;
        interactionLabel.wordWrap = false;
        interactionLabel.normal.textColor = new Color(0.92f, 0.95f, 0.92f, 0.95f);

        // Меню и настройки рисуются на экране терминала — стилей IMGUI для них
        // больше не нужно.
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
            if (dir.InteractionVisible) DrawInteraction(w, h);
        }

        if (dir.Modal == ModalKind.Intro)
            DrawIntro(w, h);
        else if (dir.Modal == ModalKind.Rest)
            DrawRest(w, h);
        else if (dir.Modal == ModalKind.Call)
            DrawCall(w, h);
        else if (dir.Modal == ModalKind.Credits)
            DrawCredits(w, h);
        else if (dir.Modal == ModalKind.Epilogue)
            DrawEpilogue(w, h);
        // Отчёт смены и проигрыши рисуются на экране терминала (TerminalApp);
        // главное меню и настройки — тоже там.

        if (dir.CheatsOpen) DrawDevPanel(w, h);

        if ((dir.Modal == ModalKind.None || dir.Modal == ModalKind.Dictaphone)
            && !string.IsNullOrEmpty(dir.Subtitle))
        {
            Rect band = new Rect(w * 0.5f - Mathf.Min(460f, w * 0.44f), h - h * 0.14f,
                                 Mathf.Min(920f, w * 0.88f), h * 0.085f);
            DrawBand(band, 0.62f);
            GUI.Label(band, dir.Subtitle, subtitleStyle);
        }
        // Подсказки станции — на мониторах (TerminalApp / MessengerApp).
    }

    void DrawCrosshair(float w, float h)
    {
        float s = Mathf.Max(3f, Mathf.Round(h * 0.0035f * dir.UiScale));
        float x = w * 0.5f - s * 0.5f;
        float y = h * 0.5f - s * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, s + 2f, s + 2f), white);
        GUI.color = new Color(0.92f, 0.95f, 0.92f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, s, s), white);
        GUI.color = Color.white;
    }

    void DrawInteraction(float w, float h)
    {
        string label = dir.InteractionLabel;
        if (string.IsNullOrEmpty(label)) return;

        GUIStyle style = interactionLabel;
        float textW = style.CalcSize(new GUIContent(label)).x + 8f;        float textH = style.lineHeight > 1f ? style.lineHeight : h * 0.03f;
        float x = w * 0.5f - textW * 0.5f;
        float y = h * 0.5f + h * 0.035f;

        DrawBand(new Rect(x - 16f, y - 6f, textW + 32f, textH + 12f), 0.55f);
        GUI.Label(new Rect(x, y, textW, textH), label, style);
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
            GUI.Label(new Rect(0f, h * 0.52f, w, h * 0.06f), dir.IntroTitle, center);
            GUI.color = Color.white;
        }
    }

    void DrawRest(float w, float h)
    {
        float s = dir.UiScale;
        float a = 0.75f * dir.RestProgress;
        GUI.color = new Color(0f, 0f, 0f, a);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
        GUI.color = Color.white;

        GUI.Label(new Rect(0f, h * (0.5f - 0.06f * s), w, h * 0.05f * s), "ОТДЫХ", title);
        GUI.Label(new Rect(0f, h * (0.5f + 0.02f * s), w, h * 0.05f * s),
            Mathf.RoundToInt(dir.RestProgress * 100f) + "%", center);
    }

    void DrawCall(float w, float h)
    {
        float s = dir.UiScale;

        float bw = Mathf.Min(900f * s, w * 0.92f);
        float bh = Mathf.Min(h * 0.26f * s, h * 0.60f);
        var band = new Rect(w * 0.5f - bw * 0.5f, h - h * 0.08f - bh, bw, bh);
        DrawBand(band, 0.82f);

        var nameR = new Rect(band.x + 24f * s, band.y + 14f * s, band.width - 48f * s, 30f * s);
        GUI.Label(nameR, "ТЕЛЕФОН · ВИКТОР", callName);

        var lineR = new Rect(band.x + 24f * s, band.y + 48f * s, band.width - 48f * s, 54f * s);
        GUI.Label(lineR, dir.CallLine, callLine);

        if (dir.CallAwaitingAnswer)
        {
            string shown = "ОТВЕТ: " + dir.CallAnswerView;
            float inputH = Mathf.Max(34f * s, callInput.lineHeight);
            var inR = new Rect(band.x + 24f * s, band.y + 104f * s, band.width - 48f * s, inputH);
            GUI.Label(inR, shown, dir.CallTypo
                ? Tinted(callInput, new Color(1f, 0.36f, 0.36f))
                : callInput);

            GUI.Label(new Rect(band.x + 24f * s, band.y + band.height - 38f * s,
                               band.width - 48f * s, 28f * s),
                      "ENTER — сказать", hintStyle);
        }
        else
        {
            GUI.Label(new Rect(band.x + 24f * s, band.y + 110f * s, band.width - 48f * s, 34f * s),
                      "…", callLine);
        }
    }

    GUIStyle Tinted(GUIStyle src, Color c)
    {
        var s = new GUIStyle(src);
        s.normal.textColor = c;
        return s;
    }

    // Настройки, главное меню и отчёты рисуются на экране терминала (TerminalApp).

    void DrawBand(Rect r, float alpha)
    {
        DrawSolid(r, new Color(0f, 0f, 0f, alpha));
    }

    /// <summary>Happy end: чёрный экран, титры едут снизу вверх сами.</summary>
    void DrawCredits(float w, float h)
    {
        DrawSolid(new Rect(0f, 0f, w, h), Color.black);

        var style = new GUIStyle(endStyle);
        style.alignment = TextAnchor.MiddleCenter;
        float lineH = style.fontSize * 1.45f;
        if (lineH <= 0f) return;

        // Скорость — одна строка в секунду; потом титры просто стоят на месте.
        float t = Time.time - dir.CreditsStart;
        float bottom = h * 0.92f;
        float travel = Mathf.Min(t * lineH,
            Mathf.Max(0f, bottom - h * 0.15f +
                          (dir.CreditsLines.Length - 1) * lineH));

        for (int i = 0; i < dir.CreditsLines.Length; i++)
        {
            float y = bottom - travel + i * lineH;
            if (y < -lineH || y > h) continue;
            GUI.Label(new Rect(0f, y, w, lineH), dir.CreditsLines[i], style);
        }
    }

    /// <summary>Bad end: чёрный экран, строки появляются по одной (~1.9 с на строку).</summary>
    void DrawEpilogue(float w, float h)
    {
        DrawSolid(new Rect(0f, 0f, w, h), Color.black);

        float t = Time.time - dir.CreditsStart;
        int shown = Mathf.Clamp(Mathf.FloorToInt(t / 1.9f) + 1, 0, dir.EpilogueLines.Length);

        var body = new GUIStyle(endStyle);
        body.wordWrap = true;

        for (int i = 0; i < shown; i++)
        {
            float a = Mathf.Clamp01((t - (i * 1.9f)) / 0.55f);
            var line = new GUIStyle(body);
            line.normal.textColor = new Color(body.normal.textColor.r,
                body.normal.textColor.g, body.normal.textColor.b, a);
            GUI.Label(new Rect(w * 0.10f, h * 0.22f + i * body.fontSize * 2.1f,
                               w * 0.80f, h * 0.05f), dir.EpilogueLines[i], line);
        }

        GUI.Label(new Rect(0f, h - h * 0.06f, w, h * 0.045f),
                  "R — переиграть ночь 3   ·   E — с самого начала", hintStyle);
    }

    /// <summary>Дев-панель (F9): список читов для быстрого прохода контента.</summary>
    void DrawDevPanel(float w, float h)
    {
        float s = dir.UiScale;
        float pad = 22f * s;
        float lineH = hintStyle.fontSize * 1.75f;

        float pw = Mathf.Min(640f * s, w * 0.9f);
        float ph = pad * 1.6f + (dir.CheatLines.Length + 1) * lineH;
        var panel = new Rect(w * 0.5f - pw * 0.5f, h * 0.5f - ph * 0.5f, pw, ph);

        DrawSolid(new Rect(panel.x - 6f, panel.y - 6f, panel.width + 12f, panel.height + 12f),
                  new Color(0f, 0f, 0f, 0.90f));
        DrawSolid(panel, new Color(0.02f, 0.035f, 0.028f, 0.97f));
        DrawBorder(panel, Mathf.Max(1f, s), new Color(1f, 0.76f, 0.29f, 0.55f));

        GUI.Label(new Rect(panel.x + pad, panel.y + pad * 0.6f, panel.width - pad * 2, lineH),
                  "ДЕВ-МЕНЮ · F9", hintStyle);
        for (int i = 0; i < dir.CheatLines.Length; i++)
            GUI.Label(new Rect(panel.x + pad, panel.y + pad * 0.6f + (i + 1) * lineH,
                               panel.width - pad * 2, lineH),
                      dir.CheatLines[i], hintStyle);
    }

    void DrawSolid(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = Color.white;
    }

    void DrawBorder(Rect r, float t, Color c)
    {
        DrawSolid(new Rect(r.x, r.y, r.width, t), c);
        DrawSolid(new Rect(r.x, r.yMax - t, r.width, t), c);
        DrawSolid(new Rect(r.x, r.y, t, r.height), c);
        DrawSolid(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
