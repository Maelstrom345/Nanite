using Microsoft.Xna.Framework;

namespace Nanite;

internal sealed class Camera2D
{
    public Vector2 Position { get; private set; }
    public Matrix TransformMatrix { get; private set; } = Matrix.Identity;

    public void Follow(Vector2 target, int viewportWidth, int viewportHeight)
    {
        Position = target - new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        TransformMatrix = Matrix.CreateTranslation(-Position.X, -Position.Y, 0f);
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition) => screenPosition + Position;

    public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition - Position;
}