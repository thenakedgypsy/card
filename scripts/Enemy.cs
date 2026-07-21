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

    public int RemainingMovement { get; private set; }
    public bool HasAttackedThisTurn { get; private set; }

    private Vector2I? _reservedCell = null;
    private Node2D _target;
    private TurnManager _turnManager;
    private bool _playerPathBlocked = false;
    private Sprite2D _sprite;
    private bool _isHovered;

    private List<Vector2I> _plannedPath = new List<Vector2I>();

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;

        CurrentHealth = Health;
    }

    public void ResetTurnState()
    {
        RemainingMovement = MoveDistance;
        HasAttackedThisTurn = false;
        _plannedPath.Clear();
    }

    // --- STEP 1: INSTANT CALCULATIONS & GRID RESERVATIONS ---

    public void PlanMove(Node2D playerCore)
    {
        _plannedPath.Clear();

        if (RemainingMovement <= 0 || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition);

        // Target selection according to the 3 explicit conditions
        Node2D target = null;

        if (AttacksSummons)
        {
            // Condition 3: Always target nearest summon
            target = GetNearestSummon();
        }
        else
        {
            List<Vector2I> pathToPlayer = _turnManager.FindPath(myCell, playerCell);
            _playerPathBlocked = (pathToPlayer == null || pathToPlayer.Count == 0);

            if (_playerPathBlocked)
            {
                // Condition 2: Blocked by summon -> target the blocking summon only
                target = _turnManager.GetFirstBlockingSummon(myCell, playerCell);
            }
            else
            {
                // Condition 1 / Default: Target player core
                target = playerCore;
            }
        }

        if (target == null)
            return;

        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);
        List<Vector2I> path = _turnManager.FindPath(myCell, targetCell);

        if (path == null || path.Count == 0)
            return;

        // 2. Step Calculation
        int maxSteps = Mathf.Min(path.Count, RemainingMovement);
        int stepsToTake = 0;

        for (int i = 0; i < maxSteps; i++)
        {
            Vector2I checkCell = path[i];

            // Stop if tile is occupied by another enemy
            if (_turnManager.IsEnemyOccupied(checkCell))
            {
                break;
            }

            // STRICT PROTECTION: Never enter the target's cell itself while it exists!
            if (checkCell == targetCell)
            {
                break;
            }

            // Stop if reaching 1 tile away (adjacent) from target
            if (_turnManager.TileDistance(checkCell, targetCell) <= 1)
            {
                stepsToTake = i + 1;
                break;
            }

            stepsToTake = i + 1;
        }

        if (stepsToTake == 0)
            return;

        // 3. Grid Reservation
        _target = target;
        Vector2I startCell = myCell;
        Vector2I destinationCell = path[stepsToTake - 1];

        _turnManager.MoveEnemyCell(startCell, destinationCell);
        _reservedCell = destinationCell;

        for (int i = 0; i < stepsToTake; i++)
        {
            _plannedPath.Add(path[i]);
        }

        RemainingMovement -= stepsToTake;
    }

    // --- STEP 2: PARALLEL VISUAL ANIMATION ---

    public async Task AnimateMoveAsync(float delay = 0f)
    {
        if (_plannedPath.Count == 0 || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        if (delay > 0f)
        {
            await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);

            if (CurrentHealth <= 0 || !IsInstanceValid(this))
                return;
        }

        for (int i = 0; i < _plannedPath.Count; i++)
        {
            if (CurrentHealth <= 0 || !IsInstanceValid(this))
                return;

            Vector2 targetWorldPos = _turnManager.CellToWorld(_plannedPath[i]);
            await MoveToTileAsync(targetWorldPos);
        }

        _reservedCell = null;
        _plannedPath.Clear();
    }

    private async Task MoveToTileAsync(Vector2 targetPos)
    {
        if (!GodotObject.IsInstanceValid(this) || CurrentHealth <= 0)
            return;
    
        Tween moveTween = CreateTween();
        if (moveTween == null) return;
    
        moveTween.TweenProperty(this, "global_position", targetPos, StepDuration)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.InOut);
    
        Tween hopTween = CreateTween();
        if (hopTween != null)
        {
            hopTween.TweenProperty(_sprite, "position:y", -HopHeight, StepDuration * 0.45f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
            hopTween.Chain().TweenProperty(_sprite, "position:y", 0f, StepDuration * 0.55f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.In);
        }
    
        await ToSignal(moveTween, Tween.SignalName.Finished);
    
        if (!GodotObject.IsInstanceValid(this))
            return;
    
        Vector2 spriteSize = _sprite.Texture.GetSize() * _sprite.Scale;
        _sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));
    
        if (RestDuration > 0)
        {
            await ToSignal(GetTree().CreateTimer(RestDuration), SceneTreeTimer.SignalName.Timeout);
        }
    }

    // --- PHASE 2: COMBAT ---

    public async Task ExecuteAttackPhaseAsync(Node2D playerCore)
    {
        if (HasAttackedThisTurn || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Node2D attackTarget = GetTargetToAttack(playerCore);

        if (attackTarget != null)
        {
            await AttackAsync(attackTarget);
            HasAttackedThisTurn = true;
        }
    }

    private Node2D GetTargetToAttack(Node2D playerCore)
    {
        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition);

        // Condition 3: Enemies configured to prioritize summons
        if (AttacksSummons)
        {
            Node2D nearestSummon = GetNearestSummon();
            if (nearestSummon != null && IsInRange(nearestSummon))
            {
                return nearestSummon;
            }
            return null; // Will not attack anything else if nearest summon is out of range
        }

        // Condition 1: Player core in attack range
        if (IsInRange(playerCore))
        {
            return playerCore;
        }

        // Condition 2: Blocked by summon -> Attack ONLY the blocking summon (if in range)
        Node2D blockingSummon = _turnManager.GetFirstBlockingSummon(myCell, playerCell);
        if (blockingSummon != null && IsInRange(blockingSummon))
        {
            return blockingSummon;
        }

        return null;
    }

    private async Task AttackAsync(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return;

        GD.Print($"[{Name}] ATTACK → '{target.Name}'");

        await FlashYellowAsync();

        if (GodotObject.IsInstanceValid(target) && target.HasMethod("TakeDamage"))
            target.Call("TakeDamage", AttackDamage);
    }

    // --- UTILITY & COMBAT HELPERS ---

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

    public async Task FlashYellowAsync()
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