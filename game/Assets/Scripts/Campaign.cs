using UnityEngine;

/// <summary>
/// Сквозной прогресс: переживает перезагрузку сцены, но не переигрывание ночи.
/// Хранит снимок состояния на момент закрытия последней ночи (CommitNight);
/// смерть посреди ночи снимок не трогает — BeginNight пересобирает ночь с её старта.
/// Прогресс живёт только в сессии (роглайт); при желании можно сбрасывать снимок
/// в PlayerPrefs через JsonUtility — точка для этого оставлена здесь.
/// </summary>
public static class Campaign
{
    public static int CurrentNight = 1;
    static GameState snapshot; // конец последней закрытой ночи

    static GameState Clone(GameState s) =>
        JsonUtility.FromJson<GameState>(JsonUtility.ToJson(s));

    /// <summary>Начать ночь: свежий сеанс — новая игра, после CommitNight — снимок.</summary>
    public static GameState BeginNight(int night)
    {
        if (snapshot == null) snapshot = GameState.NewGame();
        GameState s = Clone(snapshot);
        s.ResetForNight(night);
        return s;
    }

    /// <summary>Смена закрыта: снимок = конец ночи, следующая — ночь nextNight.</summary>
    public static void CommitNight(GameState finished, int nextNight)
    {
        snapshot = Clone(finished);
        CurrentNight = nextNight;
    }

    public static void Reset()
    {
        snapshot = null;
        CurrentNight = 1;
    }
}