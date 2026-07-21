using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody2D, IHealth
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [ExportGroup("Hop Settings")]
    [Export] public float StepDuration = 0.16f; // Time spent hopping to next tile
    [Export] public float RestDuration = 0.32f; // Delay resting on each space
    [Export] public float HopHeight = 8f;      // How high the piece lifts up (in pixels)

    [ExportGroup("Main Settings")]
    [Export] public int MoveDistance = 4; // tiles per turn
    [Export] public int Health = 10;
    public int CurrentHealth;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;
    [Export] public int AttackRange = 1; // tiles
    [Export] public string SummonGroupName = "Summons";

    private Vector2I? _reservedCell = null;

    private Node2D _target;
    private bool _isTakingTurn = false;
    private TurnManager _turnManager;
    private bool _playerPathBlocked = false;
    private Sprite2D _sprite;
    private bool _isHovered;

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;

        CurrentHealth = Health;
    }

    public void TakeTurn(Node2D playerCore)
    {
        _isTakingTurn = true;
        _target = null;
        _playerPathBlocked = false;

        GD.Print($"[{Name}] TakeTurn START");

        // 1. Attack player immediately if already in range
        if (IsInRange(playerCore))
        {
            Attack(playerCore);
            EndTurn();
            return;
        }

        // 2. Check if player is reachable
        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition);

        List<Vector2I> pathToPlayer = _turnManager.FindPath(myCell, playerCell);
        _playerPathBlocked = pathToPlayer == null || pathToPlayer.Count == 0;

        // 3. If blocked, move toward the summon blocking the route
        if (_playerPathBlocked || AttacksSummons)
        {
            Node2D targetSummon = null;

            if (_playerPathBlocked)
            {
                targetSummon = _turnManager.GetFirstBlockingSummon(myCell, playerCell);
            }
            
            if (targetSummon == null)
            {
                targetSummon = GetNearestSummon();
            }

            if (targetSummon != null)
            {
                if (IsInRange(targetSummon))
                {
                    Attack(targetSummon);
                    EndTurn();
                    return;
                }

                if (TrySetTarget(targetSummon))
                    return;
            }
        }

        // 4. Otherwise move toward player
        if (!_playerPathBlocked)
        {
            _target = playerCore;
            BeginMoveAlong(pathToPlayer);
            return;
        }

        EndTurn();
    }

    private bool TrySetTarget(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);

        List<Vector2I> path = _turnManager.FindPath(myCell, targetCell);

        if (path != null && path.Count > 0)
        {
            _target = target;
            BeginMoveAlong(path);
            return true;
        }

        return false;
    }

    // --- BOARD GAME PIECE TWEEN MOVEMENT ---

