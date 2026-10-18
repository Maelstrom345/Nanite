using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

internal sealed class HiveMapScreen : Screen
{
    private readonly HiveMap _map;
    private MouseState _previousMouse;
    private Texture2D _mapPicture;
    
    // Grid Layout
    private const int TileSize = 100;
    private const int TileGap = 20;
    private Vector2 _gridOrigin;

    public HiveMapScreen(Game1 game) : base(game)
    {
        // Load the map data
        _map = HiveMapDatabase.GetMapOne();
        
        // Load the hidden picture
        try { _mapPicture = game.Content.Load<Texture2D>(_map.PicturePath); }
        catch { _mapPicture = null; }

        // Center the grid on the screen
        float totalWidth = (_map.GridColumns * TileSize) + ((_map.GridColumns - 1) * TileGap);
        float totalHeight = (_map.GridRows * TileSize) + ((_map.GridRows - 1) * TileGap);
        _gridOrigin = new Vector2(
            (GraphicsDevice.Viewport.Width - totalWidth) / 2f,
            (GraphicsDevice.Viewport.Height - totalHeight) / 2f
        );
    }

    public override void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        bool justClicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        
        if (justClicked)
        {
            Point mousePoint = new Point(mouse.X, mouse.Y);

            foreach (var hive in _map.Hives)
            {
                Rectangle tileRect = GetTileRect(hive);
                if (tileRect.Contains(mousePoint) && !hive.Cleared)
                {
                    Game.SwitchScreen(new ArenaScreen(Game, hive));
                    return;
                }
            }
        }
        
        if (keyboard.IsKeyDown(Keys.Escape))
            Game.SwitchScreen(new MenuScreen(Game));

        _previousMouse = mouse;
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 5, 25)); // Deep space color

        SpriteBatch.Begin();

        // Title
        string title = _map.Name + " Constellation";
        Vector2 titleSize = Font.MeasureString(title);
        SpriteBatch.DrawString(Font, title, new Vector2((GraphicsDevice.Viewport.Width - titleSize.X) / 2f, 50), Color.White);

        foreach (var hive in _map.Hives)
        {
            Rectangle rect = GetTileRect(hive);

            if (hive.Cleared)
            {
                // --- CLEARED TILE (REVEAL PICTURE) ---
                if (_mapPicture != null)
                {
                    Rectangle sourceRect = new Rectangle(
                        hive.GridX * (_mapPicture.Width / _map.GridColumns),
                        hive.GridY * (_mapPicture.Height / _map.GridRows),
                        _mapPicture.Width / _map.GridColumns,
                        _mapPicture.Height / _map.GridRows
                    );
                    SpriteBatch.Draw(_mapPicture, rect, sourceRect, Color.White);
                }
                else
                {
                    // Fallback if picture is missing
                    SpriteBatch.Draw(Pixel, rect, Color.LimeGreen);
                }
            }
            else
            {
                // --- UNCLEARED TILE (HIDDEN) ---
                Point mousePoint = new Point(Mouse.GetState().X, Mouse.GetState().Y);
                bool isHovered = rect.Contains(mousePoint);

                Color tileColor = isHovered ? new Color(60, 60, 80) : new Color(30, 30, 40);
                SpriteBatch.Draw(Pixel, rect, tileColor);
                
                if (isHovered)
                {
                    int border = 3;
                    SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, border), Color.White);
                    SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), Color.White);
                    SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, border, rect.Height), Color.White);
                    SpriteBatch.Draw(Pixel, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), Color.White);
                }

                // Draw Star
                int cx = rect.Center.X;
                int cy = rect.Center.Y;
                SpriteBatch.Draw(Pixel, new Rectangle(cx - 2, cy - 15, 4, 30), Color.Gold);
                SpriteBatch.Draw(Pixel, new Rectangle(cx - 15, cy - 2, 30, 4), Color.Gold);
                
                // Draw Theme
                string theme = hive.Theme;
                Vector2 tSize = Font.MeasureString(theme);
                SpriteBatch.DrawString(Font, theme, new Vector2(cx - tSize.X / 2f, rect.Bottom - 20), Color.Gray);
            }
        }

        SpriteBatch.DrawString(Font, "Click a star to enter the hive. Press ESC for menu.", new Vector2(20, GraphicsDevice.Viewport.Height - 40), Color.DarkGray);

        SpriteBatch.End();
    }

    private Rectangle GetTileRect(HiveData hive)
    {
        return new Rectangle(
            (int)(_gridOrigin.X + hive.GridX * (TileSize + TileGap)),
            (int)(_gridOrigin.Y + hive.GridY * (TileSize + TileGap)),
            TileSize, TileSize
        );
    }
}