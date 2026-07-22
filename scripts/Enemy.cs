using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody2D, IHealth
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [ExportGroup("Hop Settings")]
    [Export] public float StepDuration = 0.16f;
    [Export] public float RestDuration = 0.32f;
    [Export] public float HopHeight = 8f;

    [ExportGroup("Main Settings")]
    [Export] public int MoveDistance = 4;
    [Export] public int Health = 10;
    public int CurrentHealth;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;
    [Export] public int AttackRange = 1;
    [Export] public string SummonGroupName = "Summons";

    public int RemainingMovement { get; private set; }
    public bool HasAttackedThisTurn { get; private set; }
    public bool WasPathBlocked { get; private set; }

    private Vector2I? _reservedCell = null;
    private Node2D _target;
    private TurnManager _turnManager;
    private Sprite2D _sprite;
    private bool _isHovered;

    private List<Vector2I> _plannedPath = new List<Vector2I>();

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;

        CurrentHealth = Health;
    }

    public void ResetTurnState(bool resetMovement = true)
    {
        if (resetMovement)
        {
            RemainingMovement = MoveDistance;
            HasAttackedThisTurn = false;
        }

        WasPathBlocked = false;
        _plannedPath.Clear();
        _reservedCell = null;
        SetBlockedVisualState(false);
    }

    // --- STEP 1: POSITION CALCULATIONS ---

    public void PlanMove(Node2D playerCore)
    {
        _plannedPath.Clear();
        WasPathBlocked = false;

        if (RemainingMovement <= 0 || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition);

        // 1. Unmark own tile as blocked so we don't trap ourselves or others doing calculations
        _turnManager.FreeCell(myCell);

        Vector2I targetCell = playerCell;
        Node2D primaryTarget = playerCore;

        if (AttacksSummons)
        {
            Node2D nearestSummon = GetNearestSummon();
            if (nearestSummon != null)
            {
                primaryTarget = nearestSummon;
                targetCell = _turnManager.WorldToCell(nearestSummon.GlobalPosition);
            }
        }

        // 2. See if a path is possible without being blocked (Summons are solid by default here)
        List<Vector2I> path = _turnManager.FindPath(myCell, targetCell);
        
        if (path == null || path.Count == 0)
        {
            // Path IS blocked. Find shortest route excluding summons.
            WasPathBlocked = true;
            SetBlockedVisualState(true);
            path = _turnManager.FindPathIgnoringSummons(myCell, targetCell);
        }
        else
        {
            SetBlockedVisualState(false);
        }

        if (path == null || path.Count == 0)
        {
            // Still no path (completely boxed in by terrain/enemies)
            _turnManager.OccupyCell(myCell); // Re-occupy current spot
            _reservedCell = myCell;
            return;
        }

        // 3. Trace it as far as possible
        int stepsToTake = 0;
        for (int i = 0; i < path.Count && stepsToTake < RemainingMovement; i++)
        {
            Vector2I checkCell = path[i];

            // Stop if tile is filled by another enemy
            if (_turnManager.IsEnemyOccupied(checkCell))
                break;

            // If we are on the blocked fallback path, stop once we hit a summon (making us adjacent)
            if (WasPathBlocked && _turnManager.IsCellOccupiedBySummon(checkCell))
                break;

            // Never walk directly onto the primary target's cell
            if (checkCell == targetCell)
                break;

            stepsToTake++;
        }

        Vector2I destinationCell = myCell;
        
        if (stepsToTake > 0)
        {
            destinationCell = path[stepsToTake - 1];
            for (int i = 0; i < stepsToTake; i++)
            {
                _plannedPath.Add(path[i]);
            }
            RemainingMovement -= stepsToTake;
        }

        // 4. Mark this tile as filled for subsequent enemies
        _turnManager.OccupyCell(destinationCell);
        _reservedCell = destinationCell;
    }

    // --- STEP 2: MOVEMENT ANIMATION ---

    private void SetBlockedVisualState(bool blocked)
    {
        if (_sprite == null)
            return;

        _sprite.SelfModulate = blocked ? Colors.Red : Colors.White;
    }

    public async Task AnimateMoveAsync(float delay = 0f)
    {
        if (_plannedPath.Count == 0 || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        if (delay > 0f)
        {
            await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
            if (CurrentHealth <= 0 || !IsInstanceValid(this)) return;
        }

        for (int i = 0; i < _plannedPath.Count; i++)
        {
            if (CurrentHealth <= 0 || !IsInstanceValid(this)) return;
            Vector2 targetWorldPos = _turnManager.CellToWorld(_plannedPath[i]);
            await MoveToTileAsync(targetWorldPos);
        }

        // Only clear planned path, leave reserved cell intact for subsequent phases
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

    // --- PHASE 3: COMBAT ---

    public async Task ExecuteAttackPhaseAsync(Node2D playerCore)
    {
        if (HasAttackedThisTurn || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;

        Node2D attackTarget = GetTargetToAttack(playerCore);

        if (attackTarget != null)
        {
            await AttackAsync(attackTarget);
            HasAttackedThisTurn = true;
            RemainingMovement = 0; // Usually attacking ends movement completely
        }
    }

    private Node2D GetTargetToAttack(Node2D playerCore)
    {
        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition);

        if (AttacksSummons)
        {
            Node2D nearestSummon = GetNearestSummon();
            if (nearestSummon != null && IsInRange(nearestSummon))
                return nearestSummon;
            return null;
        }

        // Attack if path was blocked and we are in range of the blocking summon
        if (WasPathBlocked)
        {
            Node2D blockingSummon = _turnManager.GetFirstBlockingSummon(myCell, playerCell);
            if (blockingSummon != null && IsInRange(blockingSummon))
                return blockingSummon;
        }

        // Attack if near the player core
        if (IsInRange(playerCore))
            return playerCore;

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

    public int GetRouteDistanceTo(Node2D target, bool ignoreSummons)
    {
        if (!GodotObject.IsInstanceValid(target))
            return int.MaxValue;
    
        Vector2I myCell = _reservedCell ?? _turnManager.WorldToCell(GlobalPosition);
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);
    
        return _turnManager.GetPathLengthToTarget(myCell, targetCell, ignoreSummons);
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
        if (mouse != null) mouse.SetHoveredEnemy(this);

        _sprite.SelfModulate = WasPathBlocked ? Colors.Red : Colors.Yellow;
    }

    public void MouseOff()
    {
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        if (mouse != null) mouse.SetHoveredEnemy(null);

        _sprite.SelfModulate = WasPathBlocked ? Colors.Red : Colors.White;
    }

    private void _on_area_2d_mouse_entered() => MouseOver();
    private void _on_area_2d_mouse_exited() => MouseOff();

    public bool IsHovered() => _isHovered;
}