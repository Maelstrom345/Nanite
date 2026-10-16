using Microsoft.Xna.Framework;

namespace Nanite;

internal sealed class MapDot
{
    public string Id { get; }
    public Vector2 WorldPosition { get; }
    public HiveType Type { get; }
    public bool Discovered { get; set; }
    public bool Completed { get; set; }

    public MapDot(HiveData hive)
    {
        Id = hive.Id;
        WorldPosition = hive.WorldPosition;
        Type = hive.Type;
        Discovered = hive.Discovered;
        Completed = hive.Completed;
    }
}