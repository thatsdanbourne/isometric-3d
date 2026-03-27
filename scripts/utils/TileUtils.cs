using Godot;
using System;

public static class TileUtils
{
	public const int ChunkSize = 16;
	private const int TileSize = 1;

	//world <> tile conversions
	public static Vector2I WorldToTile(Vector3 worldPos)
	{
		return new Vector2I(
			Mathf.RoundToInt(worldPos.X),
			Mathf.RoundToInt(worldPos.Z)
		);
	}

	public static Vector3 TileToWorld(Vector2I tilePos)
	{
		return new Vector3(
			tilePos.X * TileSize,
			0,
			tilePos.Y * TileSize
		);
	}

	//tile <> chunk conversions
	private static Vector2I TileToChunk(Vector2I tilePos)
	{
		return new Vector2I(
			Mathf.FloorToInt((float)tilePos.X / ChunkSize),
			Mathf.FloorToInt((float)tilePos.Y / ChunkSize)
		);
	}

	public static Vector2I ChunkToTile(Vector2I chunkCoord)
	{
		return chunkCoord * ChunkSize;
	}

	public static Vector2I GetChunkCenterTile(Vector2I chunkCoord)
	{
		var origin = ChunkToTile(chunkCoord);
		return origin + new Vector2I(ChunkSize / 2, ChunkSize / 2);
	}

	// world <> chunk conversions
	public static Vector2I WorldToChunk(Vector3 worldPos)
	{
		return TileToChunk(WorldToTile(worldPos));
	}

	public static Vector3 ChunkToWorld(Vector2I chunkCoord)
	{
		return TileToWorld(chunkCoord * ChunkSize);
	}

	private static Vector3 GetMouseWorldPosition(Camera3D camera, float groundY = 0f)
	{
		var viewport = camera.GetViewport();
		var mousePos = viewport.GetMousePosition();

		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		var planeNormal = Vector3.Up;

		var denom = planeNormal.Dot(rayDir);

		if (Mathf.Abs(denom) < 0.0001f)
			return Vector3.Zero;

		var t = (groundY - planeNormal.Dot(rayOrigin)) / denom;

		if (t < 0)
			return Vector3.Zero;

		return rayOrigin + rayDir * t;
	}

	public static Vector2I GetMouseTilePosition(Camera3D camera, float groundY = 0f)
	{
		var worldPos = GetMouseWorldPosition(camera, groundY);
		return WorldToTile(worldPos);
	}
}