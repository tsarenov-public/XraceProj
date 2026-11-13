using System;
using System.Collections.Generic;

[Serializable]
public class ResultsData
{
    public int schemaVersion = 1;
    public List<RaceResultEntry> results = new List<RaceResultEntry>();
}
