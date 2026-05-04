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

    private int _enemiesActing = 0;

    public override void _Ready()
    {
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
        BeginPlayerTurn();
    }

    public void BeginPlayerTurn()
    {
        State = GameState.PlayerTurn;
        _energyManager.RegenerateEnergy();
        energyPlayedThisTurn = 0;
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