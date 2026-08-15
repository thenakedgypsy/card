using Godot;
using System;
using System.Collections.Generic;

public partial class Board : TileMapLayer
{
	private Mouse _mouse;

	public override void _Ready()
	{
		_mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
		GenerateBoard(1234567);
	}


	// ============================================================
	// WALKABILITY
	// ============================================================

	public bool IsCellWalkable(Vector2I cellCoords)
	{
		TileData tileData = GetCellTileData(cellCoords);

		if (tileData == null)
		{
			return false;
		}

		Variant customData = tileData.GetCustomData("walkable");
		return customData.AsBool();
	}


	// ============================================================
	// MOUSE
	// ============================================================

	public override void _Process(double delta)
	{
	}


	public void MouseEnter()
	{
		_mouse?.setOverBoard(true);
	}


	public void MouseExit()
	{
		_mouse?.setOverBoard(false);
	}


	// ============================================================
	// BOARD GENERATION
	// ============================================================

	public void GenerateBoard(int seed)
	{
		if (TileSet == null)
		{
			GD.PrintErr("Board: No TileSet assigned.");
			return;
		}

		if (TileSet.GetSourceCount() == 0)
		{
			GD.PrintErr("Board: TileSet contains no sources.");
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		Vector2I tileSize = TileSet.TileSize;

		if (tileSize.X <= 0 || tileSize.Y <= 0)
		{
			GD.PrintErr("Board: Invalid TileSet tile size.");
			return;
		}

		// --------------------------------------------------------
		// GRID SIZE
		// --------------------------------------------------------

		// 50% extra width wise, but reduced by 5 tiles as requested
		int width = Mathf.Max(
			12,
			Mathf.FloorToInt((viewportSize.X / tileSize.X) * 1.5f) - 5
		);

		int totalRows = Mathf.Max(
			10,
			Mathf.FloorToInt(viewportSize.Y / tileSize.Y)
		);


		// --------------------------------------------------------
		// ISOMETRIC PLAYABLE BAND
		// --------------------------------------------------------

		int playableRows = Mathf.Max(
			7,
			Mathf.FloorToInt(totalRows * 0.35f)
		);

		int topRow = (totalRows - playableRows) / 2;
		int bottomRow = topRow + playableRows - 1;


		// --------------------------------------------------------
		// HORIZONTAL EXTENT
		// --------------------------------------------------------

		int startX = 1;
		int endX = width - 2;


		// --------------------------------------------------------
		// COMMON ENDPOINT
		// --------------------------------------------------------

		int middleY = topRow + playableRows / 2;

		Vector2I startPoint = new Vector2I(startX, middleY);
		Vector2I endPoint = new Vector2I(endX, middleY);


		// --------------------------------------------------------
		// TILE SOURCE
		// --------------------------------------------------------

		int sourceId = TileSet.GetSourceId(0);
		Vector2I atlasCoords = new Vector2I(0, 0);


		// --------------------------------------------------------
		// CLEAR OLD BOARD
		// --------------------------------------------------------

		Clear();


		// --------------------------------------------------------
		// SEEDED RANDOM
		// --------------------------------------------------------

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)seed;


		// --------------------------------------------------------
		// GENERATE MAIN ROUTES
		// --------------------------------------------------------

		List<Vector2I> route1 = GenerateRoute(startPoint, endPoint, topRow, bottomRow, -1, rng);
		List<Vector2I> route2 = GenerateRoute(startPoint, endPoint, topRow, bottomRow,  0, rng);
		List<Vector2I> route3 = GenerateRoute(startPoint, endPoint, topRow, bottomRow,  1, rng);


		// --------------------------------------------------------
		// DRAW MAIN ROUTES
		// 65% chance to be thick.
		// --------------------------------------------------------

		DrawThickRoute(route1, sourceId, atlasCoords, rng.Randf() < 0.65f ? 2 : 1);
		DrawThickRoute(route2, sourceId, atlasCoords, rng.Randf() < 0.65f ? 2 : 1);
		DrawThickRoute(route3, sourceId, atlasCoords, rng.Randf() < 0.65f ? 2 : 1);


		// --------------------------------------------------------
		// GENERATE & DRAW SPINDLY ROUTES
		// Strictly 1 tile wide, meandering randomly.
		// --------------------------------------------------------

		int spindlyCount = rng.RandiRange(1, 3);
		for (int i = 0; i < spindlyCount; i++)
		{
			List<Vector2I> spindlyRoute = GenerateRoute(startPoint, endPoint, topRow, bottomRow, 0, rng, true);
			DrawThickRoute(spindlyRoute, sourceId, atlasCoords, 1);
		}


		// --------------------------------------------------------
		// OPTIONAL JUNCTIONS / CROSS-CONNECTORS
		// --------------------------------------------------------

		AddConnections(route1, route2, sourceId, atlasCoords, rng, topRow, bottomRow);
		AddConnections(route2, route3, sourceId, atlasCoords, rng, topRow, bottomRow);
	}


