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

    

    public GameState gameState;

    public override void _Ready()
    {
        
    }

    public override void _Process(double delta)
    {
        
    }
    public void BeginSetup()
    {
        gameState = GameState.Setup;
    }
    public void UpdateSetup()
    {
        
    }

    public void BeginPlayerTurn()
    {
        gameState = GameState.PlayerTurn;
    }

    public void UpdatePlayerTurn()
    {
        
    }


    
}