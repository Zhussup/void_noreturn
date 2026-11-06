using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Титры на весь экран поверх кадра: текст из Resources/credits.txt едет снизу
/// вверх. Строка с отступом — приглушённая, первая — заголовок. ESC и ENTER
/// закрывают, в конце закрываются сами. Открытие — CreditsRoll.Open().
/// </summary>
public class CreditsRoll : MonoBehaviour
{
    const float RefHeight = 1080f; // размеры ниже — в пикселях кадра 1080p
    const float Speed = 70f;
    const float TitleLine = 110f, NameLine = 50f, RoleLine = 42f, BlankLine = 34f;
    const int TitleSize = 64, NameSize = 32, RoleSize = 26, HintSize = 22;
    const string Hint = "ESC / ENTER назад";

    struct Line
    {
        public string text;
        public int size;
        public float height;
        public Color color;
    }

    static CreditsRoll instance;

    /// <summary>Держится до конца кадра закрытия: то же нажатие не дойдёт до меню.</summary>
    public static bool IsOpen => instance != null && instance.open;

    public static void Open()
    {
        if (instance != null) instance.Begin();
    }

    readonly List<Line> lines = new List<Line>();
    readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
    bool open, closing;
    int openedFrame;
    float openedAt, totalHeight;
    int styledHeight;

    void Awake()
    {
        instance = this;
    }

    void Begin()
    {
        if (open) return;
        Parse();
        open = true;
        closing = false;
        openedFrame = Time.frameCount; // ENTER, открывший титры, их не закрывает
        openedAt = Time.unscaledTime;
        Debug.Log("[VNR] Титры: открыты · строк " + lines.Count);
    }

    void Parse()
    {
        lines.Clear();
        totalHeight = 0f;
        TextAsset asset = Resources.Load<TextAsset>("credits");
        string text = asset != null ? asset.text : "VOID: NO RETURN";
        bool first = true;
        foreach (string raw in text.Split('\n'))
        {
            string trimmed = raw.Trim();
            var line = new Line { text = trimmed };
            if (trimmed.Length == 0)
            {
                line.height = BlankLine;
            }
            else if (first)
            {
                line.size = TitleSize;
                line.height = TitleLine;
                line.color = ScreenPalette.Terminal.Void;
                first = false;
            }
            else if (raw[0] == ' ' || raw[0] == '\t')
            {
                line.size = RoleSize;
                line.height = RoleLine;
                line.color = ScreenPalette.Terminal.Dim;
            }
            else
            {
                line.size = NameSize;
                line.height = NameLine;
                line.color = ScreenPalette.Terminal.Text;
            }
            lines.Add(line);
            totalHeight += line.height;
        }
    }

    float Scroll => (Time.unscaledTime - openedAt) * Speed;

    void Update()
    {
        if (!open) return;
        bool pressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.KeypadEnter);
        if ((pressed && Time.frameCount > openedFrame) || Scroll > RefHeight + totalHeight)
            closing = true;
    }

    void LateUpdate()
    {
        if (!closing) return;
        closing = false;
        open = false;
        Debug.Log("[VNR] Титры: закрыты");
    }

    GUIStyle StyleOf(int refSize, float k)
    {
        if (styledHeight != Screen.height)
        {
            styles.Clear();
            styledHeight = Screen.height;
        }
        if (!styles.TryGetValue(refSize, out GUIStyle style))
        {
            style = new GUIStyle
            {
                font = ScreenStyle.Current.font,
                fontSize = Mathf.RoundToInt(refSize * k),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
            };
            styles[refSize] = style;
        }
        return style;
    }

    void Draw(Rect rect, string text, int size, Color color, float k)
    {
        GUIStyle style = StyleOf(size, k);
        // Проект в Linear, а IMGUI без обратного шага темнит палитру экранов.
        style.normal.textColor =
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;
        GUI.Label(rect, text, style);
    }

    void OnGUI()
    {
        if (!open || Event.current.type != EventType.Repaint) return;
        GUI.depth = -1000;

        float w = Screen.width, h = Screen.height, k = h / RefHeight;
        Color prev = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
        GUI.color = prev;

        float y = (RefHeight - Scroll) * k;
        foreach (Line line in lines)
        {
            float lineH = line.height * k;
            if (line.text.Length > 0 && y + lineH > 0f && y < h)
                Draw(new Rect(0f, y, w, lineH), line.text, line.size, line.color, k);
            y += lineH;
        }

        // Подсказка на чёрной плашке: текст титров под ней не просвечивает.
        float hintH = 56f * k;
        var hintRect = new Rect(0f, h - hintH, w, hintH);
        GUI.color = Color.black;
        GUI.DrawTexture(hintRect, Texture2D.whiteTexture);
        GUI.color = prev;
        Draw(hintRect, Hint, HintSize, ScreenPalette.Terminal.Dim, k);
    }
}
