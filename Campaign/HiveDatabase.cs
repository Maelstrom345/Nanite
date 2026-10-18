using System;
using System.Collections.Generic;

namespace Nanite;

internal static class HiveMapDatabase
{
    // Save the map in memory so it persists between screens
    private static HiveMap _cachedMap;

    public static HiveMap GetMapOne()
    {
        if (_cachedMap == null)
        {
            _cachedMap = BuildMapOne();
        }
        return _cachedMap;
    }

    private static HiveMap BuildMapOne()
    {
        // ... (Keep your existing BuildMapOne code here, but make it private static HiveMap)
    
        var map = new HiveMap
        {
            Id = "map_01",
            Name = "Cygnus",
            PicturePath = "cygnus_reveal", // We'll add this image later
            GridColumns = 5,
            GridRows = 4
        };

        // 20 Hives total: 4 of each theme
        var themes = new List<string>
        {
            "Summoner", "Summoner", "Summoner", "Summoner",
            "Exploder", "Exploder", "Exploder", "Exploder",
            "Range",    "Range",    "Range",    "Range",
            "Speedster","Speedster","Speedster","Speedster",
            "Mixed",    "Mixed",    "Mixed",    "Mixed"
        };

        // Shuffle so they aren't clumped together
        var rng = new Random();
        for (int i = themes.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = themes[i];
            themes[i] = themes[j];
            themes[j] = temp;
        }

        // Generate the 20 hives
        for (int i = 0; i < 20; i++)
        {
            int x = i % 5;
            int y = i / 5;

            map.Hives.Add(new HiveData(
                id: $"map_01_hive_{i:D2}",
                type: HiveType.Normal,
                worldPosition: Microsoft.Xna.Framework.Vector2.Zero,
                regionPosition: Microsoft.Xna.Framework.Point.Zero,
                radiusX: 100, 
                radiusY: 100, 
                generationSeed: i, 
                difficulty: 1
            )
            {
                GridX = x,
                GridY = y,
                Theme = themes[i]
            });
        }

        return map;
    }
}