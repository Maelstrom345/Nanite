using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal static class HiveArenaSettings
{
    public const int NormalArenaSize = 3072;
    public const int DangerousArenaSize = 4000;

    public const int TerrainCellSize = 32;

    public const int RedBugSpawnWeightMultiplier = 3;
    public const int EnemiesPerWaveMultiplier = 2;
    public const int MaxActiveArenaEnemies = 24;
}

internal sealed class HiveArena
{
    private const byte Floor = 0;
    private const byte Boundary = 2;

    private readonly byte[,] _terrain;

    private Texture2D _terrainTexture;

    public HiveData Hive { get; }

    public int Width { get; }
    public int Height { get; }

    public int CellSize { get; }

    public int Columns { get; }
    public int Rows { get; }

    public Vector2 PlayerSpawn { get; }

    public HiveArena(
        HiveData hive,
        int width,
        int height,
        int cellSize,
        int seed)
    {
        Hive = hive;

        Width = width;
        Height = height;
        CellSize = cellSize;

        Columns = Width / CellSize;
        Rows = Height / CellSize;

        _terrain = new byte[Columns, Rows];

        // Entire arena is floor.
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                _terrain[x, y] = Floor;
            }
        }

        // Only the outermost tiles are boundaries.
        for (int x = 0; x < Columns; x++)
        {
            _terrain[x, 0] = Boundary;
            _terrain[x, Rows - 1] = Boundary;
        }

        for (int y = 0; y < Rows; y++)
        {
            _terrain[0, y] = Boundary;
            _terrain[Columns - 1, y] = Boundary;
        }

        PlayerSpawn = new Vector2(
            Width / 2f,
            Height / 2f);
    }

    public bool CanOccupyTerrain(Vector2 position, int size)
    {
        float halfSize = size / 2f;

        float left = position.X - halfSize;
        float right = position.X + halfSize;
        float top = position.Y - halfSize;
        float bottom = position.Y + halfSize;

        // Stay inside arena.
        if (left < CellSize ||
            right >= Width - CellSize ||
            top < CellSize ||
            bottom >= Height - CellSize)
        {
            return false;
        }

        // Everything inside the boundary is empty.
        return true;
    }


    public Vector2 GetEnemySpawnPoint(
        int spawnIndex,
        int totalSpawns,
        int wave,
        int enemySize)
    {
        float centerX = Width / 2f;
        float centerY = Height / 2f;

        float spawnRadius =
            Math.Min(Width, Height) * 0.40f;

        float angle =
            totalSpawns > 0
                ? MathHelper.TwoPi * spawnIndex / totalSpawns
                    + wave * 0.35f
                : 0f;

        Vector2 position = new Vector2(
            centerX + MathF.Cos(angle) * spawnRadius,
            centerY + MathF.Sin(angle) * spawnRadius);

        float margin =
            Math.Max(enemySize, CellSize) + 16f;

        position.X = MathHelper.Clamp(
            position.X,
            margin,
            Width - margin);

        position.Y = MathHelper.Clamp(
            position.Y,
            margin,
            Height - margin);

        return position;
    }

    public int GetEnemyCountForWave(int wave)
    {
        bool dangerousHive =
            Hive.Type == HiveType.Dangerous;

        int baseCount =
            dangerousHive
                ? 6 + wave * 2
                : 2 + wave;

        return baseCount *
               HiveArenaSettings.EnemiesPerWaveMultiplier;
    }

    public void CreateGraphicsResources(
        GraphicsDevice graphicsDevice)
    {
        _terrainTexture?.Dispose();

        _terrainTexture =
            new Texture2D(
                graphicsDevice,
                Width,
                Height);

        Color[] pixels =
            new Color[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool border =
                    x < CellSize ||
                    x >= Width - CellSize ||
                    y < CellSize ||
                    y >= Height - CellSize;

                pixels[y * Width + x] =
                    border
                        ? Color.DarkSlateGray
                        : Color.Black;
            }
        }

        _terrainTexture.SetData(pixels);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_terrainTexture == null)
            return;

        spriteBatch.Draw(
            _terrainTexture,
            Vector2.Zero,
            Color.White);
    }

    public void DrawSpawnGuides(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        int wave,
        int enemySize,
        Vector2 cameraPosition,
        int viewportWidth,
        int viewportHeight)
    {
        // Intentionally empty.
        // The arena has no spawn markers or terrain.
    }

    public void ReleaseGraphicsResources()
    {
        _terrainTexture?.Dispose();
        _terrainTexture = null;
    }
}