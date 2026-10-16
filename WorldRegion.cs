using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework;

namespace Nanite;

internal sealed class WorldRegion
{
    public Point Coordinates { get; }
    public int Seed { get; }
    public bool IsGenerated { get; set; }
    public List<HiveData> Hives { get; } = new();
    public HashSet<Point> ModifiedTerrainChunks { get; } = new();
    public HashSet<string> CollectedResources { get; } = new();
    public HashSet<string> DestroyedObjects { get; } = new();
    public HashSet<Point> DiscoveredLocations { get; } = new();

    public WorldRegion(Point coordinates, int seed)
    {
        Coordinates = coordinates;
        Seed = seed;
    }
}

internal sealed class WorldGenerator
{
    public int WorldSeed { get; }

    public WorldGenerator(int worldSeed)
    {
        WorldSeed = worldSeed;
    }

    public int GetRegionSeed(int regionX, int regionY)
    {
        return DeriveRegionSeed(WorldSeed, regionX, regionY);
    }

    public const int MaxHiveRadius = 1280;

    public static int DeriveRegionSeed(int worldSeed, int regionX, int regionY)
    {
        unchecked
        {
            uint seed = (uint)worldSeed;
            seed ^= (uint)regionX * 0x9E3779B9u;
            seed = (seed << 13) | (seed >> 19);
            seed ^= (uint)regionY * 0x85EBCA6Bu;
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return (int)seed;
        }
    }

    public WorldRegion GenerateRegion(Point coordinates)
    {
        int seed = GetRegionSeed(coordinates.X, coordinates.Y);
        var region = new WorldRegion(coordinates, seed)
        {
            IsGenerated = true
        };

        Random random = new Random(seed);
        int hiveRoll = random.Next(6);
        if (hiveRoll >= 3) return region;

        HiveType type = hiveRoll < 2 ? HiveType.Normal : HiveType.Dangerous;
        int minimumRadius = type == HiveType.Normal ? 512 : 768;
        int maximumRadius = type == HiveType.Normal ? 1024 : MaxHiveRadius;
        int radiusX = random.Next(minimumRadius, maximumRadius + 1);
        int radiusY = random.Next(minimumRadius, maximumRadius + 1);
        float worldX = coordinates.X * WorldDimensions.RegionWidth +
            random.Next(MaxHiveRadius, WorldDimensions.RegionWidth - MaxHiveRadius);
        float worldY = coordinates.Y * WorldDimensions.RegionHeight +
            random.Next(MaxHiveRadius, WorldDimensions.RegionHeight - MaxHiveRadius);
        int difficulty = type == HiveType.Dangerous ? 3 : 1;
        var hive = new HiveData(
            $"{coordinates.X}:{coordinates.Y}:{type}:0",
            type,
            new Vector2(worldX, worldY),
            coordinates,
            radiusX,
            radiusY,
            seed,
            difficulty);
        region.Hives.Add(hive);
        return region;
    }
}