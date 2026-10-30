using UnityEngine;

/// <summary>
/// Оформление экранов мониторов в одном ассете: шрифт, CRT-материал, размер
/// RenderTexture и палитры терминала и мессенджера. Лежит в
/// Assets/VOID/Resources/ScreenStyle.asset и грузится через Resources —
/// ScreenSurface создаётся из кода и иначе получить ссылки не может.
/// Цвета читаются через ScreenPalette; править их можно прямо в ассете.
/// </summary>
[CreateAssetMenu(menuName = "VOID/Screen Style", fileName = "ScreenStyle")]
public class ScreenStyle : ScriptableObject
{
    public const string ResourcePath = "ScreenStyle";

    [Header("Вывод")]
    [Tooltip("Моноширинный шрифт с кириллицей (JetBrains Mono)")]
    public Font font;
    [Tooltip("Материал квада Screen: CRT_Screen, в _MainTex подаётся RenderTexture")]
    public Material crtMaterial;
    [Tooltip("Размер RenderTexture экрана; соотношение — как у квада (0.32 × 0.24 → 4:3)")]
    public Vector2Int textureSize = new Vector2Int(1024, 768);
    [Tooltip("Не чаще стольких перерисовок в секунду (только когда текст изменился)")]
    public float maxRenderRate = 30f;

    [Header("Терминал (GANDEX-OS)")]
    public Color termBackground = Hex(0x050A08);
    public Color termText = Hex(0x7DFF9C);
    public Color termDim = Hex(0x3E7C50);
    public Color termLines = Hex(0x1E4A2E);
    public Color termWarn = Hex(0xFFC24A);
    public Color termAlarm = Hex(0xFF5C5C);
    public Color termVoid = Hex(0xF2FFF5);

    [Header("Мессенджер")]
    public Color msgBackground = Hex(0x0C0F0C);
    public Color msgText = Hex(0xB9D1C2);
    [Tooltip("В docs/03_screens_design.md значения нет — оставлено прежнее")]
    public Color msgDim = Hex(0x667A70);
    public Color msgAccent = Hex(0x8FA6FF);
    public Color msgOk = Hex(0x59FF9E);
    public Color msgWarn = Hex(0xFFC24A);
    public Color msgAlarm = Hex(0xFF5C5C);
    [Tooltip("В docs/03_screens_design.md значения нет — оставлено прежнее")]
    public Color msgBright = Hex(0xEBF7F0);

    static ScreenStyle cached;

    /// <summary>Ассет из Resources; если его нет — значения по умолчанию.</summary>
    public static ScreenStyle Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<ScreenStyle>(ResourcePath);
            if (cached == null) cached = CreateInstance<ScreenStyle>();
            return cached;
        }
    }

    static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}

/// <summary>
/// Палитра экранов — единственное место, откуда приложения берут цвета.
/// Значения живут в ScreenStyle (docs/03_screens_design.md).
/// </summary>
public static class ScreenPalette
{
    static ScreenStyle S => ScreenStyle.Current;

    public static class Terminal
    {
        public static Color Background => S.termBackground;
        public static Color Text => S.termText;
        public static Color Dim => S.termDim;
        public static Color Lines => S.termLines;
        public static Color Warn => S.termWarn;
        public static Color Alarm => S.termAlarm;
        public static Color Void => S.termVoid;
    }

    public static class Messenger
    {
        public static Color Background => S.msgBackground;
        public static Color Text => S.msgText;
        public static Color Dim => S.msgDim;
        public static Color Accent => S.msgAccent;
        public static Color Ok => S.msgOk;
        public static Color Warn => S.msgWarn;
        public static Color Alarm => S.msgAlarm;
        public static Color Bright => S.msgBright;
    }
}
