using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

public class Game1 : Game
{
    // ---- Engine ----
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SpriteFont _font;

    // ---- Public accessors (used by Screen base) ----
    public SpriteBatch SpriteBatch => _spriteBatch;
    public Texture2D Pixel => _pixel;
    public SpriteFont Font => _font;

    // ---- Current screen ----
    private Screen _currentScreen;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = displayMode.Width;
        _graphics.PreferredBackBufferHeight = displayMode.Height;
        _graphics.IsFullScreen = true;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = Content.Load<SpriteFont>("font");

        // Start on the menu screen
        _currentScreen = new MenuScreen(this);
    }

    /// <summary>
    /// Called by screens when they want to change state.
    /// </summary>
    private void SwitchScreen(Screen nextScreen)
    {
        _currentScreen = nextScreen;
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

        _currentScreen.Update(gameTime, keyboard, mouse);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _currentScreen.Draw(gameTime);
        base.Draw(gameTime);
    }
}