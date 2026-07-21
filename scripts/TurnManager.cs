using System;
using System.Collections.Generic;
using Godot;

public partial class TurnManager : Node
{
    public enum GameState
    {
        Setup,
        PlayerTurn,
        SummonTurn,
        EnemyTurn,
        CleanupStep     
    }

    public static TurnManager Instance { get; private set; }

    public GameState State { get; private set; }

    private int energyPlayedThisTurn;
    [Export] private int energyPlayLimit;

    private EnergyManager _energyManager;
    private Node2D _playercore;
    private Hand _hand;

    [Export] private float enemyTurnDelay = 00.001f;
    [Export] private float actionSpacingDelay = 0.5f;

    private int _enemiesActing = 0;
    private int _summonsActing = 0;
    private int _enemiesStarted = 0;
    private int _enemiesScheduled = 0;
    private int _summonsStarted = 0;
    private int _summonsScheduled = 0;

    private Board _board;
    private AStarGrid2D _astarGrid;
    private HashSet<Vector2I> _occupiedEnemyCells = new HashSet<Vector2I>();


    public override void _Ready()
    {
        Instance = this;

        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
        _hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;

        BuildGrid();

        Setup();
    }

    public void Setup()
    {
        for (int i = 0; i < 5; i++)
        {
            DrawCardTemp();
        }
       BeginPlayerTurn(); 
    }

    public void DrawCardTemp()
    {
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/Card.tscn");
		Card card = scene.Instantiate() as Card;

		AddChild(card);

		Random random = new Random();
		int num = random.Next(101);
		
		if (num < 23)
		{
			card.Generate("fireball", Card.Location.Hand);
		}
        else if (num < 46)
		{
			card.Generate("blockOfIce", Card.Location.Hand);
        }    		
		else if (num < 69)
		{
			card.Generate("windturret", Card.Location.Hand);
		}
        else if (num < 85)
		{
			card.Generate("fireturret", Card.Location.Hand);
		}
        //else if (num < 101)
        //{
        //    card.Generate("energy_neutral", Card.Location.Hand);
        //}
    }



    public void BeginPlayerTurn()
    {
        State = GameState.PlayerTurn;
        _energyManager.RegenerateEnergy();
        energyPlayedThisTurn = 0;
        while (_hand.GetNumCards() < 5)
        {
            DrawCardTemp();
        }

    }

    public bool CanPlayEnergy()
    {
        return energyPlayedThisTurn + 1 <= energyPlayLimit;
    }

    public void PlayEnergy()
    {
        energyPlayedThisTurn++;
    }

    public void EndPlayerTurn()
    {
        BeginSummonTurn();
    }

    public async void BeginEnemyTurn()
    {
        await ToSignal(GetTree().CreateTimer(enemyTurnDelay), SceneTreeTimer.SignalName.Timeout);

        RebakeNav();

        // Register initial enemy positions at start of turn
        _occupiedEnemyCells.Clear();
        var enemies = GetTree().GetNodesInGroup("Enemies");
        var enemyList = new List<Enemy>();

        foreach (Node node in enemies)
        {
            if (node is Enemy enemy && GodotObject.IsInstanceValid(enemy) && enemy.CurrentHealth > 0)
            {
                enemyList.Add(enemy);
                OccupyCell(WorldToCell(enemy.GlobalPosition));
            }
        }

        State = GameState.EnemyTurn;
        _playercore = GetParent().GetNode<Node2D>("Board/PlayerCore");

        _enemiesActing = 0;
        _enemiesStarted = 0;
        _enemiesScheduled = enemyList.Count;

        if (_enemiesScheduled == 0)
        {
            BeginPlayerTurn();
            return;
        }

        // Sort by distance so closest enemies move first
        enemyList.Sort((a, b) => a.GetRouteDistanceTo(_playercore).CompareTo(b.GetRouteDistanceTo(_playercore)));

        for (int i = 0; i < enemyList.Count; i++)
        {
            if (i > 0)
            {
                await ToSignal(GetTree().CreateTimer(actionSpacingDelay), SceneTreeTimer.SignalName.Timeout);
            }

            Enemy enemy = enemyList[i];
            _enemiesActing++;
            _enemiesStarted++;
            enemy.TurnFinished += OnEnemyFinishedTurn;
            enemy.TakeTurn(_playercore);
        }
    }

