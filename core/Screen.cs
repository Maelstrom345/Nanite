using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

/// <summary>
/// Abstract base for a top-level game screen.
/// Every screen owns its own Update and Draw logic.
/// Screens don't talk to each other — Game1 mediates transitions.
/// </summary>
public abstract class Screen
{
    protected Game1 Game { get; }
    protected GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    protected SpriteBatch SpriteBatch => Game.SpriteBatch;
    protected Texture2D Pixel => Game.Pixel;
    protected SpriteFont Font => Game.Font;

    protected Screen(Game1 game)
    {
        Game = game;
    }

    public abstract void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse);
    public abstract void Draw(GameTime gameTime);
}