private async void BeginMoveAlong(List<Vector2I> path)
{
    Vector2I startCell = _turnManager.WorldToCell(GlobalPosition);
    int maxSteps = Mathf.Min(path.Count, MoveDistance);
    int stepsToTake = 0;

    // 1. Check how many steps along the path are clear
    for (int i = 0; i < maxSteps; i++)
    {
        Vector2I checkCell = path[i];

        // Stop if entering attack range
        if (_target != null && IsInRangeOfCell(checkCell, _target))
        {
            stepsToTake = i + 1;
            break;
        }

        // Stop if tile is occupied by another enemy
        if (_turnManager.IsEnemyOccupied(checkCell))
        {
            break;
        }

        stepsToTake = i + 1;
    }

    // 2. If blocked on turn start, remain in startCell
    if (stepsToTake == 0)
    {
        HandleEndOfMovement();
        return;
    }

    Vector2I destinationCell = path[stepsToTake - 1];

    // 3. Immediately free startCell for trailing enemies & claim destinationCell
    _turnManager.MoveEnemyCell(startCell, destinationCell);
    _reservedCell = destinationCell;

    // 4. Perform visual hopping
    for (int i = 0; i < stepsToTake; i++)
    {
        if (CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Vector2 targetWorldPos = _turnManager.CellToWorld(path[i]);
        await MoveToTileAsync(targetWorldPos);
    }

    _reservedCell = null;
    HandleEndOfMovement();
}

    private bool IsInRangeOfCell(Vector2I cell, Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;

        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);
        return _turnManager.TileDistance(cell, targetCell) <= AttackRange;
    }

    private async Task MoveToTileAsync(Vector2 targetPos)
    {
        // Tween 1: Move Root Node to target cell location
        Tween moveTween = CreateTween();
        moveTween.TweenProperty(this, "global_position", targetPos, StepDuration)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.InOut);

        // Tween 2: Lift Sprite Up and Put Down (Parallel Visual Effect)
        Tween hopTween = CreateTween();
        
        // Lift up (Peak halfway through movement)
        hopTween.TweenProperty(_sprite, "position:y", -HopHeight, StepDuration * 0.45f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        
        // Put back down
        hopTween.TweenProperty(_sprite, "position:y", 0f, StepDuration * 0.55f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

        // Wait for physical position and hop to finish
        await ToSignal(moveTween, Tween.SignalName.Finished);
        await ToSignal(hopTween, Tween.SignalName.Finished);

        // Reset local Y offset to ensure perfect landing alignment
        Vector2 spriteSize = _sprite.Texture.GetSize() * _sprite.Scale;
        _sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));

        // Brief pause resting on space before moving to next space
        if (RestDuration > 0)
        {
            await ToSignal(GetTree().CreateTimer(RestDuration), SceneTreeTimer.SignalName.Timeout);
        }
    }

    private void HandleEndOfMovement()
    {
        if (CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Velocity = Vector2.Zero;

        if (_target != null && IsInRange(_target))
        {
            Attack(_target);
            EndTurn();
            return;
        }

        EndTurn();
    }

    // --- COMBAT & UTILITY ---

    public int GetRouteDistanceTo(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return int.MaxValue;
    
        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);
    
        return _turnManager.GetPathLengthToTarget(myCell, targetCell);
    }

    private bool IsInRange(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);

        return _turnManager.TileDistance(myCell, targetCell) <= AttackRange;
    }

    private void Attack(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return;

        GD.Print($"[{Name}] ATTACK → '{target.Name}'");

        FlashYellow();

        if (target.HasMethod("TakeDamage"))
            target.Call("TakeDamage", AttackDamage);
    }

    public async void FlashYellow()
    {
        Color original = _sprite.SelfModulate;
        Tween tween = CreateTween();
        tween.TweenProperty(_sprite, "self_modulate", Colors.Orange, 0.25f);
        tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public async void FlashRed()
    {
        Color original = _sprite.SelfModulate;
        Tween tween = CreateTween();
        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.25f);
        tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private Node2D GetNearestSummon()
    {
        var summons = GetTree().GetNodesInGroup(SummonGroupName);
        Node2D nearest = null;
        float minDist = float.MaxValue;

        foreach (Node node in summons)
        {
            if (!GodotObject.IsInstanceValid(node))
                continue;

            if (node is Node2D summon)
            {
                float dist = GlobalPosition.DistanceTo(summon.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = summon;
                }
            }
        }

        return nearest;
    }

    private void EndTurn()
    {
        _isTakingTurn = false;
        Velocity = Vector2.Zero;
        _target = null;

        EmitSignal(SignalName.TurnFinished, this);
    }

    public float GetMaxHealth() => Health;
    public float GetCurrentHealth() => CurrentHealth;

    public void TakeDamage(int value)
    {
        CurrentHealth -= value;
        GD.Print($"Enemy {Name} takes {value} damage");

        FlashRed();

        if (CurrentHealth <= 0)
        {
            GD.Print($"Enemy {Name} IS DESTROYED");

            // Free whichever cell this enemy was holding/heading toward
            Vector2I cellToFree = _reservedCell ?? _turnManager.WorldToCell(GlobalPosition);
            _turnManager.FreeCell(cellToFree);

            SetProcess(false);
            SetPhysicsProcess(false);
            SetDeferred("monitoring", false);
            CallDeferred(Node.MethodName.QueueFree);
        }
    }

    public void MouseOver()
    {
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        if (mouse != null)
            mouse.SetHoveredEnemy(this);

        _sprite.SelfModulate = Colors.Yellow;
    }

    public void MouseOff()
    {
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        if (mouse != null)
            mouse.SetHoveredEnemy(null);

        _sprite.SelfModulate = Colors.White;
    }

    private void _on_area_2d_mouse_entered() => MouseOver();
    private void _on_area_2d_mouse_exited() => MouseOff();

    public bool IsHovered() => _isHovered;
}