    private void OnEnemyFinishedTurn(Enemy enemy)
    {
        if (enemy != null)
        {
            enemy.TurnFinished -= OnEnemyFinishedTurn;
        }

        _enemiesActing--;

        if (_enemiesActing <= 0 && _enemiesStarted >= _enemiesScheduled)
        {
            BeginPlayerTurn();
        }
    }

    private void OnSummonFinishedTurn(Summon summon)
    {
        if (summon != null)
        {
            summon.TurnFinished -= OnSummonFinishedTurn;
        }

        _summonsActing--;

        if (_summonsActing <= 0 && _summonsStarted >= _summonsScheduled)
        {
            BeginEnemyTurn();
        }
    }

    public async void BeginSummonTurn()
    {
        State = GameState.SummonTurn;
        RebakeNav();
        var summons = GetTree().GetNodesInGroup("Summons");
        var summonList = new List<Summon>();
        _summonsActing = 0;
        _summonsStarted = 0;
        _summonsScheduled = 0;

        foreach (Node node in summons)
        {
            Summon summon = node as Summon;
            if (summon == null)
                continue;

            summonList.Add(summon);
        }

        _summonsScheduled = summonList.Count;

        if (_summonsScheduled == 0)
        {
            BeginEnemyTurn();
            return;
        }

        for (int i = 0; i < summonList.Count; i++)
        {
            if (i > 0)
            {
                await ToSignal(GetTree().CreateTimer(actionSpacingDelay), SceneTreeTimer.SignalName.Timeout);
            }

            Summon summon = summonList[i];
            _summonsActing++;
            _summonsStarted++;
            summon.TurnFinished += OnSummonFinishedTurn;
            summon.TakeTurn();
        }
    }

    public int GetEnergyLimit()
    {
        return energyPlayLimit;
    }

    private void BuildGrid()
    {
        _board = GetTree().CurrentScene.GetNode<Board>("Board") as Board;

        _astarGrid = new AStarGrid2D();
        // Adjust the starting point to -12, and increase the size to cover the span.
        // If your range is -12 to 6 (X), the width is 18 (6 - (-12)).
        // Adjust these numbers until they encompass your entire map.
        _astarGrid.Region = new Rect2I(-12, -8, 25, 20); 
        _astarGrid.CellSize = new Vector2(64, 32);
        _astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        _astarGrid.Update();
    }
    
    public void RebakeNav()
    {
        if (_astarGrid == null)
        {
            BuildGrid();
        }
        else
        {
            _astarGrid.Region = _board.GetUsedRect();
            _astarGrid.Update();
        }

        // Iterate through all cells in the grid region to check walkability
        Rect2I region = _astarGrid.Region;
        for (int x = region.Position.X; x < region.End.X; x++)
        {
            for (int y = region.Position.Y; y < region.End.Y; y++)
            {
                Vector2I cell = new Vector2I(x, y);

                // If the tile is NOT walkable on the board, mark it solid in the AStarGrid
                if (!_board.IsCellWalkable(cell))
                {
                    _astarGrid.SetPointSolid(cell, true);
                }
            }
        }

        // Mark summons as solid
        var summons = GetTree().GetNodesInGroup("Summons");
        foreach (Node node in summons)
        {
            if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
            {
                Vector2I cell = WorldToCell(summon.GlobalPosition);

                if (_astarGrid.IsInBoundsv(cell))
                    _astarGrid.SetPointSolid(cell, true);
            }
        }

        // --- NEW: Mark enemies as solid ---
        //var enemies = GetTree().GetNodesInGroup("Enemies");
        //foreach (Node node in enemies)
        //{
        //    if (node is Node2D enemy && GodotObject.IsInstanceValid(enemy))
        //    {
        //        Vector2I cell = WorldToCell(enemy.GlobalPosition);
//
        //        if (_astarGrid.IsInBoundsv(cell))
        //            _astarGrid.SetPointSolid(cell, true);
        //    }
        //}
    }

