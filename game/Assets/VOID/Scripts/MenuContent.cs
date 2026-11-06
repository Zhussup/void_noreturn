using UnityEngine;

/// <summary>
/// Ссылки меню в одном ассете: Assets/VOID/Resources/MenuContent.asset.
/// Музыка лежит вне Resources, а CRT_Post ищется через Shader.Find — без ссылки
/// отсюда ни то, ни другое в билд не попадёт.
/// </summary>
[CreateAssetMenu(menuName = "VOID/Menu Content", fileName = "MenuContent")]
public class MenuContent : ScriptableObject
{
    public const string ResourcePath = "MenuContent";

    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Tooltip("Шейдеры, которые код находит по имени: удерживают их в билде")]
    public Shader[] keepShaders;

    static MenuContent cached;

    public static MenuContent Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<MenuContent>(ResourcePath);
            return cached;
        }
    }
}
