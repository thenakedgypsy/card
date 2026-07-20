using Godot;
using System;

public partial class Board : TileMapLayer
{
	private Mouse _mouse;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
	}

	public bool IsCellWalkable(Vector2I cellCoords)
	{
		TileData tileData = GetCellTileData(cellCoords);
		
		// If there is no tile drawn here, it's automatically not walkable
		if (tileData == null)
		{
			return false;
		}

		// Replace "walkable" with the exact name you gave your Custom Data Layer in the inspector
		Variant customData = tileData.GetCustomData("walkable");
		bool walkable = customData.AsBool();
		GD.Print($"Cell {cellCoords} | HasTile: true | Walkable Layer Value: {walkable}");
		
		return walkable;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void MouseEnter()
	{
		_mouse.setOverBoard(true);
	}

	public void MouseExit()
	{
		_mouse.setOverBoard(false);
	}
}