    public Vector2I WorldToCell(Vector2 worldPosition)
    {
        return _board.LocalToMap(_board.ToLocal(worldPosition));
    }

    public Vector2 CellToWorld(Vector2I cell)
    {
        return _board.ToGlobal(_board.MapToLocal(cell));
    }

    public int TileDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    public bool IsSolidCell(Vector2I cell)
    {
        if (_astarGrid.IsInBoundsv(cell))
        {
            return _astarGrid.IsPointSolid(cell);
        }
        return true;        
    }

    public List<Vector2I> FindPath(Vector2I from, Vector2I to)
    {
        if (_astarGrid == null) return null;

        if (!_astarGrid.IsInBoundsv(from) || !_astarGrid.IsInBoundsv(to))
        {
            GD.PrintErr($"Pathfinding failed: Start {from} or End {to} is out of bounds.");
            return null;
        }

        // Un-solidify start and target points temporarily so AStar can calculate
        bool wasFromSolid = _astarGrid.IsPointSolid(from);
        bool wasToSolid = _astarGrid.IsPointSolid(to);
        if (wasFromSolid) _astarGrid.SetPointSolid(from, false);
        if (wasToSolid) _astarGrid.SetPointSolid(to, false);

        // --- PASS 1: OPTIMAL PATH (Try routing around stationary enemies) ---
        var modifiedCells = new List<Vector2I>();
        foreach (Vector2I enemyCell in _occupiedEnemyCells)
        {
            if (enemyCell != from && enemyCell != to && _astarGrid.IsInBoundsv(enemyCell))
            {
                if (!_astarGrid.IsPointSolid(enemyCell))
                {
                    _astarGrid.SetPointSolid(enemyCell, true);
                    modifiedCells.Add(enemyCell);
                }
            }
        }

        Godot.Collections.Array<Vector2I> pathArray = _astarGrid.GetIdPath(from, to);

        // Revert only the cells we temporarily marked solid
        foreach (Vector2I cell in modifiedCells)
        {
            _astarGrid.SetPointSolid(cell, false);
        }

        // --- PASS 2: FALLBACK PATH (If route is blocked by enemies in a hallway/choke) ---
        if (pathArray.Count <= 1)
        {
            pathArray = _astarGrid.GetIdPath(from, to);
        }

        // Restore start and target points
        if (wasFromSolid) _astarGrid.SetPointSolid(from, true);
        if (wasToSolid) _astarGrid.SetPointSolid(to, true);

        if (pathArray.Count <= 1)
            return null;

        var path = new List<Vector2I>(pathArray);
        path.RemoveAt(0); // Remove start position
        return path;
    }

    // --- NEW: Helper method to update grid solids dynamically ---
    public void SetCellSolid(Vector2I cell, bool isSolid)
    {
        if (_astarGrid != null && _astarGrid.IsInBoundsv(cell))
        {
            _astarGrid.SetPointSolid(cell, isSolid);
        }
    }

    public void MoveEnemyCell(Vector2I oldCell, Vector2I newCell)
    {
        FreeCell(oldCell);
        OccupyCell(newCell);
    }

    public void FreeCell(Vector2I cell)
    {
        _occupiedEnemyCells.Remove(cell);
    }

    public void OccupyCell(Vector2I cell)
    {
        _occupiedEnemyCells.Add(cell);
    }

