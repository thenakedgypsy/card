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

    [Export] private float enemyTurnDelay = 0.75f;
    [Export] private float actionSpacingDelay = 0.25f;

    private int _enemiesActing = 0;
    private int _summonsActing = 0;
    private int _enemiesStarted = 0;
    private int _enemiesScheduled = 0;
    private int _summonsStarted = 0;
    private int _summonsScheduled = 0;

    private Board _board;
    private AStarGrid2D _astarGrid;


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
		else
		{
			card.Generate("blockOfIce", Card.Location.Hand);
		}

        //else if (num < 69) //nice
		//{
		//	card.Generate("fireturret", Card.Location.Hand);
		//}      		
		//else if (num < 90)
		//{
		//	card.Generate("windturret", Card.Location.Hand);
		//}
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

        State = GameState.EnemyTurn;
        _playercore = GetParent().GetNode<Node2D>("Board/PlayerCore");

        var enemies = GetTree().GetNodesInGroup("Enemies");
        var enemyList = new List<Enemy>();
        _enemiesActing = 0;
        _enemiesStarted = 0;
        _enemiesScheduled = 0;

        foreach (Node node in enemies)
        {
            Enemy enemy = node as Enemy;
            if (enemy == null) continue;

            enemyList.Add(enemy);
        }

        _enemiesScheduled = enemyList.Count;

        if (_enemiesScheduled == 0)
        {
            BeginPlayerTurn();
            return;
        }

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

    public List<Vector2I> FindPath(Vector2I from, Vector2I to)
    {
        if (_astarGrid == null)
            return null;

        if (!_astarGrid.IsInBoundsv(from) || !_astarGrid.IsInBoundsv(to))
        {
            GD.PrintErr($"Pathfinding failed: Start {from} or End {to} is out of bounds. Grid Region: {_astarGrid.Region}");
            return null;
        }

        // 1. Remember if the destination is solid and temporarily disable it
        bool wasTargetSolid = _astarGrid.IsPointSolid(to);
        if (wasTargetSolid)
        {
            _astarGrid.SetPointSolid(to, false);
        }

        // 2. Calculate the path
        Godot.Collections.Array<Vector2I> pathArray = _astarGrid.GetIdPath(from, to);

        // 3. Restore the destination's solid state so other calculations aren't messed up
        if (wasTargetSolid)
        {
            _astarGrid.SetPointSolid(to, true);
        }

        if (pathArray.Count <= 1)
            return null;

        var path = new List<Vector2I>(pathArray);
        path.RemoveAt(0); // drop the starting cell, keep only steps to move through
        return path;
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
}