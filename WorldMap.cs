using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nanite;

internal sealed class WorldMap
{
    private readonly HiveManager _hives;

    public WorldMap(HiveManager hives)
    {
        _hives = hives;
    }

    public IEnumerable<MapDot> GetDots() => _hives.Hives
        .Where(hive => hive.Discovered)
        .Select(hive => new MapDot(hive));

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds,
        Vector2 playerWorldPosition, bool showRegionBoundaries = false)
    {
        spriteBatch.Draw(pixel, bounds, Color.Black * 0.85f);

        if (showRegionBoundaries)
        {
            for (int x = 0; x <= WorldDimensions.RegionCountX; x++)
            {
                int screenX = bounds.X + x * bounds.Width / WorldDimensions.RegionCountX;
                spriteBatch.Draw(pixel, new Rectangle(screenX, bounds.Y, 1, bounds.Height), Color.DimGray);
            }
            for (int y = 0; y <= WorldDimensions.RegionCountY; y++)
            {
                int screenY = bounds.Y + y * bounds.Height / WorldDimensions.RegionCountY;
                spriteBatch.Draw(pixel, new Rectangle(bounds.X, screenY, bounds.Width, 1), Color.DimGray);
            }
        }

        foreach (MapDot dot in GetDots())
        {
            Vector2 position = ToMapPosition(dot.WorldPosition, bounds);
            Color color = dot.Completed
                ? Color.LimeGreen
                : dot.Type == HiveType.Dangerous ? Color.OrangeRed : Color.Cyan;
            spriteBatch.Draw(pixel, new Rectangle((int)position.X - 3, (int)position.Y - 3, 7, 7), color);
        }

        Vector2 playerPosition = ToMapPosition(playerWorldPosition, bounds);
        spriteBatch.Draw(pixel, new Rectangle((int)playerPosition.X - 4, (int)playerPosition.Y - 4, 9, 9), Color.White);
    }

    private static Vector2 ToMapPosition(Vector2 worldPosition, Rectangle bounds)
    {
        float x = MathHelper.Clamp(worldPosition.X / WorldDimensions.WorldWidth, 0f, 1f);
        float y = MathHelper.Clamp(worldPosition.Y / WorldDimensions.WorldHeight, 0f, 1f);
        return new Vector2(bounds.X + x * bounds.Width, bounds.Y + y * bounds.Height);
    }
}