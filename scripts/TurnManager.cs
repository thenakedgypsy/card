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

    public GameState State { get; private set; }

    private int energyPlayedThisTurn;
    [Export] private int energyPlayLimit;

    private EnergyManager _energyManager;
    private Node2D _playercore;
    private Hand _hand;

    [Export] private float enemyTurnDelay = 1f;
    [Export] private float actionSpacingDelay = 0.3f;

    private int _enemiesActing = 0;
    private int _summonsActing = 0;
    private int _enemiesStarted = 0;
    private int _enemiesScheduled = 0;
    private int _summonsStarted = 0;
    private int _summonsScheduled = 0;
    

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
		int num = random.Next(101);
		
		if (num < 23)
		{
			card.Generate("fireball", Card.Location.Hand);
		}
		else if (num < 46)
		{
			card.Generate("blockOfIce", Card.Location.Hand);
		}
        else if (num < 69) //nice
		{
			card.Generate("fireturret", Card.Location.Hand);
		}      		
		else if (num < 90)
		{
			card.Generate("windturret", Card.Location.Hand);
		}
        else if (num < 101)
        {
            card.Generate("energy_neutral", Card.Location.Hand);
        }
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
        _playercore = GetParent().GetNode<Node2D>("Board/Nav/PlayerCore");

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

    public void RebakeNav()
    {
        NavigationRegion2D nav = GetTree().CurrentScene.GetNode<NavigationRegion2D>("Board/Nav");
        if (!nav.IsBaking())
        {
        nav.BakeNavigationPolygon();
        }
    }
}