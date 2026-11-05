using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сквозное состояние игры. Карьерные поля переживают ночь; ночные сбрасываются
/// в ResetForNight. Поля намеренно простые — сериализуются JsonUtility без адаптеров.
/// Сборка = три ночи с финалом (ночи 4–5 из PDF в игру не идут).
/// </summary>
[Serializable]
public class GameState
{
    [Header("Ночь")]
    public int night = 1;          // 1..3
    public bool isVoid;            // зарезервировано под режим VOID (ночи 4–5 вырезаны)

    [Header("Состояние Брика")]
    public int insomnia = 1;       // 1..5 — с каждой ночью опечаток больше
    public float anxiety = 12f;    // 0..100, 100 — смерть
    public int pills = 4;          // таблеток в блистере
    public int moodStart;          // ответ ГАРМОНИИ в начале ночи
    public bool moodAnswered;

    [Header("Прогресс саботажа")]
    public int planStep;           // 0 — разведка не сделана, 1 — ДЦ-7 найден
    public int suspicion;          // 0..100, кибербезы
    public int cyberStrikes;       // сколько проверок уже застали грязные логи

    // Индексы совпадают с trust: 0 — Джоэл, 1 — Дэлайла, 2 — Куплинов.
    public static readonly string[] EmployeeCodes = { "a-5120", "a-4471", "a-6643" };
    public bool dirtyLogs;         // в логах остались следы Брика
    public int traces;             // следы Брика (ночь 4)
    public bool[] codes = new bool[3];
    public bool[] records = new bool[4]; // прослушанные записи диктофона

    [Header("Работа и люди")]
    public int tasksDone;
    public int[] trust = new int[3]; // Джоэл, Дэлайла, Куплинов

    [Header("Теневой рынок")]
    public int credits;            // выручка за проданные данные

    [Header("ТЕРРА · покупатели")]
    public List<string> filesTaken = new List<string>(); // скачанные целевые файлы
    public List<string> filesSold = new List<string>();  // проданные файлы (любой канал)
    public int terraTrust;         // доверие покупателей ТЕРРА, 0..100
    public int deals;              // закрытые сделки напрямую через ТЕРРА
    public bool missionReady;      // дедлайн ТЕРРА пройден (контроль 06:00 ночи 3)

    public void ResetForNight(int index)
    {
        night = Mathf.Clamp(index, 1, 3);
        isVoid = false; // ночи 4–5 вырезаны; поле оставлено под возможный режим VOID
        insomnia = Mathf.Clamp(night, 1, 5);
        anxiety = 12f;
        if (pills < 2) pills = 2;
        moodStart = 0;
        moodAnswered = false;
        planStep = 0;
        suspicion = 0;
        cyberStrikes = 0;
        dirtyLogs = false;
        tasksDone = 0;
        missionReady = false;
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
