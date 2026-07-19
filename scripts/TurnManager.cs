using System;
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

    public GameState State { get; private set; }

    private int energyPlayedThisTurn;
    [Export] private int energyPlayLimit;

    private EnergyManager _energyManager;
    private Node2D _playercore;
    private Hand _hand;

    private int _enemiesActing = 0;

    public override void _Ready()
    {
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
        _hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;

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
		int num = random.Next(2);
		
		if (num == 0)
		{
			card.Generate("fireball", Card.Location.Hand);
		}
		else
		{
			card.Generate("blockOfIce", Card.Location.Hand);
		}
    }

    public void BeginPlayerTurn()
    {
        State = GameState.PlayerTurn;
        _energyManager.RegenerateEnergy();
        energyPlayedThisTurn = 0;
        DrawCardTemp();

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
        BeginEnemyTurn();
    }

    public void BeginEnemyTurn()
    {
        State = GameState.EnemyTurn;
        _playercore = GetParent().GetNode<Node2D>("Board/Nav/PlayerCore");

        var enemies = GetTree().GetNodesInGroup("Enemies");
        _enemiesActing = 0;

        foreach (Node node in enemies)
        {
            Enemy enemy = node as Enemy;
            if (enemy == null) continue;

            _enemiesActing++;

            enemy.TurnFinished += OnEnemyFinishedTurn;
            enemy.TakeTurn(_playercore);
        }

        if (_enemiesActing == 0)
        {
            BeginPlayerTurn();
        }
    }

    private void OnEnemyFinishedTurn(Enemy enemy)
    {
        if (enemy != null)
        {
            enemy.TurnFinished -= OnEnemyFinishedTurn;
        }

        _enemiesActing--;

        if (_enemiesActing <= 0)
        {
            BeginPlayerTurn();
        }
    }

    public int GetEnergyLimit()
    {
        return energyPlayLimit;
    }
}