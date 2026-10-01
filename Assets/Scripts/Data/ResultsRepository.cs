using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public struct SaveResult
{
    public string resultId;
    public int place;
    public bool isNewRecord;
}

public static class ResultsRepository
{
    public static string FilePath => Path.Combine(Application.persistentDataPath, "race_results.json");

    public static ResultsData Load()
    {
        if (!File.Exists(FilePath)) return new ResultsData();
        // IO errors propagate: an inaccessible file must never be treated as empty history.
        string json = File.ReadAllText(FilePath);
        ResultsData data;
        try
        {
            data = JsonUtility.FromJson<ResultsData>(json);
            if (data == null || data.results == null || !json.Contains("\"results\""))
                throw new ArgumentException("Missing results array.");
            foreach (RaceResultEntry result in data.results)
                if (result == null || string.IsNullOrEmpty(result.id) ||
                    result.timeSeconds < 0 || float.IsNaN(result.timeSeconds) ||
                    float.IsInfinity(result.timeSeconds) || result.driftScore < 0)
                    throw new ArgumentException("Invalid race result.");
        }
        catch (ArgumentException)
        {
            string backup = Path.Combine(Application.persistentDataPath,
                "race_results_corrupt_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff", CultureInfo.InvariantCulture) + ".json");
            File.Move(FilePath, backup);
            Debug.LogWarning("Corrupt race history preserved at " + backup);
            return new ResultsData();
        }
        if (data.schemaVersion != 1)
            throw new NotSupportedException("Unsupported race results schema: " + data.schemaVersion);
        data.results.Sort(Compare);
        return data;
    }

    // Call once from the finish flow; never from Update, pause or scene exit.
    public static SaveResult SaveNewEntry(RaceResultEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (entry.driftScore < 0 || entry.timeSeconds < 0 || float.IsNaN(entry.timeSeconds) || float.IsInfinity(entry.timeSeconds))
            throw new ArgumentException("Score and time must be finite and non-negative.", nameof(entry));
        ResultsData data = Load();
        int best = data.results.Count == 0 ? 0 : data.results[0].driftScore;
        if (string.IsNullOrWhiteSpace(entry.id)) entry.id = Guid.NewGuid().ToString("N");
        foreach (RaceResultEntry existing in data.results)
            if (existing.id == entry.id) throw new ArgumentException("Result ID already saved.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.createdAtUtc))
            entry.createdAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        data.results.Add(entry);
        data.results.Sort(Compare);
        Directory.CreateDirectory(Application.persistentDataPath);
        string temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
        if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
        else File.Move(temporaryPath, FilePath);
        return new SaveResult
        {
            resultId = entry.id,
            place = data.results.IndexOf(entry) + 1,
            isNewRecord = entry.driftScore > best
        };
    }

    private static int Compare(RaceResultEntry a, RaceResultEntry b)
    {
        int scoreOrder = b.driftScore.CompareTo(a.driftScore);
        if (scoreOrder != 0) return scoreOrder;
        int timeOrder = a.timeSeconds.CompareTo(b.timeSeconds);
        return timeOrder != 0 ? timeOrder : string.CompareOrdinal(a.id, b.id);
    }
}
