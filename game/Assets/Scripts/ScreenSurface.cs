using System.Collections.Generic;
using UnityEngine;

public struct ScreenLine
{
    public string text;
    public Color color;
    /// <summary>Множитель кегля для этой строки (1 — как обычно).</summary>
    public float scale;

    public ScreenLine(string text, Color color) : this(text, color, 1f) { }

    public ScreenLine(string text, Color color, float scale)
    {
        this.text = text;
        this.color = color;
        this.scale = scale <= 0f ? 1f : scale;
    }
}

/// <summary>
/// Текст на физическом экране. Строки — TextMesh, но лежат не на кваде
/// монитора, а на отдельном «стенде» далеко под комнатой (вне дальности
/// камеры игрока, на своём слое). Ортокамера стенда рисует их в
/// RenderTexture, а квад Screen показывает её через CRT-шейдер. Поэтому текст
/// больше не просвечивает сквозь корпус и предметы: в комнате это обычная
/// текстура на кваде.
///
/// Публичный API прежний (Create, Set, Fit, SetScale, Cols, Rows), приложения
/// работают без изменений. Камера рисует только когда текст или цвет строки
/// изменились, и не чаще ScreenStyle.maxRenderRate.
/// </summary>
public class ScreenSurface : MonoBehaviour
{
    /// <summary>Слой текстовых стендов. Имени в TagManager не требует.</summary>
    public const int TextLayer = 30;
    const float StandDepth = -1000f;   // стенды ниже комнаты, дальше farClip камеры игрока
    const float StandSpacing = 5f;

    public Transform screen;
    public float characterSize = 0.00064f;
    public float lineHeight = 0.0115f;
    public float padX = 0.012f;
    public float padY = 0.010f;
    public Font font;

    // Базовые метрики: от них считается пользовательский размер текста.
    float baseCharacterSize = 0.00064f;
    float baseLineHeight = 0.0115f;
    float appliedScale = 1f;

    /// <summary>
    /// Кегль, с которым создаётся динамический шрифт. TextMesh масштабирует
    /// глиф как characterSize * fontSize / 10, поэтому ширина символа считается
    /// с тем же коэффициентом — иначе сетка колонок разъезжается.
    /// </summary>
    public const int FontSize = 128;

    public int Cols { get; private set; }
    public int Rows { get; private set; }

    /// <summary>Затемнение экрана 0…1 (например, от тревоги). Передаётся в CRT-шейдер.</summary>
    public float Darken { get => darken; set { darken = Mathf.Clamp01(value); ApplyEffects(); } }

    /// <summary>Сила глитча 0…1 (например, VOID). Передаётся в CRT-шейдер.</summary>
    public float Glitch { get => glitch; set { glitch = Mathf.Clamp01(value); ApplyEffects(); } }

    readonly List<TextMesh> pool = new List<TextMesh>();
    readonly List<string> lastText = new List<string>();
    readonly List<Color> lastColor = new List<Color>();
    readonly List<float> lastScale = new List<float>();

    float width, height;
    float advanceEm = 0.6f; // ширина символа в долях кегля; уточняется по шрифту

    Camera standCamera;
    RenderTexture target;
    Renderer screenRenderer;
    MaterialPropertyBlock block;
    float darken, glitch;
    bool dirty = true;
    float nextRender;

    static int standCount;
    static bool mainCameraMasked;
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int DarkenId = Shader.PropertyToID("_Darken");
    static readonly int GlitchId = Shader.PropertyToID("_Glitch");
    static readonly int SeedId = Shader.PropertyToID("_Seed");

    public static ScreenSurface Create(Transform screenQuad, Font font,
                                       float characterSize = 0.00064f,
                                       float lineHeight = 0.0115f)
    {
        ScreenStyle style = ScreenStyle.Current;

        var go = new GameObject("Surface_" + ScreenName(screenQuad));
        go.layer = TextLayer;
        go.transform.SetPositionAndRotation(
            new Vector3(standCount++ * StandSpacing, StandDepth, 0f), Quaternion.identity);

        var s = go.AddComponent<ScreenSurface>();
        s.screen = screenQuad;
        s.font = style.font != null ? style.font : font;
        s.baseCharacterSize = characterSize;
        s.baseLineHeight = lineHeight;
        s.characterSize = characterSize;
        s.lineHeight = lineHeight;
        s.SetupOutput(style);
        s.Init();
        return s;
    }

