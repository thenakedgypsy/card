using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Board : TileMapLayer
{
	private Mouse _mouse;

	[Export] public int DistanceBetweenPoints = 30;
	[Export] public int Paths = 3;
	[Export] public int MaxPathWidth = 2;
	[Export] public Vector2 ScreenOffset = Vector2.Zero;

	public override void _Ready()
	{
		_mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
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

		try
		{
			Variant customData = tileData.GetCustomData("walkable");
			if (customData.VariantType == Variant.Type.Bool)
			{
				return customData.AsBool();
			}
		}
		catch
		{
			// Fallback if custom data is not configured
		}

		return true;
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

	public void GenerateBoard(int seed)
	{
		Random rng = new Random(seed);
		var usedCells = GetUsedCells();

		// 1. Scan tiles and cache their properties and walkability
		var tileInfoDict = new Dictionary<Vector2I, (int sourceId, Vector2I atlasCoords, int alternativeTile)>();
		var walkableCells = new List<Vector2I>();

		foreach (Vector2I cell in usedCells)
		{
			int sourceId = GetCellSourceId(cell);
			Vector2I atlasCoords = GetCellAtlasCoords(cell);
			int altTile = GetCellAlternativeTile(cell);
			tileInfoDict[cell] = (sourceId, atlasCoords, altTile);

			if (IsCellWalkable(cell))
			{
				walkableCells.Add(cell);
			}
		}

		if (walkableCells.Count == 0)
		{
			walkableCells = new List<Vector2I>(usedCells);
		}

		// 2. Pick two points that are >= DistanceBetweenPoints apart from each other
		Vector2I p1 = walkableCells.Count > 0 ? walkableCells[0] : new Vector2I(0, 0);
		Vector2I p2 = walkableCells.Count > 1 ? walkableCells[walkableCells.Count - 1] : new Vector2I(DistanceBetweenPoints, 0);

		bool foundPair = false;
		for (int i = 0; i < walkableCells.Count; i += 3)
		{
			for (int j = i + 1; j < walkableCells.Count; j += 3)
			{
				Vector2I a = walkableCells[i];
				Vector2I b = walkableCells[j];
				int dist = Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
				if (dist >= DistanceBetweenPoints)
				{
					p1 = a;
					p2 = b;
					foundPair = true;
					break;
				}
			}
			if (foundPair) break;
		}

		// 3. Define Base Rectangle and ensure it has full 2D area (preventing 1D line collapse)
		int minX = Mathf.Min(p1.X, p2.X);
		int maxX = Mathf.Max(p1.X, p2.X);
		int minY = Mathf.Min(p1.Y, p2.Y);
		int maxY = Mathf.Max(p1.Y, p2.Y);

		int width = maxX - minX;
		int height = maxY - minY;
		int halfDist = DistanceBetweenPoints / 2;

		if (width < DistanceBetweenPoints)
		{
			int centerX = (minX + maxX) / 2;
			minX = centerX - halfDist;
			maxX = centerX + halfDist;
		}
		if (height < DistanceBetweenPoints)
		{
			int centerY = (minY + maxY) / 2;
			minY = centerY - halfDist;
			maxY = centerY + halfDist;
		}

		// Find the center point of the rectangle and position the layer in the middle of the screen
		Vector2I rectCenterCell = new Vector2I((minX + maxX) / 2, (minY + maxY) / 2);
		Vector2 localCenter = MapToLocal(rectCenterCell);
		Vector2 screenSize = GetViewportRect().Size;
		GlobalPosition = (screenSize / 2.0f) + ScreenOffset - localCenter;

		foreach (Vector2I cell in usedCells)
		{
			if (cell.X < minX || cell.X > maxX || cell.Y < minY || cell.Y > maxY)
			{
				EraseCell(cell);
			}
		}

		// 4. Devise multiple distinct non-diagonal paths between p1 and p2 using penalty-based A* routing
		HashSet<Vector2I> allPathCells = new HashSet<Vector2I>();
		List<List<Vector2I>> generatedPaths = new List<List<Vector2I>>();
		Dictionary<Vector2I, float> cellPenalties = new Dictionary<Vector2I, float>();

		int numPathsToGenerate = Mathf.Max(1, Paths);

		for (int pathIdx = 0; pathIdx < numPathsToGenerate; pathIdx++)
		{
			var path = FindAStarPath(p1, p2, minX, maxX, minY, maxY, cellPenalties, rng);
			if (path != null && path.Count > 0)
			{
				generatedPaths.Add(path);
				foreach (var cell in path)
				{
					allPathCells.Add(cell);
					// Heavily penalize used cells for subsequent paths to force distinct non-overlapping routes
					if (cell != p1 && cell != p2)
					{
						if (!cellPenalties.ContainsKey(cell)) cellPenalties[cell] = 0f;
						cellPenalties[cell] += 30.0f;
					}
				}
			}
			else
			{
				// Fallback direct line if A* fails
				var directPath = GetDirectLine(p1, p2);
				generatedPaths.Add(directPath);
				foreach (var cell in directPath) allPathCells.Add(cell);
			}
		}

		// 5. Delete all tiles not in these paths
		var remainingCells = GetUsedCells();
		foreach (Vector2I cell in remainingCells)
		{
			if (!allPathCells.Contains(cell))
			{
				EraseCell(cell);
			}
		}

		// 6. Thicken paths at random points along the path up to MaxPathWidth by re-adding tiles
		int maxThickness = Mathf.Max(1, MaxPathWidth);
		int defaultSource = 0;
		Vector2I defaultAtlas = new Vector2I(0, 0);
		int defaultAlt = 0;

		if (tileInfoDict.Count > 0)
		{
			var first = tileInfoDict.First();
			defaultSource = first.Value.sourceId;
			defaultAtlas = first.Value.atlasCoords;
			defaultAlt = first.Value.alternativeTile;
		}

		HashSet<Vector2I> thickenedCells = new HashSet<Vector2I>(allPathCells);

		foreach (var path in generatedPaths)
		{
			foreach (var cell in path)
			{
				if (rng.NextDouble() < 0.4)
				{
					int thickness = rng.Next(1, maxThickness + 1);
					for (int tx = -thickness; tx <= thickness; tx++)
					{
						for (int ty = -thickness; ty <= thickness; ty++)
						{
							if (Math.Abs(tx) + Math.Abs(ty) <= thickness)
							{
								Vector2I neighbor = new Vector2I(cell.X + tx, cell.Y + ty);
								if (neighbor.X >= minX && neighbor.X <= maxX && neighbor.Y >= minY && neighbor.Y <= maxY)
								{
									thickenedCells.Add(neighbor);
								}
							}
						}
					}
				}
			}
		}

		foreach (var cell in thickenedCells)
		{
			if (tileInfoDict.TryGetValue(cell, out var info))
			{
				SetCell(cell, info.sourceId, info.atlasCoords, info.alternativeTile);
			}
			else
			{
				SetCell(cell, defaultSource, defaultAtlas, defaultAlt);
			}
		}
	}

	private List<Vector2I> FindAStarPath(Vector2I start, Vector2I goal, int minX, int maxX, int minY, int maxY, Dictionary<Vector2I, float> penalties, Random rng)
	{
		var openSet = new List<Vector2I> { start };
		var cameFrom = new Dictionary<Vector2I, Vector2I>();
		var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
		var fScore = new Dictionary<Vector2I, float> { [start] = Heuristic(start, goal) };

		Vector2I[] directions = { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) };

		while (openSet.Count > 0)
		{
			Vector2I current = openSet[0];
			float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
			for (int i = 1; i < openSet.Count; i++)
			{
				Vector2I candidate = openSet[i];
				float f = fScore.ContainsKey(candidate) ? fScore[candidate] : float.MaxValue;
				if (f < lowestF)
				{
					lowestF = f;
					current = candidate;
				}
			}

			if (current == goal)
			{
				List<Vector2I> path = new List<Vector2I>();
				Vector2I curr = goal;
				while (cameFrom.ContainsKey(curr))
				{
					path.Add(curr);
					curr = cameFrom[curr];
				}
				path.Add(start);
				path.Reverse();
				return path;
			}

			openSet.Remove(current);

			foreach (var dir in directions)
			{
				Vector2I neighbor = current + dir;
				if (neighbor.X < minX || neighbor.X > maxX || neighbor.Y < minY || neighbor.Y > maxY)
					continue;

				float penalty = penalties.ContainsKey(neighbor) ? penalties[neighbor] : 0f;
				float tentativeG = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue) + 1.0f + penalty + (float)(rng.NextDouble() * 0.5);

				if (tentativeG < (gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue))
				{
					cameFrom[neighbor] = current;
					gScore[neighbor] = tentativeG;
					fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

					if (!openSet.Contains(neighbor))
					{
						openSet.Add(neighbor);
					}
				}
			}
		}

		return null;
	}

	private float Heuristic(Vector2I a, Vector2I b)
	{
		return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
	}

	private List<Vector2I> GetDirectLine(Vector2I a, Vector2I b)
	{
		List<Vector2I> path = new List<Vector2I>();
		Vector2I curr = a;
		path.Add(curr);
		int safety = 0;
		while (curr != b && safety < 2000)
		{
			safety++;
			int dx = b.X - curr.X;
			int dy = b.Y - curr.Y;
			if (dx != 0) curr += new Vector2I(Math.Sign(dx), 0);
			else if (dy != 0) curr += new Vector2I(0, Math.Sign(dy));
			path.Add(curr);
		}
		return path;
	}
}