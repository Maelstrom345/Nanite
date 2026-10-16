using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Nanite;

internal enum HiveType
{
    Normal,
    Dangerous
}

internal sealed class HiveData
{
    public string Id { get; }
    public HiveType Type { get; }
    public Vector2 WorldPosition { get; }
    public Point RegionPosition { get; }
    public float TerritorySize { get; }
    public int RadiusX { get; }
    public int RadiusY { get; }
    public int GenerationSeed { get; }
    public int Difficulty { get; }
    public bool Discovered { get; set; }
    public bool Completed { get; set; }
public int RegionX{get; set;}
public int RegionY{get; set;}
    public HiveData(string id, HiveType type, Vector2 worldPosition, Point regionPosition,
        int radiusX, int radiusY, int generationSeed, int difficulty)
    {
        Id = id;
        Type = type;
        WorldPosition = worldPosition;
        RegionPosition = regionPosition;
        RadiusX = radiusX;
        RadiusY = radiusY;
        TerritorySize = radiusX + radiusY;
        GenerationSeed = generationSeed;
        Difficulty = difficulty;
    }
}

internal sealed class HiveManager
{
    private readonly Dictionary<string, HiveData> _hives = new();
    private readonly Dictionary<Point, List<HiveData>> _hivesByRegion = new();

    public IEnumerable<HiveData> Hives => _hives.Values;

    public bool Register(HiveData hive)
    {
        if (!_hives.TryAdd(hive.Id, hive)) return false;
        if (!_hivesByRegion.TryGetValue(hive.RegionPosition, out List<HiveData> regionHives))
        {
            regionHives = new List<HiveData>();
            _hivesByRegion.Add(hive.RegionPosition, regionHives);
        }
        regionHives.Add(hive);
        return true;
    }

    public IEnumerable<HiveData> GetNearby(Vector2 worldPosition, float radius)
    {
        float radiusSquared = radius * radius;
        int minX = Math.Max(0, (int)MathF.Floor((worldPosition.X - radius) / WorldDimensions.RegionWidth));
        int minY = Math.Max(0, (int)MathF.Floor((worldPosition.Y - radius) / WorldDimensions.RegionHeight));
        int maxX = Math.Min(WorldDimensions.RegionCountX - 1,
            (int)MathF.Floor((worldPosition.X + radius) / WorldDimensions.RegionWidth));
        int maxY = Math.Min(WorldDimensions.RegionCountY - 1,
            (int)MathF.Floor((worldPosition.Y + radius) / WorldDimensions.RegionHeight));

        var nearby = new List<HiveData>();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!_hivesByRegion.TryGetValue(new Point(x, y), out List<HiveData> regionHives)) continue;
                foreach (HiveData hive in regionHives)
                {
                    if (Vector2.DistanceSquared(hive.WorldPosition, worldPosition) <= radiusSquared)
                        nearby.Add(hive);
                }
            }
        }
        return nearby;
    }

    public void DiscoverNearby(Vector2 worldPosition)
    {
        float searchRadius = WorldGenerator.MaxHiveRadius;
        float radiusSquared = searchRadius * searchRadius;
        int minX = Math.Max(0, (int)MathF.Floor((worldPosition.X - searchRadius) / WorldDimensions.RegionWidth));
        int minY = Math.Max(0, (int)MathF.Floor((worldPosition.Y - searchRadius) / WorldDimensions.RegionHeight));
        int maxX = Math.Min(WorldDimensions.RegionCountX - 1,
            (int)MathF.Floor((worldPosition.X + searchRadius) / WorldDimensions.RegionWidth));
        int maxY = Math.Min(WorldDimensions.RegionCountY - 1,
            (int)MathF.Floor((worldPosition.Y + searchRadius) / WorldDimensions.RegionHeight));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!_hivesByRegion.TryGetValue(new Point(x, y), out List<HiveData> regionHives)) continue;
                foreach (HiveData hive in regionHives)
                {
                    if (Vector2.DistanceSquared(hive.WorldPosition, worldPosition) > radiusSquared) continue;
                    float dx = (worldPosition.X - hive.WorldPosition.X) / hive.RadiusX;
                    float dy = (worldPosition.Y - hive.WorldPosition.Y) / hive.RadiusY;
                    if (dx * dx + dy * dy <= 1f)
                        hive.Discovered = true;
                }
            }
        }
    }

    public bool MarkCompleted(string hiveId)
    {
        if (!_hives.TryGetValue(hiveId, out HiveData hive)) return false;
        hive.Completed = true;
        return true;
    }

    public bool TryGetNearestIncomplete(Vector2 worldPosition, float maximumDistance,
        out HiveData nearestHive)
    {
        nearestHive = null;
        float nearestDistanceSquared = maximumDistance * maximumDistance;
        foreach (HiveData hive in _hives.Values)
        {
            if (hive.Completed) continue;
            float distanceSquared = Vector2.DistanceSquared(hive.WorldPosition, worldPosition);
            if (distanceSquared > nearestDistanceSquared) continue;
            nearestDistanceSquared = distanceSquared;
            nearestHive = hive;
        }
        return nearestHive != null;
    }
}