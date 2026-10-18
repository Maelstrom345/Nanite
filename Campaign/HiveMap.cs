using System.Collections.Generic;

namespace Nanite;

internal sealed class HiveMap
{
    public string Id;
    public string Name;
    public string PicturePath; // The name of the texture to reveal
    
    public int GridColumns = 5;
    public int GridRows = 4;
    
    public List<HiveData> Hives = new();

    public bool IsFullyCleared()
    {
        foreach (var hive in Hives)
            if (!hive.Cleared) return false;
        return true;
    }
}