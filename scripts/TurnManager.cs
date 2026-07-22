using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    [Export] private float actionSpacingDelay = 0.02f;

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
        for (int i = 0; i < 5; i++) DrawCardTemp();
        BeginPlayerTurn(); 
    }

    public void DrawCardTemp()
    {
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/Card.tscn");
		Card card = scene.Instantiate() as Card;
		AddChild(card);

		Random random = new Random();
		int num = random.Next(101);
		
		if (num < 23) card.Generate("fireball", Card.Location.Hand);
        else if (num < 46) card.Generate("blockOfIce", Card.Location.Hand);
		else if (num < 69) card.Generate("windturret", Card.Location.Hand);
        else if (num < 85) card.Generate("fireturret", Card.Location.Hand);
    }

    public void BeginPlayerTurn()
    {
        State = GameState.PlayerTurn;
        _energyManager.RegenerateEnergy();
        energyPlayedThisTurn = 0;
        while (_hand.GetNumCards() < 5) DrawCardTemp();
    }

    public bool CanPlayEnergy() => energyPlayedThisTurn + 1 <= energyPlayLimit;
    public void PlayEnergy() => energyPlayedThisTurn++;
    public void EndPlayerTurn() => BeginSummonTurn();

    public async void BeginEnemyTurn()
    {
        await ToSignal(GetTree().CreateTimer(enemyTurnDelay), SceneTreeTimer.SignalName.Timeout);

        State = GameState.EnemyTurn;
        _playercore = GetParent().GetNode<Node2D>("Board/PlayerCore");
        RebakeNav();

        // Register initial positions
        _occupiedEnemyCells.Clear();
        var enemies = GetTree().GetNodesInGroup("Enemies").Cast<Enemy>().Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHealth > 0).ToList();
        
        if (enemies.Count == 0)
        {
            BeginPlayerTurn();
            return;
        }

        foreach (var enemy in enemies)
        {
            OccupyCell(WorldToCell(enemy.GlobalPosition));
            enemy.ResetTurnState();
        }

        // Execute movement planning and animation for the initial pass.
        await ExecuteEnemyTurnPhase(enemies);

        // Attack phase after movement, using the same turn order.
        foreach (Enemy enemy in enemies.Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHealth > 0))
        {
            await enemy.ExecuteAttackPhaseAsync(_playercore);
        }

        // Follow-up movement for enemies that still have movement left and have not attacked.
        var remainingEnemies = enemies
            .Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHealth > 0 && e.RemainingMovement > 0 && !e.HasAttackedThisTurn)
            .ToList();

        if (remainingEnemies.Count > 0)
        {
            RebakeNav();
            await ExecuteEnemyTurnPhase(remainingEnemies);
        }

        BeginPlayerTurn();
    }

    private async Task ExecuteEnemyTurnPhase(List<Enemy> enemies)
    {
        // 1. Evaluate distances considering summons as blockers
        bool allFullyBlocked = true;
        var distances = new Dictionary<Enemy, int>();

        foreach (var enemy in enemies)
        {
            int dist = enemy.GetRouteDistanceTo(_playercore, ignoreSummons: false);
            distances[enemy] = dist;
            if (dist != int.MaxValue) allFullyBlocked = false;
        }

        // 2. Re-order: if all are fully blocked, recalculate ignoring summons
        if (allFullyBlocked)
        {
            foreach (var enemy in enemies)
            {
                distances[enemy] = enemy.GetRouteDistanceTo(_playercore, ignoreSummons: true);
            }
        }

        enemies.Sort((a, b) => distances[a].CompareTo(distances[b]));

        // 3. Begin calculating positions sequentially
        foreach (var enemy in enemies)
        {
            enemy.PlanMove(_playercore);
        }

        // 4. Async Move to positions concurrently
        var moveTasks = new List<Task>();
        for (int i = 0; i < enemies.Count; i++)
        {
            float staggerDelay = i * 0.10f;
            moveTasks.Add(enemies[i].AnimateMoveAsync(staggerDelay));
        }
        
        await Task.WhenAll(moveTasks);
    }

    private void OnSummonFinishedTurn(Summon summon)
    {
        if (summon != null) summon.TurnFinished -= OnSummonFinishedTurn;
        _summonsActing--;
        if (_summonsActing <= 0 && _summonsStarted >= _summonsScheduled) BeginEnemyTurn();
    }

    public async void BeginSummonTurn()
    {
        State = GameState.SummonTurn;
        RebakeNav();
        var summons = GetTree().GetNodesInGroup("Summons").Cast<Summon>().Where(s => s != null).ToList();
        
        _summonsActing = 0;
        _summonsStarted = 0;
        _summonsScheduled = summons.Count;

        if (_summonsScheduled == 0)
        {
            BeginEnemyTurn();
            return;
        }

        for (int i = 0; i < summons.Count; i++)
        {
            if (i > 0) await ToSignal(GetTree().CreateTimer(actionSpacingDelay), SceneTreeTimer.SignalName.Timeout);
            
            Summon summon = summons[i];
            _summonsActing++;
            _summonsStarted++;
            summon.TurnFinished += OnSummonFinishedTurn;
            summon.TakeTurn();
        }
    }

    public int GetEnergyLimit() => energyPlayLimit;

    private void BuildGrid()
    {
        _board = GetTree().CurrentScene.GetNode<Board>("Board") as Board;
        _astarGrid = new AStarGrid2D();
        _astarGrid.Region = new Rect2I(-12, -8, 25, 20); 
        _astarGrid.CellSize = new Vector2(64, 32);
        _astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        _astarGrid.Update();
    }
    
    public void RebakeNav()
    {
        if (_board == null)
        {
            BuildGrid();
            if (_board == null || _astarGrid == null) return;
        }

        _astarGrid = new AStarGrid2D();
        _astarGrid.Region = _board.GetUsedRect();
        _astarGrid.CellSize = new Vector2(64, 32);
        _astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        _astarGrid.Update();

        Rect2I region = _astarGrid.Region;
        for (int x = region.Position.X; x < region.End.X; x++)
        {
            for (int y = region.Position.Y; y < region.End.Y; y++)
            {
                Vector2I cell = new Vector2I(x, y);
                if (!_board.IsCellWalkable(cell)) _astarGrid.SetPointSolid(cell, true);
            }
        }

        var summons = GetTree().GetNodesInGroup("Summons");
        foreach (Node node in summons)
        {
            if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
            {
                Vector2I cell = WorldToCell(summon.GlobalPosition);
                if (_astarGrid.IsInBoundsv(cell)) _astarGrid.SetPointSolid(cell, true);
            }
        }
    }

    public Vector2I WorldToCell(Vector2 worldPosition) => _board.LocalToMap(_board.ToLocal(worldPosition));
    public Vector2 CellToWorld(Vector2I cell) => _board.ToGlobal(_board.MapToLocal(cell));
    public int TileDistance(Vector2I a, Vector2I b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    public bool IsSolidCell(Vector2I cell) => _astarGrid.IsInBoundsv(cell) && _astarGrid.IsPointSolid(cell);

    public List<Vector2I> FindPath(Vector2I from, Vector2I to)
    {
        if (_astarGrid == null || !_astarGrid.IsInBoundsv(from) || !_astarGrid.IsInBoundsv(to)) return null;

        bool wasFromSolid = _astarGrid.IsPointSolid(from);
        bool wasToSolid = _astarGrid.IsPointSolid(to);
        if (wasFromSolid) _astarGrid.SetPointSolid(from, false);
        if (wasToSolid) _astarGrid.SetPointSolid(to, false);

        Godot.Collections.Array<Vector2I> pathArray = _astarGrid.GetIdPath(from, to);

        if (wasFromSolid) _astarGrid.SetPointSolid(from, true);
        if (wasToSolid) _astarGrid.SetPointSolid(to, true);

        if (pathArray.Count <= 1) return null;
        var path = new List<Vector2I>(pathArray);
        path.RemoveAt(0); 
        return path;
    }

    public List<Vector2I> FindPathIgnoringSummons(Vector2I from, Vector2I to)
    {
        if (_astarGrid == null || !_astarGrid.IsInBoundsv(from) || !_astarGrid.IsInBoundsv(to)) return null;

        // Temporarily ignore summons and already-reserved enemy cells so the fallback route can be calculated.
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

        foreach (Vector2I cell in _occupiedEnemyCells)
        {
            if (_astarGrid.IsInBoundsv(cell))
            {
                _astarGrid.SetPointSolid(cell, false);
                modifiedCells.Add(cell);
            }
        }

        bool wasFromSolid = _astarGrid.IsPointSolid(from);
        bool wasToSolid = _astarGrid.IsPointSolid(to);
        if (wasFromSolid) _astarGrid.SetPointSolid(from, false);
        if (wasToSolid) _astarGrid.SetPointSolid(to, false);

        Godot.Collections.Array<Vector2I> pathArray = _astarGrid.GetIdPath(from, to);

        if (wasFromSolid) _astarGrid.SetPointSolid(from, true);
        if (wasToSolid) _astarGrid.SetPointSolid(to, true);

        foreach (Vector2I cell in modifiedCells) _astarGrid.SetPointSolid(cell, true);

        if (pathArray.Count <= 1) return null;
        var path = new List<Vector2I>(pathArray);
        path.RemoveAt(0);
        return path;
    }

    public bool IsCellOccupiedBySummon(Vector2I targetCell)
    {
        var summons = GetTree().GetNodesInGroup("Summons");
        foreach (Node node in summons)
        {
            if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
            {
                if (WorldToCell(summon.GlobalPosition) == targetCell) return true;
            }
        }
        return false;
    }

    public void FreeCell(Vector2I cell) => _occupiedEnemyCells.Remove(cell);
    public void OccupyCell(Vector2I cell) => _occupiedEnemyCells.Add(cell);
    public bool IsEnemyOccupied(Vector2I cell) => _occupiedEnemyCells.Contains(cell);

    public Node2D GetFirstBlockingSummon(Vector2I from, Vector2I to)
    {
        var idealPath = FindPathIgnoringSummons(from, to);
        if (idealPath == null) return null;

        var summons = GetTree().GetNodesInGroup("Summons");
        foreach (Vector2I cell in idealPath)
        {
            foreach (Node node in summons)
            {
                if (node is Node2D summon && GodotObject.IsInstanceValid(summon))
                {
                    if (WorldToCell(summon.GlobalPosition) == cell) return summon;
                }
            }
        }
        return null;
    }

    public int GetPathLengthToTarget(Vector2I from, Vector2I to, bool ignoreSummons)
    {
        List<Vector2I> path = ignoreSummons ? FindPathIgnoringSummons(from, to) : FindPath(from, to);
        
        if (path != null && path.Count > 0)
        {
            return path.Count;
        }
        
        return int.MaxValue; 
    }
}