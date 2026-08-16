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
    private bool _isPostEnemySummonPhase = false;

    private Board _board;
    private AStarGrid2D _astarGrid;
    private HashSet<Vector2I> _occupiedEnemyCells = new HashSet<Vector2I>();

    private PackedScene enemyScene;
    private Overworld _overworld;
    public int Seed;
    private CardManager _cardManager;

    public override void _Ready()
    {
        Instance = this;
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
        enemyScene = GD.Load<PackedScene>("res://prefabs/Enemy.tscn");
        _board = GetTree().GetFirstNodeInGroup("Board") as Board;
        _hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;
        _board = GetTree().GetFirstNodeInGroup("Board") as Board;
        _overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;
        _cardManager = GetTree().GetFirstNodeInGroup("CardManager") as CardManager;
    }

    public void Setup(int numEnemies)
    {
        GD.Print("SETUP CALLED");
        Seed = _overworld.Seed * _overworld.roundNum;
        Random rng = new Random(Seed);
        _board.GenerateBoard(rng.Next());    
        BuildGrid();
        for(int i = 0; i < numEnemies; i++)
        {
            Enemy enemy = enemyScene.Instantiate<Enemy>();
            _board.AddChild(enemy);
        }
        PlaceEntities();
        BeginPlayerTurn(); 
    }


    public void BeginPlayerTurn()
    {
        State = GameState.PlayerTurn;
        _isPostEnemySummonPhase = false;
        _energyManager.RegenerateEnergy();
        energyPlayedThisTurn = 0;
        while (_hand.GetNumCards() < 5) _cardManager.DrawCard();
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
            CoreDef def = GetTree().GetFirstNodeInGroup("ActiveDef") as CoreDef;
            Overworld _overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;
            _cardManager.Reset();
			_overworld.InScene = false;
			def.QueueFree();
            BeginPlayerTurn();
            return;
        }

        foreach (var enemy in enemies)
        {
            OccupyCell(WorldToCell(enemy.GlobalPosition));
            enemy.ResetTurnState();
        }

        // Execute Phases
        await ExecuteEnemyTurnPhase(enemies);
        
        // Attack Phase
        foreach (Enemy enemy in enemies.Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHealth > 0))
        {
            await enemy.ExecuteAttackPhaseAsync(_playercore);
        }

        // Follow Up / Leftover Movement Phase
        var remainingEnemies = enemies.Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHealth > 0 && e.RemainingMovement > 0).ToList();
        if (remainingEnemies.Count > 0)
        {
            foreach (var enemy in remainingEnemies)
            {
                enemy.ResetTurnState(false);
            }

            RebakeNav();
            await ExecuteEnemyTurnPhase(remainingEnemies);
        }

        BeginPostEnemySummonTurn();
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

        if (_summonsActing <= 0 && _summonsStarted >= _summonsScheduled)
        {
            if (_isPostEnemySummonPhase)
            {
                _isPostEnemySummonPhase = false;
                BeginPlayerTurn();
            }
            else
            {
                BeginEnemyTurn();
            }
        }
    }

    public async void BeginSummonTurn()
    {
        _isPostEnemySummonPhase = false;
        State = GameState.SummonTurn;
        RebakeNav();
        await RunSummonPhase();
    }

    public async void BeginPostEnemySummonTurn()
    {
        _isPostEnemySummonPhase = true;
        State = GameState.SummonTurn;
        RebakeNav();
        await RunSummonPhase();
    }

    private async Task RunSummonPhase()
    {
        var summons = GetTree().GetNodesInGroup("Summons").Cast<Summon>().Where(s => s != null).ToList();

        _summonsActing = 0;
        _summonsStarted = 0;
        _summonsScheduled = summons.Count;

        if (_summonsScheduled == 0)
        {
            if (_isPostEnemySummonPhase)
            {
                BeginPlayerTurn();
            }
            else
            {
                BeginEnemyTurn();               
            }
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
        _astarGrid = new AStarGrid2D();
        _astarGrid.Region = new Rect2I(-12, -8, 25, 20); 
        _astarGrid.CellSize = new Vector2(64, 32);
        _astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        _astarGrid.Update();
    }
    
    public void RebakeNav()
    {
        if (_astarGrid == null) BuildGrid();
        else
        {
            _astarGrid.Region = _board.GetUsedRect();
            _astarGrid.Update();
        }

        Rect2I region = _astarGrid.Region;
        for (int x = region.Position.X; x < region.End.X; x++)
        {
            for (int y = region.Position.Y; y < region.End.Y; y++)
            {
                Vector2I cell = new Vector2I(x, y);
                _astarGrid.SetPointSolid(cell, false);
                if (!_board.IsCellWalkable(cell)) _astarGrid.SetPointSolid(cell, true);
            }
        }

        // Summons are solid by default
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

        // Strip summon solid status
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

        if (wasFromSolid) _astarGrid.SetPointSolid(from, true);
        if (wasToSolid) _astarGrid.SetPointSolid(to, true);

        // Restore summon solid status
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

    public void PlaceEntities()
{
    // 1. Get the PlayerCore and all Enemies
    _playercore = GetParent().GetNodeOrNull<Node2D>("Board/PlayerCore");
    var enemies = GetTree().GetNodesInGroup("Enemies");

    if (_playercore == null || enemies.Count == 0) return;

    // 2. Gather all walkable cells from the Board
    var usedCells = _board.GetUsedCells();
    List<Vector2I> walkableCells = new List<Vector2I>();
    
    foreach (Vector2I cell in usedCells)
    {
        if (_board.IsCellWalkable(cell))
        {
            walkableCells.Add(cell);
        }
    }

    if (walkableCells.Count < enemies.Count + 1)
    {
        GD.PrintErr("Not enough walkable cells to place the core and all enemies!");
        return;
    }

    // 3. Find the two farthest walkable cells (O(N^2) comparison)
    float maxDistance = -1f;
    Vector2I farthestA = Vector2I.Zero;
    Vector2I farthestB = Vector2I.Zero;

    for (int i = 0; i < walkableCells.Count; i++)
    {
        for (int j = i + 1; j < walkableCells.Count; j++)
        {
            // Using straight-line distance squared to find the absolute farthest visual points
            float dist = ((Vector2)walkableCells[i]).DistanceSquaredTo((Vector2)walkableCells[j]);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                farthestA = walkableCells[i];
                farthestB = walkableCells[j];
            }
        }
    }

    // 4. Assign Leftmost to Core, Rightmost to First Enemy
    Vector2I coreCell = farthestA.X < farthestB.X ? farthestA : farthestB;
    Vector2I firstEnemyCell = farthestA.X < farthestB.X ? farthestB : farthestA;

    // Remove placed cells from the available pool so they don't overlap
    walkableCells.Remove(coreCell);
    walkableCells.Remove(firstEnemyCell);

    // Move the Core and the First Enemy to their starting positions
    _playercore.GlobalPosition = CellToWorld(coreCell);
    
    Node2D firstEnemy = enemies[0] as Node2D;
    firstEnemy.GlobalPosition = CellToWorld(firstEnemyCell);

    // 5. Place the remaining enemies progressively closer
    // Sort the remaining cells by descending distance to the Core (farthest first)
    walkableCells.Sort((a, b) => 
    {
        float distA = ((Vector2)a).DistanceSquaredTo((Vector2)coreCell);
        float distB = ((Vector2)b).DistanceSquaredTo((Vector2)coreCell);
        return distB.CompareTo(distA);
    });

    // Place the rest of the enemies
    for (int i = 1; i < enemies.Count; i++)
    {
        Node2D enemy = enemies[i] as Node2D;
        Vector2I nextCell = walkableCells[i - 1]; // Skip first enemy, so index is i-1
        enemy.GlobalPosition = CellToWorld(nextCell);
    }

    // Finalize: Update Nav Grid now that entities are occupying their final spots
    RebakeNav();
}
}