using UnityEngine;

/// <summary>
/// Ночные часы: 00:00 → 07:00. По концепту один игровой час ≈ 1,5 реальной минуты,
/// то есть вся ночь ≈ 10,5 минут реального времени.
/// </summary>
public class GameClock
{
    public const float SecondsPerGameHour = 90f;
    public const float NightMinutes = 7f * 60f;

    float minutes;          // игровые минуты от 00:00
    public bool running;

    public float Minutes => minutes;
    public int Hour => Mathf.FloorToInt(minutes / 60f);
    public int Minute => Mathf.FloorToInt(minutes) % 60;
    public bool Finished => minutes >= NightMinutes;

    public string Display => Hour.ToString("00") + ":" + Minute.ToString("00");

    public void Reset()
    {
        minutes = 0f;
        running = true;
    }

    /// <summary>Реальное время → игровое. 60 игровых минут за 90 реальных секунд.</summary>
    public void Tick(float deltaTime)
    {
        if (!running) return;
        Advance(deltaTime * (60f / SecondsPerGameHour));
    }

    /// <summary>Прыжок времени (отдых, провал памяти).</summary>
    public void Advance(float gameMinutes)
    {
        minutes = Mathf.Clamp(minutes + gameMinutes, 0f, NightMinutes);
    }

    public int WholeMinutes => Mathf.FloorToInt(minutes);
}
