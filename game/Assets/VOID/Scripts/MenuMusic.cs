using UnityEngine;

/// <summary>
/// Музыка меню. Живёт вне сцен и следит за NightDirector.Modal, поэтому меню
/// менять не надо. Играет, пока открыто главное меню, его настройки или титры.
/// НОВАЯ СМЕНА перезагружает сцену сразу, так что затухание идёт уже в интро.
/// night_theme всё это время стоит и включается, когда меню стихло.
/// </summary>
public class MenuMusic : MonoBehaviour
{
    const float FadeSeconds = 1f;
    const float MaxFadeStep = 0.05f; // кадр загрузки сцены не должен съесть затухание

    AudioSource source;
    float volume = 0.5f;
    NightDirector dir;
    AudioSource night; // NightMusic текущей сцены
    bool playing; // не isPlaying: стрим становится isPlaying не сразу

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("MenuMusic");
        DontDestroyOnLoad(go);
        go.AddComponent<MenuMusic>();
        go.AddComponent<CreditsRoll>();
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        MenuContent content = MenuContent.Current;
        if (content == null || content.music == null)
        {
            Debug.LogWarning("[VNR] Музыка меню не найдена: Assets/VOID/Resources/MenuContent.asset");
            return;
        }
        source.clip = content.music;
        volume = content.musicVolume;
    }

    bool MenuOpen =>
        CreditsRoll.IsOpen ||
        (dir != null && (dir.Modal == ModalKind.MainMenu ||
                         (dir.Modal == ModalKind.Settings && dir.SettingsFromMenu)));

    void Update()
    {
        if (dir == null) dir = FindAnyObjectByType<NightDirector>();

        bool open = MenuOpen && source.clip != null;
        if (open && !playing)
        {
            playing = true;
            source.Play();
            Debug.Log("[VNR] Музыка меню: старт · " + source.clip.name);
        }

        float step = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep) * volume / FadeSeconds;
        source.volume = Mathf.MoveTowards(source.volume, open ? volume : 0f, step);

        if (open || source.volume > 0f)
        {
            HoldNightTheme();
            return;
        }
        if (playing)
        {
            playing = false;
            source.Stop();
            Debug.Log("[VNR] Музыка меню: стихла");
        }
        ReleaseNightTheme();
    }

    void HoldNightTheme()
    {
        if (night == null)
        {
            GameObject go = GameObject.Find("NightMusic");
            if (go != null) night = go.GetComponent<AudioSource>();
        }
        if (night != null && night.isPlaying) night.Stop();
    }

    void ReleaseNightTheme()
    {
        if (night == null) return;
        // На финалах NightDirector сам оставляет тишину.
        if (dir != null && (dir.Modal == ModalKind.None || dir.Modal == ModalKind.Intro))
        {
            night.Play();
            Debug.Log("[VNR] night_theme: старт после меню");
        }
        night = null;
    }
}
