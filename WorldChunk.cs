using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class WorldChunk
{
    public const int Size = 512;
    public const int PixelCount = Size * Size;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public int Seed { get; }
    public bool HasDugCells { get; set; }
    public BitArray DugCells { get; } = new BitArray(PixelCount);
    public Color[] Pixels { get; set; } = null;
    public Texture2D Texture { get; set; } = null;
   

    public WorldChunk(int chunkX, int chunkY, int seed)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Seed = seed;
    }

    public void ReleaseActiveResources()
    {
        Texture?.Dispose();
        Texture = null;
        Pixels = null;
    }
}

internal sealed class WorldChunkGenerator
{
    private const int TextureTileSize = 8;
    private readonly int _worldSeed;

    public WorldChunkGenerator(int worldSeed)
    {
        _worldSeed = worldSeed;
    }

    public int GetChunkSeed(int chunkX, int chunkY)
    {
        unchecked
        {
            Vector2 chunkCenter = new Vector2(
                chunkX * WorldChunk.Size + WorldChunk.Size / 2f,
                chunkY * WorldChunk.Size + WorldChunk.Size / 2f);
            Point region = WorldDimensions.GetRegionCoordinates(chunkCenter);
            uint seed = (uint)WorldGenerator.DeriveRegionSeed(_worldSeed, region.X, region.Y);
            seed ^= (uint)chunkX * 0x9E3779B9u;
            seed = (seed << 13) | (seed >> 19);
            seed ^= (uint)chunkY * 0x85EBCA6Bu;
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return (int)seed;
        }
    }

    public Color[] GeneratePixels(int chunkX, int chunkY)
    {
        Random random = new Random(GetChunkSeed(chunkX, chunkY));
        Color[] pixels = new Color[WorldChunk.PixelCount];
        for (int tileY = 0; tileY < WorldChunk.Size; tileY += TextureTileSize)
        {
            for (int tileX = 0; tileX < WorldChunk.Size; tileX += TextureTileSize)
            {
                Color dirtColor = new Color(
                    (byte)random.Next(88, 107),
                    0,
                    (byte)random.Next(246, 256));
                for (int y = tileY; y < tileY + TextureTileSize; y++)
                {
                    int rowStart = y * WorldChunk.Size + tileX;
                    for (int x = 0; x < TextureTileSize; x++)
                    {
                        pixels[rowStart + x] = dirtColor;
                    }
                }
            }
        }

        return pixels;
    }

    public static void PaintHivePatch(Color[] pixels, int chunkX, int chunkY, HiveData hive)
    {
        int chunkOriginX = chunkX * WorldChunk.Size;
        int chunkOriginY = chunkY * WorldChunk.Size;
        int minX = Math.Max(0, (int)MathF.Ceiling(
            hive.WorldPosition.X - hive.RadiusX - chunkOriginX));
        int maxX = Math.Min(WorldChunk.Size - 1, (int)MathF.Floor(
            hive.WorldPosition.X + hive.RadiusX - chunkOriginX));
        int minY = Math.Max(0, (int)MathF.Ceiling(
            hive.WorldPosition.Y - hive.RadiusY - chunkOriginY));
        int maxY = Math.Min(WorldChunk.Size - 1, (int)MathF.Floor(
            hive.WorldPosition.Y + hive.RadiusY - chunkOriginY));
        Color color = hive.Type == HiveType.Dangerous ? Color.Red : Color.Black;

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = chunkOriginY + y;
            float dy = (worldY - hive.WorldPosition.Y) / hive.RadiusY;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = chunkOriginX + x;
                float dx = (worldX - hive.WorldPosition.X) / hive.RadiusX;
                if (dx * dx + dy * dy <= 1f) pixels[y * WorldChunk.Size + x] = color;
            }
        }
    }
}