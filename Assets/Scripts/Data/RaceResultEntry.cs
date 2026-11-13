using System;

[Serializable]
public class RaceResultEntry
{
    public string id;
    public string carId;
    public string carName;
    public float timeSeconds;
    public int driftScore;
    public int maxMultiplier;
    public string createdAtUtc;
}
