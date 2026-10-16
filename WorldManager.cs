using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class WorldManager
{
    private const int ActiveChunkPadding = 3;
    private  WorldGenerator _generator;
    private readonly Dictionary<Point, WorldRegion> _regions = new();
    private readonly HashSet<Point> _activeRegions = new();
    private HashSet<Point> _activeChunks = new();
    private HashSet<Point> _nextActiveChunks = new();
    private readonly Dictionary<Point, WorldChunk> _chunks = new();
    private  WorldChunkGenerator _chunkGenerator;
    private readonly List<Vector2> _normalHiveLocations = new();
    private readonly List<Vector2> _dangerHiveLocations = new();
        private Color[] _dirtyPixelBuffer = Array.Empty<Color>();
    private bool _allRegionMetadataForDebugGenerated;
    private bool _hasActiveChunkWindow;
    private bool _hasPlayerRegion;
    private int _activeFirstChunkX;
    private int _activeFirstChunkY;
    private int _activeLastChunkX;
    private int _activeLastChunkY;

    public int WorldSeed => _generator.WorldSeed;
    public Point CurrentRegion { get; private set; }
    public IReadOnlyCollection<Point> ActiveRegions => _activeRegions;
    public IReadOnlyCollection<Point> ActiveChunks => _activeChunks;
    public IReadOnlyDictionary<Point, WorldChunk> Chunks => _chunks;
    public IReadOnlyList<Vector2> NormalHiveLocations => _normalHiveLocations;
    public IReadOnlyList<Vector2> DangerousHiveLocations => _dangerHiveLocations;
    public HiveManager Hives { get; } = new();
    public WorldMap Map { get; }

    public WorldManager(int worldSeed)
    {
        _generator = new WorldGenerator(worldSeed);
        _chunkGenerator = new WorldChunkGenerator(worldSeed);
        Map = new WorldMap(Hives);
    }

    public void PreGenerateArea(GraphicsDevice graphicsDevice, Vector2 centerPosition, int radiusInChunks)
{
    int centerChunkX = GetChunkCoordinate(centerPosition.X);
    int centerChunkY = GetChunkCoordinate(centerPosition.Y);

    for (int y = -radiusInChunks; y <= radiusInChunks; y++)
    {
        for (int x = -radiusInChunks; x <= radiusInChunks; x++)
        {
            WorldChunk chunk = GetOrCreateChunk(centerChunkX + x, centerChunkY + y);
            ActivateChunk(graphicsDevice, chunk);
        }
    }
}
    public void Reset(int newSeed)
{
    // Release the old chunks and textures
    ReleaseChunks();
    
    // Re-initialize the generators with the new seed
    _generator = new WorldGenerator(newSeed);
    _chunkGenerator = new WorldChunkGenerator(newSeed);
    
    // Reset state
    _hasActiveChunkWindow = false;
    _hasPlayerRegion = false;
    // You may also want to reset _regions and Hives here if needed
}

    public WorldRegion GetOrGenerateRegion(Point coordinates)
    {
        if (!_regions.TryGetValue(coordinates, out WorldRegion region))
        {
            region = _generator.GenerateRegion(coordinates);
            _regions.Add(coordinates, region);
            foreach (HiveData hive in region.Hives) Hives.Register(hive);
        }
        return region;
    }

    public WorldRegion GetRegionAt(Vector2 worldPosition) => GetOrGenerateRegion(
        WorldDimensions.GetRegionCoordinates(worldPosition));

    public void GenerateAllRegionMetadataForDebug()
    {
        if (_allRegionMetadataForDebugGenerated) return;

        for (int regionY = 0; regionY < WorldDimensions.RegionCountY; regionY++)
        {
            for (int regionX = 0; regionX < WorldDimensions.RegionCountX; regionX++)
            {
                GetOrGenerateRegion(new Point(regionX, regionY));
            }
        }
        _allRegionMetadataForDebugGenerated = true;
    }

    public void UpdatePlayerPosition(Vector2 worldPosition, int nearbyRegionRadius = 1)
    {
        Point nextRegion = WorldDimensions.GetRegionCoordinates(worldPosition);
        if (!_hasPlayerRegion || nextRegion != CurrentRegion)
        {
            CurrentRegion = nextRegion;
            _hasPlayerRegion = true;
            _activeRegions.Clear();

            for (int y = Math.Max(0, CurrentRegion.Y - nearbyRegionRadius);
                 y <= Math.Min(WorldDimensions.RegionCountY - 1, CurrentRegion.Y + nearbyRegionRadius); y++)
            {
                for (int x = Math.Max(0, CurrentRegion.X - nearbyRegionRadius);
                     x <= Math.Min(WorldDimensions.RegionCountX - 1, CurrentRegion.X + nearbyRegionRadius); x++)
                {
                    Point coordinates = new Point(x, y);
                    GetOrGenerateRegion(coordinates);
                    _activeRegions.Add(coordinates);
                }
            }
        }

        Hives.DiscoverNearby(worldPosition);
    }

    public WorldRegion GetRegionForChunk(int chunkX, int chunkY)
    {
        Vector2 chunkCenter = new Vector2(
            chunkX * WorldChunk.Size + WorldChunk.Size / 2f,
            chunkY * WorldChunk.Size + WorldChunk.Size / 2f);
        return GetRegionAt(chunkCenter);
    }

    public bool TryGetNearestIncompleteHive(Vector2 worldPosition, float maximumDistance,
        out HiveData hive)
    {
        return Hives.TryGetNearestIncomplete(worldPosition, maximumDistance, out hive);
    }

    public void UpdateActiveChunks(GraphicsDevice graphicsDevice, Vector2 cameraPosition,
        int viewportWidth, int viewportHeight)
    {
        int firstChunkX = GetChunkCoordinate(cameraPosition.X) - ActiveChunkPadding;
        int firstChunkY = GetChunkCoordinate(cameraPosition.Y) - ActiveChunkPadding;
        int lastChunkX = GetChunkCoordinate(cameraPosition.X + viewportWidth) + ActiveChunkPadding;
        int lastChunkY = GetChunkCoordinate(cameraPosition.Y + viewportHeight) + ActiveChunkPadding;
        if (_hasActiveChunkWindow &&
            firstChunkX == _activeFirstChunkX && firstChunkY == _activeFirstChunkY &&
            lastChunkX == _activeLastChunkX && lastChunkY == _activeLastChunkY)
        {
            return;
        }

        _hasActiveChunkWindow = true;
        _activeFirstChunkX = firstChunkX;
        _activeFirstChunkY = firstChunkY;
        _activeLastChunkX = lastChunkX;
        _activeLastChunkY = lastChunkY;
        _nextActiveChunks.Clear();

        for (int chunkY = firstChunkY; chunkY <= lastChunkY; chunkY++)
        {
            for (int chunkX = firstChunkX; chunkX <= lastChunkX; chunkX++)
            {
                Point coordinates = new Point(chunkX, chunkY);
                WorldChunk chunk = GetOrCreateChunk(chunkX, chunkY);
                ActivateChunk(graphicsDevice, chunk);
                _nextActiveChunks.Add(coordinates);
            }
        }

        foreach (Point coordinates in _activeChunks)
        {
            if (_nextActiveChunks.Contains(coordinates)) continue;

            WorldChunk chunk = _chunks[coordinates];
            chunk.ReleaseActiveResources();
            if (!chunk.HasDugCells) _chunks.Remove(coordinates);
        }

        (_activeChunks, _nextActiveChunks) = (_nextActiveChunks, _activeChunks);
        RefreshHiveLocations();
    }

    public WorldChunk GetChunkForCell(GraphicsDevice graphicsDevice, int worldX, int worldY,
        out int localX, out int localY)
    {
        int chunkX = GetChunkCoordinate(worldX);
        int chunkY = GetChunkCoordinate(worldY);
        localX = worldX - chunkX * WorldChunk.Size;
        localY = worldY - chunkY * WorldChunk.Size;
        WorldChunk chunk = GetOrCreateChunk(chunkX, chunkY);
        ActivateChunk(graphicsDevice, chunk);
        return chunk;
    }

    public bool CanOccupyTerrain(Vector2 position, int size)
    {
        float halfSize = size / 2f;
        for (int corner = 0; corner < 4; corner++)
        {
            float cornerX = position.X + ((corner & 1) == 0 ? -halfSize : halfSize);
            float cornerY = position.Y + ((corner & 2) == 0 ? -halfSize : halfSize);
            int worldX = (int)MathF.Floor(cornerX);
            int worldY = (int)MathF.Floor(cornerY);
            int chunkX = GetChunkCoordinate(worldX);
            int chunkY = GetChunkCoordinate(worldY);
            int localX = worldX - chunkX * WorldChunk.Size;
            int localY = worldY - chunkY * WorldChunk.Size;
            if (!_chunks.TryGetValue(new Point(chunkX, chunkY), out WorldChunk chunk)) return false;
            int index = localY * WorldChunk.Size + localX;
            if (chunk.DugCells[index]) continue;
            if (chunk.Pixels == null || IsDirtColor(chunk.Pixels[index])) return false;
        }
        return true;
    }

    public void DigTerrainAroundPosition(GraphicsDevice graphicsDevice, Vector2 worldPosition, float radius)
    {
        int centerX = (int)MathF.Floor(worldPosition.X);
        int centerY = (int)MathF.Floor(worldPosition.Y);
        int cellRadius = (int)Math.Ceiling(radius);
        int left = centerX - cellRadius;
        int top = centerY - cellRadius;
        int right = centerX + cellRadius;
        int bottom = centerY + cellRadius;
        var dirtyRegions = new Dictionary<Point, Rectangle>();

        for (int worldY = top; worldY <= bottom; worldY++)
        {
            float dy = (worldY - worldPosition.Y) / cellRadius;
            for (int worldX = left; worldX <= right; worldX++)
            {
                float dx = (worldX - worldPosition.X) / cellRadius;
                if (dx * dx + dy * dy > 1f) continue;

                WorldChunk chunk = GetChunkForCell(graphicsDevice, worldX, worldY,
                    out int localX, out int localY);
                int index = localY * WorldChunk.Size + localX;
               if (chunk.DugCells[index] || !IsDirtColor(chunk.Pixels[index]) || IsHiveColor(chunk.Pixels[index])) continue;

                chunk.DugCells[index] = true;
                chunk.HasDugCells = true;
                chunk.Pixels[index] = Color.DarkSlateGray;
                Point coordinates = new Point(chunk.ChunkX, chunk.ChunkY);
                Rectangle changedCell = new Rectangle(localX, localY, 1, 1);
                if (dirtyRegions.TryGetValue(coordinates, out Rectangle dirtyRegion))
                {
                    dirtyRegions[coordinates] = Rectangle.Union(dirtyRegion, changedCell);
                }
                else
                {
                    GetRegionForChunk(chunk.ChunkX, chunk.ChunkY)
                        .ModifiedTerrainChunks.Add(coordinates);
                    dirtyRegions.Add(coordinates, changedCell);
                }
            }
        }

        foreach (KeyValuePair<Point, Rectangle> dirtyChunk in dirtyRegions)
        {
            WorldChunk chunk = _chunks[dirtyChunk.Key];
            Rectangle region = dirtyChunk.Value;
                int pixelCount = region.Width * region.Height;
                if (_dirtyPixelBuffer.Length < pixelCount) _dirtyPixelBuffer = new Color[pixelCount];
            for (int row = 0; row < region.Height; row++)
            {
                Array.Copy(chunk.Pixels, (region.Y + row) * WorldChunk.Size + region.X,
                    _dirtyPixelBuffer, row * region.Width, region.Width);
            }
                chunk.Texture.SetData(0, region, _dirtyPixelBuffer, 0, pixelCount);
        }
    }

    public void ReleaseChunks()
    {
        foreach (WorldChunk chunk in _chunks.Values) chunk.ReleaseActiveResources();
        _chunks.Clear();
        _activeChunks.Clear();
        _nextActiveChunks.Clear();
        _hasActiveChunkWindow = false;
        _normalHiveLocations.Clear();
        _dangerHiveLocations.Clear();
    }

    public void UnloadActiveChunks()
    {
        foreach (Point coordinates in _activeChunks)
        {
            if (!_chunks.TryGetValue(coordinates, out WorldChunk chunk)) continue;
            chunk.ReleaseActiveResources();
            if (!chunk.HasDugCells) _chunks.Remove(coordinates);
        }

        _activeChunks.Clear();
        _nextActiveChunks.Clear();
        _hasActiveChunkWindow = false;
        _normalHiveLocations.Clear();
        _dangerHiveLocations.Clear();
    }

    public void DrawActiveChunks(SpriteBatch spriteBatch)
    {
        foreach (Point coordinates in _activeChunks)
        {
            WorldChunk chunk = _chunks[coordinates];
            if (chunk.Texture == null) continue;
            spriteBatch.Draw(chunk.Texture,
                new Rectangle(chunk.ChunkX * WorldChunk.Size, chunk.ChunkY * WorldChunk.Size,
                    WorldChunk.Size, WorldChunk.Size),
                Color.White);
        }
    }

    private WorldChunk GetOrCreateChunk(int chunkX, int chunkY)
    {
        Point coordinates = new Point(chunkX, chunkY);
        if (!_chunks.TryGetValue(coordinates, out WorldChunk chunk))
        {
            chunk = new WorldChunk(chunkX, chunkY, _chunkGenerator.GetChunkSeed(chunkX, chunkY));
            _chunks.Add(coordinates, chunk);
        }
        return chunk;
    }

    private void ActivateChunk(GraphicsDevice graphicsDevice, WorldChunk chunk)
    {
        if (chunk.Texture != null) return;

        chunk.Pixels = _chunkGenerator.GeneratePixels(chunk.ChunkX, chunk.ChunkY);
        foreach (HiveData hive in GetHivesIntersectingChunk(chunk.ChunkX, chunk.ChunkY))
        {
            WorldChunkGenerator.PaintHivePatch(chunk.Pixels, chunk.ChunkX, chunk.ChunkY, hive);
        }

        for (int i = 0; i < chunk.DugCells.Length; i++)
        {
            if (chunk.DugCells[i]) chunk.Pixels[i] = Color.DarkSlateGray;
        }

        chunk.Texture = new Texture2D(graphicsDevice, WorldChunk.Size, WorldChunk.Size);
        chunk.Texture.SetData(chunk.Pixels);
    }

    private IEnumerable<HiveData> GetHivesIntersectingChunk(int chunkX, int chunkY)
    {
        float chunkLeft = chunkX * WorldChunk.Size;
        float chunkTop = chunkY * WorldChunk.Size;
        float chunkRight = chunkLeft + WorldChunk.Size;
        float chunkBottom = chunkTop + WorldChunk.Size;
        float searchPadding = WorldGenerator.MaxHiveRadius;
        Point firstRegion = WorldDimensions.GetRegionCoordinates(new Vector2(
            chunkLeft - searchPadding, chunkTop - searchPadding));
        Point lastRegion = WorldDimensions.GetRegionCoordinates(new Vector2(
            chunkRight + searchPadding, chunkBottom + searchPadding));

        for (int regionY = firstRegion.Y; regionY <= lastRegion.Y; regionY++)
        {
            for (int regionX = firstRegion.X; regionX <= lastRegion.X; regionX++)
            {
                GetOrGenerateRegion(new Point(regionX, regionY));
            }
        }

        Vector2 chunkCenter = new Vector2(chunkLeft + WorldChunk.Size / 2f,
            chunkTop + WorldChunk.Size / 2f);
        float searchRadius = MathF.Sqrt(2f) *
            (WorldGenerator.MaxHiveRadius + WorldChunk.Size / 2f);
        return Hives.GetNearby(chunkCenter, searchRadius);
    }

    private void RefreshHiveLocations()
    {
        _normalHiveLocations.Clear();
        _dangerHiveLocations.Clear();
        foreach (HiveData hive in Hives.Hives)
        {
            int chunkX = GetChunkCoordinate(hive.WorldPosition.X);
            int chunkY = GetChunkCoordinate(hive.WorldPosition.Y);
            if (!_activeChunks.Contains(new Point(chunkX, chunkY))) continue;

            if (hive.Type == HiveType.Dangerous)
                _dangerHiveLocations.Add(hive.WorldPosition);
            else
                _normalHiveLocations.Add(hive.WorldPosition);
        }
    }

    private static int GetChunkCoordinate(float worldCoordinate)
    {
        return (int)MathF.Floor(worldCoordinate / WorldChunk.Size);
    }

    private static bool IsDirtColor(Color color) => color.B > 200 && color.R < 130;
    private static bool IsHiveColor(Color color) => color.R > 150 && color.G > 50 && color.B < 150;
}