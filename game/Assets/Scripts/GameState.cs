using System;
using UnityEngine;

/// <summary>
/// Сквозное состояние игры. Переживает ночь и (в будущем) сохраняется между сменами.
/// Поля намеренно простые — сериализуются JsonUtility без адаптеров.
/// </summary>
[Serializable]
public class GameState
{
    [Header("Ночь")]
    public int night = 1;          // 1..5
    public bool isVoid;            // играем за VOID (ночи 4–5)

    [Header("Состояние Брика")]
    public int insomnia = 1;       // 1..5 — с каждой ночью опечаток больше
    public float anxiety = 12f;    // 0..100, 100 — смерть
    public int pills = 4;          // таблеток в блистере
    public int moodStart;          // ответ ГАРМОНИИ в начале ночи
    public bool moodAnswered;

    [Header("Прогресс саботажа")]
    public int planStep;           // 0 — разведка не сделана, 1 — ДЦ-7 найден
    public int suspicion;          // 0..100, кибербезы

    // Индексы совпадают с trust: 0 — Джоэл, 1 — Дэлайла, 2 — Куплинов.
    public static readonly string[] EmployeeCodes = { "a-5120", "a-4471", "a-6643" };
    public bool dirtyLogs;         // в логах остались следы Брика
    public int traces;             // следы Брика (ночь 4)
    public bool[] codes = new bool[3];
    public bool[] records = new bool[4]; // прослушанные записи диктофона

    [Header("Работа и люди")]
    public int tasksDone;
    public int[] trust = new int[3]; // Джоэл, Дэлайла, Куплинов

    public void ResetForNight(int index)
    {
        night = Mathf.Clamp(index, 1, 5);
        isVoid = night >= 4;
        insomnia = Mathf.Clamp(night, 1, 5);
        anxiety = 12f;
        if (pills < 2) pills = 2;
        moodStart = 0;
        moodAnswered = false;
        planStep = 0;
        suspicion = 0;
        dirtyLogs = false;
        tasksDone = 0;
        if (codes == null || codes.Length != 3) codes = new bool[3];
        if (records == null || records.Length != 4) records = new bool[4];
        if (trust == null || trust.Length != 3) trust = new int[] { 50, 50, 50 };
    }

    public static GameState NewGame()
    {
        var s = new GameState();
        s.trust = new int[] { 50, 50, 50 };
        s.codes = new bool[3];
        s.records = new bool[4];
        s.ResetForNight(1);
        return s;
    }
}
