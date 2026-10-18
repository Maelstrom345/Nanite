using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

internal sealed class MenuScreen : Screen
{
    // ---- Menu state ----
    private float _menuZoomScale = 6f;
    private bool _isZoomingOut = false;
    private const float ZoomOutSpeed = 8f;

    // ---- Textures ----
    private readonly Texture2D _eyeSocketTexture;
    private readonly Texture2D _eyePupilTexture;
    private readonly PlayerState _playerState = new PlayerState();
    private float _pupilMaxOffset = 5f;

    private float _eyeSocketScale = 1.5f;

    public MenuScreen(Game1 game) : base(game)
    {
        _eyeSocketTexture = game.Content.Load<Texture2D>("eye_socket");
        _eyePupilTexture = game.Content.Load<Texture2D>("eye_pupil");
    }

    public override void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isZoomingOut)
        {
            _menuZoomScale = MathF.Max(1f, _menuZoomScale - ZoomOutSpeed * delta);
                   if (_menuZoomScale <= 1f)
            {
                Game.SwitchScreen(new HiveMapScreen(Game));
            }
        }
        else if (keyboard.IsKeyDown(Keys.Enter))
        {
            _isZoomingOut = true;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        SpriteBatch.Begin();

        Vector2 menuCenter = new Vector2(
            GraphicsDevice.Viewport.Width / 2f,
            GraphicsDevice.Viewport.Height / 2f);

        Vector2 menuSocketOrigin = new Vector2(
            _eyeSocketTexture.Width / 2f - 24.5f,
            _eyeSocketTexture.Height / 2f - 68.5f);
        float menuSocketScale = _menuZoomScale * _eyeSocketScale * _playerState.Size / _eyeSocketTexture.Width;
        SpriteBatch.Draw(_eyeSocketTexture, menuCenter, null, Color.White, 0f,
            menuSocketOrigin, menuSocketScale, SpriteEffects.None, 0f);

        float animationTime = (float)gameTime.TotalGameTime.TotalSeconds;
        Vector2 menuPupilOffset = new Vector2(
            MathF.Cos(animationTime),
            MathF.Sin(animationTime * 0.7f)) * (_pupilMaxOffset * _menuZoomScale * 0.5f);

        Vector2 menuPupilOrigin = new Vector2(
            _eyePupilTexture.Width / 2f - 27f,
            _eyePupilTexture.Height / 2f - 55f);
        float menuPupilScale = _menuZoomScale * _playerState.Size / _eyeSocketTexture.Width;
        SpriteBatch.Draw(_eyePupilTexture, menuCenter + menuPupilOffset, null, Color.White,
            0f, menuPupilOrigin, menuPupilScale, SpriteEffects.None, 0f);

        if (!_isZoomingOut)
        {
            string prompt = "Press ENTER";
            Vector2 size = Font.MeasureString(prompt);
            SpriteBatch.DrawString(Font, prompt,
                new Vector2(menuCenter.X - size.X / 2, menuCenter.Y + 150),
                Color.White);
        }

        SpriteBatch.End();
    }
}