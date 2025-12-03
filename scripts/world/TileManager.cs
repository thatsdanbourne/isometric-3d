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
    public static Vector2I TileToChunk(Vector2I tilePos) {
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
}
