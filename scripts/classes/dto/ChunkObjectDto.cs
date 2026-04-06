using System;
using Godot;

public class ChunkObjectDto
{
	public int DefinitionId;
	public Vector2I ChunkCoord;
	public Vector2I TileCoord;
	public Vector3 Position;
	public ChunkObjectSource Source;
}