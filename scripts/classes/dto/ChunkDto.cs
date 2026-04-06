using Godot;
using System.Collections.Generic;

public class ChunkDto(
	Vector2I coord,
	List<TileInstanceDto> tiles,
	List<ChunkObjectDto> objects)
{
	public Vector2I Coord = coord;
	public List<TileInstanceDto> Tiles = tiles;
	public List<ChunkObjectDto> Objects = objects;
}