    /// <summary>Задать эффекты CRT разом: затемнение и глитч, оба 0…1.</summary>
    public void SetEffects(float darkenAmount, float glitchAmount)
    {
        darken = Mathf.Clamp01(darkenAmount);
        glitch = Mathf.Clamp01(glitchAmount);
        ApplyEffects();
    }

    /// <summary>
    /// Пересобрать сетку под выбранный размер текста. Колонок и строк станет
    /// меньше — приложения считают раскладку по Cols/Rows и подстраиваются.
    /// </summary>
    public void SetScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.6f);
        if (Mathf.Abs(scale - appliedScale) < 0.001f) return;

        appliedScale = scale;
        characterSize = baseCharacterSize * scale;
        lineHeight = baseLineHeight * scale;
        Rebuild();
    }

    void Rebuild()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null) continue;
            pool[i].gameObject.SetActive(false);
            Destroy(pool[i].gameObject);
        }
        pool.Clear();
        lastText.Clear();
        lastColor.Clear();
        Init();
    }

    // ------------------------------------------------------------ вывод

    static string ScreenName(Transform screenQuad)
    {
        return screenQuad.parent != null ? screenQuad.parent.name : screenQuad.name;
    }

    /// <summary>Камера стенда, RenderTexture и CRT-материал на кваде экрана.</summary>
    void SetupOutput(ScreenStyle style)
    {
        width = Mathf.Abs(screen.lossyScale.x);
        height = Mathf.Abs(screen.lossyScale.y);

        bool messenger = ScreenName(screen).Contains("Chat");
        Color background = messenger ? ScreenPalette.Messenger.Background : ScreenPalette.Terminal.Background;

        target = new RenderTexture(Mathf.Max(64, style.textureSize.x), Mathf.Max(64, style.textureSize.y), 16,
                                   RenderTextureFormat.ARGB32)
        {
            name = "RT_" + name,
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            wrapMode = TextureWrapMode.Clamp,
        };
        target.Create();

        var camGo = new GameObject("StandCamera");
        camGo.layer = TextLayer;
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -1f);
        standCamera = camGo.AddComponent<Camera>();
        standCamera.enabled = false; // рисуем вручную, только когда есть изменения
        standCamera.orthographic = true;
        standCamera.orthographicSize = height * 0.5f;
        standCamera.aspect = width / height;
        standCamera.nearClipPlane = 0.5f;
        standCamera.farClipPlane = 1.5f;
        standCamera.cullingMask = 1 << TextLayer;
        standCamera.clearFlags = CameraClearFlags.SolidColor;
        standCamera.backgroundColor = background;
        standCamera.allowHDR = false;
        standCamera.allowMSAA = false;
        standCamera.useOcclusionCulling = false;
        standCamera.targetTexture = target;

        // камера игрока не должна видеть стенды, даже если окажется рядом
        if (!mainCameraMasked && Camera.main != null)
        {
            Camera.main.cullingMask &= ~(1 << TextLayer);
            mainCameraMasked = true;
        }

        screenRenderer = screen.GetComponent<Renderer>();
        if (screenRenderer != null)
        {
            if (style.crtMaterial != null) screenRenderer.sharedMaterial = style.crtMaterial;
            else screenRenderer.sharedMaterial = FallbackMaterial();
            block = new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTexId, target);
            block.SetFloat(SeedId, standCount * 17.31f);
            screenRenderer.SetPropertyBlock(block);
        }
        ApplyEffects();

        if (font != null)
        {
            font.RequestCharactersInTexture("M", FontSize);
            if (font.GetCharacterInfo('M', out CharacterInfo ci, FontSize) && ci.advance > 0)
                advanceEm = ci.advance / (float)FontSize;
        }
        Font.textureRebuilt += OnFontRebuilt;
    }

    static Material fallback;

    /// <summary>Если CRT-материал не назначен — просто показать текстуру.</summary>
    static Material FallbackMaterial()
    {
        if (fallback == null) fallback = new Material(Shader.Find("Unlit/Texture")) { name = "ScreenFallback" };
        return fallback;
    }

    void ApplyEffects()
    {
        if (screenRenderer == null || block == null) return;
        screenRenderer.GetPropertyBlock(block);
        block.SetFloat(DarkenId, darken);
        block.SetFloat(GlitchId, glitch);
        screenRenderer.SetPropertyBlock(block);
    }

    void OnFontRebuilt(Font f)
    {
        if (f == font) dirty = true; // атлас перестроился — глифы надо перерисовать
    }

    void LateUpdate()
    {
        if (!dirty || standCamera == null) return;
        if (Time.unscaledTime < nextRender) return;

        standCamera.Render();
        dirty = false;
        float rate = Mathf.Max(1f, ScreenStyle.Current.maxRenderRate);
        nextRender = Time.unscaledTime + 1f / rate;
    }

    void OnDestroy()
    {
        Font.textureRebuilt -= OnFontRebuilt;
        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }

    // ------------------------------------------------------------ сетка

    void Init()
    {
        width = Mathf.Abs(screen.lossyScale.x);
        height = Mathf.Abs(screen.lossyScale.y);
        lastScale.Clear();

        float charWidth = characterSize * (FontSize / 10f) * advanceEm;
        Cols = Mathf.Max(8, Mathf.FloorToInt((width - 2f * padX) / charWidth));
        Rows = Mathf.Max(4, Mathf.FloorToInt((height - 2f * padY) / lineHeight));

        for (int i = 0; i < Rows; i++)
        {
            var go = new GameObject("L" + i);
            go.layer = TextLayer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                -width * 0.5f + padX,
                height * 0.5f - padY - i * lineHeight,
                0f);

            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = FontSize;
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
            lastScale.Add(1f);
        }
        dirty = true;
    }

    public string Fit(string text)
    {
        return Fit(text, Cols);
    }

    static string Fit(string text, int cols)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= cols) return text;
        return text.Substring(0, cols);
    }

    /// <summary>Обновить содержимое экрана. Дешёвый: трогает только изменившиеся строки.</summary>
    public void Set(List<ScreenLine> lines)
    {
        // Раскладка сверху вниз: строка с увеличенным кеглем занимает больше
        // места, поэтому следующие строки сдвигаются (и могут уйти за экран).
        float top = height * 0.5f - padY;

        for (int i = 0; i < Rows; i++)
        {
            TextMesh tm = pool[i];
            bool on = lines != null && i < lines.Count;
            if (tm.gameObject.activeSelf != on)
            {
                tm.gameObject.SetActive(on);
                dirty = true;
            }
            if (!on) continue;

            float lineScale = Mathf.Clamp(lines[i].scale, 0.6f, 2f);
            if (Mathf.Abs(lastScale[i] - lineScale) > 0.001f)
            {
                lastScale[i] = lineScale;
                tm.characterSize = characterSize * lineScale;
                dirty = true;
            }

            tm.transform.localPosition = new Vector3(
                -width * 0.5f + padX, top, 0f);

            int cols = Mathf.Max(1, Mathf.FloorToInt(
                (width - 2f * padX) /
                (characterSize * lineScale * (FontSize / 10f) * advanceEm)));

            string t = Fit(lines[i].text, cols);
            if (lastText[i] != t)
            {
                tm.text = t;
                lastText[i] = t;
                dirty = true;
            }
            if (lastColor[i] != lines[i].color)
            {
                tm.color = lines[i].color;
                lastColor[i] = lines[i].color;
                dirty = true;
            }

            top -= lineHeight * lineScale;
        }
    }
}
