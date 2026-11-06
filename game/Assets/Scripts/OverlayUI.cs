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
    GUIStyle interactionLabel, inputPreview, settingsTitle, settingsOption, settingsValue;
    GUIStyle menuOption, menuValue;
    GUIStyle hudText, hudWarn, hudBad, hudName, hudVal, hudWarnVal, hudBadVal;
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
        float s = dir != null ? dir.UiScale : 1f;
        float ti = dir != null ? dir.TypeInScale : 1f;
        int sh = Screen.height;
        if (center != null && Mathf.Abs(styledScale - s) < 0.0001f &&
            Mathf.Abs(styledTypeInScale - ti) < 0.0001f && styledHeight == sh) return;
        styledScale = s;
        styledTypeInScale = ti;
        styledHeight = sh; // после смены размера Game view шрифты пересобираются

        center = new GUIStyle(GUI.skin.label);
        center.font = font;
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
        // Хинты обязаны читаться на любом размере вью: минимум 15px и
        // заметно светлее фона, плюс тёмная полоса-подложка при отрисовке.
        // HUD-текст НЕ масштабируется от UiScale: настройка «РАЗМЕР ТЕКСТА»
        // затрагивает только мониторы; оверлеи всегда в базовом размере.
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

        // Живой пример в настройках показывает размер печатаемого текста
        // (сейчас это ответ на звонок) — тот же стиль, что и строка ответа.
        inputPreview = new GUIStyle(callInput);

        settingsTitle = new GUIStyle(center);
        settingsTitle.fontSize = Mathf.RoundToInt(Screen.height * 0.034f * s);
        settingsTitle.wordWrap = false;
        settingsTitle.normal.textColor = new Color(0.88f, 0.95f, 0.90f);

        settingsOption = new GUIStyle(center);
        settingsOption.fontSize = Mathf.RoundToInt(Screen.height * 0.027f * s);
        settingsOption.wordWrap = false;
        settingsOption.normal.textColor = new Color(0.62f, 0.68f, 0.64f);

        settingsValue = new GUIStyle(settingsOption);
        settingsValue.normal.textColor = new Color(1f, 0.76f, 0.29f);

        hudText = new GUIStyle(center);
        hudText.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.019f));

        // Пункты главного меню — тоже вне UiScale (см. DrawStatusHud).
        menuOption = new GUIStyle(center);
        menuOption.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.028f));
        menuOption.wordWrap = false;
        menuOption.normal.textColor = new Color(0.62f, 0.68f, 0.64f);

        menuValue = new GUIStyle(menuOption);
        menuValue.normal.textColor = new Color(1f, 0.76f, 0.29f);
        hudText.alignment = TextAnchor.MiddleLeft;
        hudText.wordWrap = false;
        hudText.normal.textColor = new Color(0.72f, 0.79f, 0.75f, 0.90f);

        hudWarn = new GUIStyle(hudText);
        hudWarn.normal.textColor = new Color(1f, 0.76f, 0.29f, 0.95f);

        hudBad = new GUIStyle(hudText);
        hudBad.normal.textColor = new Color(1f, 0.36f, 0.36f, 0.95f);

        // Левые колонки — тусклые названия показателей, правые — значения.
        hudName = new GUIStyle(hudText);
        hudName.normal.textColor = new Color(0.50f, 0.55f, 0.52f, 0.85f);

        hudVal = new GUIStyle(hudName);
        hudVal.alignment = TextAnchor.MiddleRight;
        hudVal.normal.textColor = new Color(0.90f, 0.95f, 0.92f, 0.95f);

        hudWarnVal = new GUIStyle(hudVal);
        hudWarnVal.normal.textColor = new Color(1f, 0.76f, 0.29f, 0.95f);

        hudBadVal = new GUIStyle(hudVal);
        hudBadVal.normal.textColor = new Color(1f, 0.36f, 0.36f, 0.95f);
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
            DrawStatusHud(w, h);
            DrawCrosshair(w, h);
            if (dir.InteractionVisible) DrawInteraction(w, h);
        }

        if (dir.Modal == ModalKind.Intro)
            DrawIntro(w, h);
        else if (dir.Modal == ModalKind.MainMenu)
            DrawMainMenu(w, h);
        else if (dir.Modal == ModalKind.Rest)
            DrawRest(w, h);
        else if (dir.Modal == ModalKind.Call)
            DrawCall(w, h);
        else if (dir.Modal == ModalKind.Death || dir.Modal == ModalKind.End)
            DrawEnd(w, h);
        else if (dir.Modal == ModalKind.Settings)
            DrawSettings(w, h);
        else if (dir.Modal == ModalKind.Credits)
            DrawCredits(w, h);
        else if (dir.Modal == ModalKind.Epilogue)
            DrawEpilogue(w, h);

        if (dir.CheatsOpen) DrawDevPanel(w, h);

        if ((dir.Modal == ModalKind.None || dir.Modal == ModalKind.Dictaphone)
            && !string.IsNullOrEmpty(dir.Subtitle))
        {
            Rect band = new Rect(w * 0.5f - Mathf.Min(460f, w * 0.44f), h - h * 0.14f,
                                 Mathf.Min(920f, w * 0.88f), h * 0.085f);
            DrawBand(band, 0.62f);
            GUI.Label(band, dir.Subtitle, subtitleStyle);
        }

        if (dir.Modal == ModalKind.None && dir.Running &&
            !string.IsNullOrEmpty(dir.Hint))
        {
            var r = new Rect(0f, h - h * 0.042f, w, h * 0.038f);
            DrawBand(r, 0.60f);
            GUI.Label(r, dir.Hint, hintStyle);
        }
    }

    /// <summary>
    /// HUD состояния: таблица «название слева — значение справа».
    /// Тревога и след показываются с полосой — видно, где граница.
    /// </summary>
    void DrawStatusHud(float w, float h)
    {
        // HUD не реагирует на UiScale — он всегда в базовом размере «1x»,
        // иначе настройка «РАЗМЕР ТЕКСТА» уводила панель из кадра.
        const float s = 1f;
        float lineH = hudText.lineHeight + 3f * s;
        float pad = 10f * s;
        float panelW = Mathf.Min(340f * s, w * 0.46f);
        bool downloading = dir.TerminalDownloading;
        float panelH = pad * 2f + lineH * (downloading ? 7f : 6f) + 18f * s;

        var panel = new Rect(16f * s - pad, 16f * s - pad, panelW, panelH);
        DrawSolid(panel, new Color(0.02f, 0.03f, 0.025f, 0.85f));
        DrawBorder(panel, Mathf.Max(2f, s), new Color(0.85f, 0.95f, 0.90f, 0.35f));

        float x = panel.x + pad;
        float y = panel.y + pad;
        float textW = panel.width - pad * 2f;
        float nameW = textW * 0.46f;
        float valX = x + nameW;
        float valW = textW - nameW;

        // часы — на всю ширину, крупнее прочих строк
        GUI.Label(new Rect(x, y, textW, lineH),
                  "НОЧЬ " + dir.State.night + " · " + dir.Clock.Display, hudText);
        y += lineH + 4f * s;

        // Тревога: ползёт к 100 — сердце Брика не выдержит.
        float anxiety = Mathf.Clamp(dir.State.anxiety, 0f, 100f);
        GUI.Label(new Rect(x, y, nameW, lineH), "ТРЕВОГА", hudName);
        GUI.Label(new Rect(valX, y, valW, lineH),
                  Mathf.RoundToInt(anxiety) + "%" + (anxiety >= 75f ? "  100 — КОНЕЦ" : ""),
                  anxiety >= 75f ? hudBadVal : anxiety >= 55f ? hudWarnVal : hudVal);
        y += lineH;
        DrawBar(new Rect(x, y - 2f * s, textW, 3f * s), anxiety / 100f,
                anxiety >= 75f ? new Color(1f, 0.36f, 0.36f, 0.90f)
                : anxiety >= 55f ? new Color(1f, 0.76f, 0.29f, 0.90f)
                : new Color(0.55f, 0.85f, 0.65f, 0.75f));
        y += 8f * s;

        // Бессонница: чем выше — тем чаще опечатки в командах.
        GUI.Label(new Rect(x, y, nameW, lineH), "БЕССОННИЦА", hudName);
        GUI.Label(new Rect(valX, y, valW, lineH),
                  dir.State.insomnia + "/5",
                  dir.State.insomnia >= 4 ? hudWarnVal : hudVal);
        y += lineH;

        // След: кибербезы следят за логами; при 100% — ордер на арест.
        bool dirty = dir.State.dirtyLogs;
        GUIStyle suspicionStyle = dir.State.suspicion >= 75f ? hudBadVal
                                : dirty ? hudWarnVal : hudVal;
        string suspicionNote = dir.State.suspicion >= 75f ? "  100 — ОРДЕР"
                             : dirty ? "   ГРЯЗНО" : "";
        GUI.Label(new Rect(x, y, nameW, lineH), "СЛЕД В ЛОГАХ", hudName);
        GUI.Label(new Rect(valX, y, valW, lineH),
                  dir.State.suspicion + "%" + suspicionNote, suspicionStyle);
        y += lineH;
        DrawBar(new Rect(x, y - 2f * s, textW, 3f * s), dir.State.suspicion / 100f,
                dirty ? new Color(1f, 0.76f, 0.29f, 0.90f)
                      : new Color(0.55f, 0.85f, 0.65f, 0.55f));
        y += 8f * s;

        // Таблетки: гасят тревогу (клавиша 1 за столом или E на блистере).
        GUI.Label(new Rect(x, y, nameW, lineH), "ТАБЛЕТКИ", hudName);
        GUI.Label(new Rect(valX, y, valW, lineH),
                  dir.State.pills + (dir.State.anxiety >= 55f && dir.State.pills > 0
                      ? "  ·  1 — принять" : ""),
                  dir.State.anxiety >= 55f && dir.State.pills > 0 ? hudWarnVal : hudVal);
        y += lineH;

        // Кибербезы: время до плановой проверки логов.
        GUI.Label(new Rect(x, y, nameW, lineH), "КИБЕРБЕЗЫ", hudName);
        GUI.Label(new Rect(valX, y, valW, lineH),
                  dir.CyberCdText + (dir.CyberDanger ? "  СЕЙЧАС" : ""),
                  dir.CyberDanger ? hudBadVal : hudVal);

        if (downloading)
        {
            y += lineH;
            GUI.Label(new Rect(x, y, textW, lineH),
                      "ЗАГРУЗКА  " + dir.TerminalDownloadText, hudWarn);
        }
    }

    void DrawBar(Rect r, float fill, Color color)
    {
        DrawSolid(r, new Color(1f, 1f, 1f, 0.10f));
        float clamped = Mathf.Clamp01(fill);
        if (clamped > 0f)
            DrawSolid(new Rect(r.x, r.y, r.width * clamped, r.height), color);
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

    /// <summary>
    /// Главное меню: тёмный экран в духе игры, крупная надпись и вертикальный
    /// список пунктов — клавиатура (↑↓, E/ENTER) и мышь (навёл — подсветилось,
    /// кликнул — сработало). Появляется при запуске игры.
    /// </summary>
    void DrawMainMenu(float w, float h)
    {
        // Меню — оверлей: фиксированный базовый размер, вне UiScale.
        const float s = 1f;

        DrawSolid(new Rect(0, 0, w, h), new Color(0.012f, 0.02f, 0.016f, 1f));

        GUI.Label(new Rect(0f, h * 0.24f, w, h * 0.11f), "VOID: NO RETURN", title);
        GUI.Label(new Rect(0f, h * 0.38f, w, hintStyle.lineHeight),
                  "НОЧНАЯ СМЕНА · ГАНДЕКС · ДИУ-2 · СМЕНЫ БЕЗ СЕЙВОВ", hintStyle);

        string[] items = dir.MainMenuItems;
        float itemW = Mathf.Min(380f * s, w * 0.8f);
        float itemH = menuOption.lineHeight + 16f * s;
        float y0 = h * 0.47f;

        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rect((w - itemW) * 0.5f, y0 + i * (itemH + 12f * s), itemW, itemH);
            bool selected = i == dir.MenuIndex;
            bool hover = r.Contains(Event.current.mousePosition);
            bool lit = selected || hover;
            if (hover && !selected) dir.MenuHover(i);

            if (lit) DrawSolid(r, new Color(1f, 0.76f, 0.29f, selected ? 0.12f : 0.05f));
            DrawBorder(r, Mathf.Max(1f, s),
                       lit ? new Color(1f, 0.76f, 0.29f, 0.8f)
                           : new Color(1f, 1f, 1f, 0.12f));
            GUI.Label(r, items[i], lit ? Centered(menuValue) : Centered(menuOption));

            // Курсор в меню свободен — пункт можно выбрать и кликом.
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) dir.MenuActivate(i);
        }

        GUI.Label(new Rect(0f, h * 0.9f, w, hintStyle.lineHeight),
                  "↑↓ — выбрать   E или ENTER — подтвердить", hintStyle);
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

    void DrawSettings(float w, float h)
    {
        float s = dir.UiScale;
        float pad = 28f * s;
        float rowH = settingsOption.lineHeight + 16f * s;
        float sectionGap = 18f * s;
        bool fromMenu = dir.SettingsFromMenu;

        float titleH = settingsTitle.lineHeight;
        float previewH = inputPreview.lineHeight;
        float hintH = hintStyle.lineHeight;
        float buttonsH = fromMenu ? rowH : 0f;

        float pw = Mathf.Clamp(620f * s, 380f * s, w * 0.9f);
        float ph = Mathf.Min(pad * 2f + titleH + sectionGap + rowH + sectionGap
                           + rowH + sectionGap + previewH + sectionGap + buttonsH
                           + hintH, h * 0.85f);
        var panel = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);

        DrawSolid(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.9f));
        DrawSolid(panel, new Color(0.045f, 0.06f, 0.052f, 0.96f));
        DrawBorder(panel, Mathf.Max(1f, 1.5f * s), new Color(0.28f, 0.38f, 0.32f, 0.85f));

        float x = panel.x + pad;
        float iw = pw - pad * 2f;
        float y = panel.y + pad;

        GUI.Label(new Rect(x, y, iw, titleH), "НАСТРОЙКИ", settingsTitle);
        y += titleH + sectionGap;

        y = DrawScaleRow(x, y, iw, rowH, s, "РАЗМЕР ТЕКСТА ЭКРАНОВ",
                         NightDirector.UiScaleSteps, dir.UiScaleIndex, 0) + sectionGap;
        y = DrawScaleRow(x, y, iw, rowH, s, "РАЗМЕР ТЕКСТА ВВОДА",
                         NightDirector.TypeInScaleSteps, dir.TypeInScaleIndex, 1) + sectionGap;

        // Живой пример: видно, как меняется именно печатаемый текст
        // (сейчас из печатаемого — только ответ на звонок).
        GUI.Label(new Rect(x, y, iw, previewH), "> Это Брик. Смена идет._", inputPreview);
        y += previewH + sectionGap;

        if (fromMenu)
        {
            var btn = new Rect(x, y, iw, rowH);
            bool hover = btn.Contains(Event.current.mousePosition);
            if (!hover) DrawSolid(btn, new Color(1f, 1f, 1f, 0.03f));
            DrawBorder(btn, Mathf.Max(1f, s), new Color(1f, 1f, 1f, hover ? 0.35f : 0.14f));
            GUI.Label(btn, "В ГЛАВНОЕ МЕНЮ", Centered(settingsOption));
            if (GUI.Button(btn, GUIContent.none, GUIStyle.none)) dir.SettingsToMenu();
            y += rowH + sectionGap;
        }

        GUI.Label(new Rect(x, y, iw, hintH),
                  fromMenu ? "↑↓ — раздел   ← → — значение   ESC — назад в меню"
                           : "↑↓ — раздел   ← → — значение   ESC — вернуться в смену", hintStyle);
    }

    GUIStyle Centered(GUIStyle src)
    {
        var c = new GUIStyle(src);
        c.alignment = TextAnchor.MiddleCenter;
        return c;
    }

    /// <summary>Одна строка настроек: название слева, ‹ значение › справа. Возвращает Y после неё.</summary>
    float DrawScaleRow(float x, float y, float iw, float rowH, float s,
                       string name, float[] steps, int selected, int row)
    {
        bool rowSelected = dir.SettingsRow == row;
        var rowRect = new Rect(x - 10f * s, y - 5f * s, iw + 20f * s, rowH + 10f * s);
        if (rowSelected)
        {
            DrawSolid(rowRect, new Color(1f, 0.76f, 0.29f, 0.08f));
            DrawBorder(rowRect, Mathf.Max(1f, s), new Color(1f, 0.76f, 0.29f, 0.55f));
        }

        GUI.Label(new Rect(x, y, iw * 0.55f, rowH), name,
                  rowSelected ? settingsValue : settingsOption);

        float arrowW = rowH;   // квадрат под стрелку
        float valueW = 96f * s;
        float gap = 6f * s;
        float bx = x + iw - arrowW * 2f - valueW - gap * 2f;

        GUIStyle val = rowSelected ? settingsValue : settingsOption;
        var left = new Rect(bx, y, arrowW, rowH);
        var value = new Rect(bx + arrowW + gap, y, valueW, rowH);
        var right = new Rect(bx + arrowW + valueW + gap * 2f, y, arrowW, rowH);

        GUI.Label(left, "‹", val);
        GUI.Label(value, Mathf.RoundToInt(steps[selected] * 100f) + "%", Centered(val));
        GUI.Label(right, "›", val);

        // Курсор в меню свободен: стрелки кликабельны.
        if (GUI.Button(left, GUIContent.none, GUIStyle.none))
        {
            dir.SelectSettingsRow(row);
            dir.AdjustSetting(-1);
        }
        if (GUI.Button(right, GUIContent.none, GUIStyle.none))
        {
            dir.SelectSettingsRow(row);
            dir.AdjustSetting(1);
        }
        return y + rowH;
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

        // Если строк много или UI-масштаб крупный — уменьшаем кегль ровно
        // настолько, чтобы весь отчёт поместился, но не мельче читаемого.
        int lines = Mathf.Max(1, dir.EndLines.Length);
        int maxFont = Mathf.Max(12, Mathf.FloorToInt(h * 0.56f / (lines * 1.2f)));
        var bodyStyle = new GUIStyle(endStyle);
        bodyStyle.fontSize = Mathf.Min(endStyle.fontSize, maxFont);
        bodyStyle.wordWrap = true;

        GUI.Label(new Rect(w * 0.10f, h * 0.27f, w * 0.80f, h * 0.62f), body, bodyStyle);
    }

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
                  "ДЕВ-МЕНЮ · F9", settingsValue);
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