    public void ClearCell(Vector2 worldPosition)
    {
        if (_astarGrid == null) return;
        
        Vector2I cell = WorldToCell(worldPosition);
        
        if (_astarGrid.IsInBoundsv(cell))
        {
            _astarGrid.SetPointSolid(cell, false);
        }
    }

    public Node2D GetFirstBlockingSummon(Vector2I from, Vector2I to)
    {
        if (_astarGrid == null) return null;

        var summons = GetTree().GetNodesInGroup("Summons");
        
        // 1. Temporarily make summons walkable to find the ideal route
        foreach (Node node in summons)
        {
            if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
            {
                Vector2I cell = WorldToCell(summon.GlobalPosition);
                if (_astarGrid.IsInBoundsv(cell))
                    _astarGrid.SetPointSolid(cell, false);
            }
        }

        // 2. Get the path ignoring summons
        Godot.Collections.Array<Vector2I> idealPath = _astarGrid.GetIdPath(from, to);

        // 3. Restore summon collisions
        foreach (Node node in summons)
        {
            if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
            {
                Vector2I cell = WorldToCell(summon.GlobalPosition);
                if (_astarGrid.IsInBoundsv(cell))
                    _astarGrid.SetPointSolid(cell, true);
            }
        }

        // 4. Find the first summon that sits on this ideal path
        foreach (Vector2I cell in idealPath)
        {
            foreach (Node node in summons)
            {
                if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
                {
                    if (WorldToCell(summon.GlobalPosition) == cell)
                    {
                        return summon;
                    }
                }
            }
        }

        return null;
    }

    public bool IsEnemyOccupied(Vector2I cell)
    {
        return _occupiedEnemyCells.Contains(cell);
    }

    public void RegisterEnemyPosition(Vector2I oldCell, Vector2I newCell)
    {
        _occupiedEnemyCells.Remove(oldCell);
        SetCellSolid(oldCell, false);
        _occupiedEnemyCells.Add(newCell);
        SetCellSolid(newCell, true);
    }

    public void UnregisterEnemyPosition(Vector2I cell)
    {
        _occupiedEnemyCells.Remove(cell);
        SetCellSolid(cell, false);
    }

    public int GetPathLengthToTarget(Vector2I from, Vector2I to)
{
    if (_astarGrid == null)
        return int.MaxValue;

    if (!_astarGrid.IsInBoundsv(from) || !_astarGrid.IsInBoundsv(to))
        return int.MaxValue;

    if (from == to)
        return 0;

    // Temporarily un-solidify summons and start/end points so we can measure path length along grid terrain
    var summons = GetTree().GetNodesInGroup("Summons");
    var modifiedCells = new List<Vector2I>();

    foreach (Node node in summons)
    {
        if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
        {
            Vector2I cell = WorldToCell(summon.GlobalPosition);
            if (_astarGrid.IsInBoundsv(cell) && _astarGrid.IsPointSolid(cell))
            {
                _astarGrid.SetPointSolid(cell, false);
                modifiedCells.Add(cell);
            }
        }
    }

    bool wasFromSolid = _astarGrid.IsPointSolid(from);
    bool wasToSolid = _astarGrid.IsPointSolid(to);

    if (wasFromSolid) _astarGrid.SetPointSolid(from, false);
    if (wasToSolid) _astarGrid.SetPointSolid(to, false);

    Godot.Collections.Array<Vector2I> pathArray = _astarGrid.GetIdPath(from, to);

    // Restore solid states
    if (wasFromSolid) _astarGrid.SetPointSolid(from, true);
    if (wasToSolid) _astarGrid.SetPointSolid(to, true);

    foreach (Vector2I cell in modifiedCells)
    {
        _astarGrid.SetPointSolid(cell, true);
    }

    // pathArray includes start position, so (Count - 1) gives the number of tile steps
    if (pathArray != null && pathArray.Count > 1)
    {
        return pathArray.Count - 1;
    }

    // Fallback to Manhattan tile distance if terrain completely blocks path
    return TileDistance(from, to);
}
}