	// ============================================================
	// ROUTE GENERATION
	// ============================================================

	private List<Vector2I> GenerateRoute(
		Vector2I start,
		Vector2I end,
		int topRow,
		int bottomRow,
		int routeBias, 
		RandomNumberGenerator rng,
		bool isSpindly = false)
	{
		List<Vector2I> route = new List<Vector2I>();
		List<Vector2I> waypoints = new List<Vector2I> { start };

		int numWaypoints = isSpindly ? rng.RandiRange(8, 14) : rng.RandiRange(4, 7);
		float xStep = (end.X - start.X) / (float)(numWaypoints + 1);

		int laneHeight = Mathf.Max(1, (bottomRow - topRow) / 3);

		for (int i = 1; i <= numWaypoints; i++)
		{
			int driftX = isSpindly ? rng.RandiRange(-3, 3) : rng.RandiRange(-1, 1);
			int wayX = Mathf.Clamp(Mathf.RoundToInt(start.X + xStep * i) + driftX, start.X + 1, end.X - 1);
			
			int wayY;

			if (isSpindly)
			{
				wayY = rng.RandiRange(topRow, bottomRow);
			}
			else if (routeBias < 0)
			{
				// Upper lane
				wayY = rng.RandiRange(topRow, topRow + laneHeight - 1);
			}
			else if (routeBias > 0)
			{
				// Lower lane
				wayY = rng.RandiRange(bottomRow - laneHeight + 1, bottomRow);
			}
			else
			{
				// Middle lane
				int middleY = topRow + (bottomRow - topRow) / 2;
				wayY = rng.RandiRange(middleY - 1, middleY + 1);
			}

			wayY = Mathf.Clamp(wayY, topRow, bottomRow);
			waypoints.Add(new Vector2I(wayX, wayY));
		}

		waypoints.Add(end);

		Vector2I current = start;
		route.Add(current);

		for (int i = 1; i < waypoints.Count; i++)
		{
			Vector2I target = waypoints[i];

			while (current != target)
			{
				bool moveHorizontal = false;

				if (current.X != target.X && current.Y != target.Y)
				{
					float horizBias = isSpindly ? 0.45f : 0.65f;
					moveHorizontal = rng.Randf() < horizBias;
				}
				else if (current.X != target.X)
				{
					moveHorizontal = true;
				}

				if (moveHorizontal)
				{
					int stepX = target.X > current.X ? 1 : -1;
					current = new Vector2I(current.X + stepX, current.Y);
				}
				else
				{
					int stepY = target.Y > current.Y ? 1 : -1;
					current = new Vector2I(current.X, current.Y + stepY);
				}

				if (!route.Contains(current))
				{
					route.Add(current);
				}
			}
		}

		return route;
	}


	// ============================================================
	// DRAW ROUTE
	// ============================================================

	private void DrawThickRoute(
		List<Vector2I> route,
		int sourceId,
		Vector2I atlasCoords,
		int thickness)
	{
		foreach (Vector2I cell in route)
		{
			SetCell(cell, sourceId, atlasCoords, 0);

			if (thickness >= 2)
			{
				SetCell(new Vector2I(cell.X, cell.Y + 1), sourceId, atlasCoords, 0);
			}
		}
	}


	// ============================================================
	// ADD OPTIONAL CONNECTIONS
	// ============================================================

	private void AddConnections(
		List<Vector2I> routeA,
		List<Vector2I> routeB,
		int sourceId,
		Vector2I atlasCoords,
		RandomNumberGenerator rng,
		int topRow,
		int bottomRow)
	{
		if (routeA.Count == 0 || routeB.Count == 0)
			return;

		int connectionCount = rng.RandiRange(0, 2);

		for (int i = 0; i < connectionCount; i++)
		{
			int minIndex = routeA.Count / 5;
			int maxIndex = routeA.Count * 4 / 5;

			if (maxIndex <= minIndex)
				continue;

			int index = rng.RandiRange(minIndex, maxIndex);
			Vector2I a = routeA[index];

			Vector2I b = FindNearestX(routeB, a.X);

			int verticalDistance = Mathf.Abs(a.Y - b.Y);
			if (verticalDistance > 8)
				continue;

			int minY = Mathf.Min(a.Y, b.Y);
			int maxY = Mathf.Max(a.Y, b.Y);

			for (int y = minY; y <= maxY; y++)
			{
				if (y < topRow || y > bottomRow)
					continue;

				SetCell(new Vector2I(a.X, y), sourceId, atlasCoords, 0);
			}
		}
	}


	// ============================================================
	// FIND ROUTE CELL CLOSEST TO AN X POSITION
	// ============================================================

	private Vector2I FindNearestX(List<Vector2I> route, int targetX)
	{
		Vector2I closest = route[0];
		int bestDistance = Mathf.Abs(closest.X - targetX);

		foreach (Vector2I cell in route)
		{
			int distance = Mathf.Abs(cell.X - targetX);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				closest = cell;
			}
		}

		return closest;
	}
}