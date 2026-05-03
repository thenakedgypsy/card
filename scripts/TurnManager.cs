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

    

    public GameState State {get; private set;}
    private int energyPlayedThisTurn;
    [Export]
    private int energyPlayLimit;

    private bool _expectingStateChange;
    private EnergyManager _energyManager;

    public override void _Ready()
    {
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
         BeginPlayerTurn();
    }

    public override void _Process(double delta)
    {

    }

    // =========================
    // SETUP
    // =========================

    public void BeginSetup()
    {       
        State = GameState.Setup;
    }

    public void EndSetup()
    {
    }

    // =========================
    // PLAYER TURN
    // =========================

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
        BeginPlayerTurn();
    }

    // =========================
    // HELPERS
    // =========================

    public int GetEnergyLimit()
    {
        return energyPlayLimit;
    }
    
}