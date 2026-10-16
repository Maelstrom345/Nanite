using System;
using Microsoft.Xna.Framework;

namespace Nanite;

internal static class WorldDimensions
{
    public const int RegionCountX = 25;
    public const int RegionCountY = 25;
    public const int ScreensPerRegionX = 10;
    public const int ScreensPerRegionY = 10;
    public const int LogicalScreenWidth = 1920;
    public const int LogicalScreenHeight = 1080;

    public const int RegionWidth = ScreensPerRegionX * LogicalScreenWidth;
    public const int RegionHeight = ScreensPerRegionY * LogicalScreenHeight;
    public const int WorldWidth = RegionCountX * RegionWidth;
    public const int WorldHeight = RegionCountY * RegionHeight;

    public static Vector2 WorldCenter => new(WorldWidth / 2f, WorldHeight / 2f);

    public static Point GetRegionCoordinates(Vector2 worldPosition)
    {
        int x = (int)MathF.Floor(worldPosition.X / RegionWidth);
        int y = (int)MathF.Floor(worldPosition.Y / RegionHeight);
        return new Point(
            Math.Clamp(x, 0, RegionCountX - 1),
            Math.Clamp(y, 0, RegionCountY - 1));
    }

    public static Vector2 ClampToWorld(Vector2 worldPosition, float margin = 0f)
    {
        return new Vector2(
            Math.Clamp(worldPosition.X, margin, WorldWidth - margin),
            Math.Clamp(worldPosition.Y, margin, WorldHeight - margin));
    }
}