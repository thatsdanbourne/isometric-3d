using Godot;
using System;

public static class TileManager
{
    public static int TileSize = 1;
    public static int ChunkSize = 16;


    //world <> tile conversions
    public static Vector2I WorldToTile(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.X / TileSize);
        int y = Mathf.FloorToInt(worldPos.Z / TileSize);
        return new Vector2I(x, y);
    }

    public static Vector3 TileToWorld(Vector2I tilePos)
    {
        return new Vector3(
            (tilePos.X + 0.5f) * TileSize,
            0,
            (tilePos.Y + 0.5f) * TileSize
        );
    }

    //tile <> chunk conversions
    public static Vector2I TileToChunk(Vector2I tilePos)
    {
        int shift = (int)Math.Log2(ChunkSize);
        return new Vector2I(
            tilePos.X >> shift,
            tilePos.Y >> shift
        );
    }

    public static Vector2I ChunkToTileOrigin(Vector2I chunkPos)
    {
        return new Vector2I(
            chunkPos.X * ChunkSize,
            chunkPos.Y * ChunkSize
        );
    }

    // world <> chunk conversions
    public static Vector2I WorldToChunk(Vector3 worldPos)
    {
        return TileToChunk(WorldToTile(worldPos));
    }

    public static Vector3 ChunkToWorldOrigin(Vector2I chunkPos)
    {
        Vector2I tileOrigin = ChunkToTileOrigin(chunkPos);
        return TileToWorld(tileOrigin);
    }

    public static Vector3 GetMouseWorldPosition(Camera3D camera, float groundY = -1f)
    {
        var viewport = camera.GetViewport();
        Vector2 mousePos = viewport.GetMousePosition();

        Vector3 rayOrigin = camera.ProjectRayOrigin(mousePos);
        Vector3 rayDir = camera.ProjectRayNormal(mousePos);

        Vector3 planeNormal = Vector3.Up;
        float planeD = groundY;

        float denom = planeNormal.Dot(rayDir);

        if (Mathf.Abs(denom) < 0.0001f)
            return Vector3.Zero;

        float t = (planeD - planeNormal.Dot(rayOrigin)) / denom;

        if (t < 0)
            return Vector3.Zero;

        return rayOrigin + rayDir * t;
    }

    public static Vector3 GetMouseTilePosition(Camera3D camera, float groundY = 0f)
    {
        Vector3 worldPos = GetMouseWorldPosition(camera, groundY);
        Vector2I tilePos = WorldToTile(worldPos);
        return TileToWorld(tilePos);
    }
}
