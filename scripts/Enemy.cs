using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    public int MoveDistance = 4;
    [Export] public int DefaultMoveDistance = 4;
    [Export] public int Health = 10;
    public int CurrentHealth;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;
    [Export] public int AttackRange = 1;
    [Export] public string SummonGroupName = "Summons";
    public Card.Element Element = Card.Element.Earth;
    [Export] public string EnemyGroupName = "Enemies";
    public int RemainingMovement { get; private set; }
    public bool HasAttackedThisTurn { get; private set; }
    public bool WasPathBlocked { get; private set; }

    public StatusEffect StatusEffect = null;

    private Vector2I? _reservedCell = null;
    private Node2D _target;
    private TurnManager _turnManager;
    private AnimatedSprite2D _sprite;
    public bool IsSlowed { get; set; }
    public bool IsStunned { get; set; }
    public bool IsConfused { get; set; }
    // Split hover states to avoid race conditions between Mouse.cs and SpellTargeter.cs
    private bool _isMouseHovered = false;
    private bool _isAoEHovered = false;

    

    private List<Vector2I> _plannedPath = new List<Vector2I>();

    // Helper property to access logical grid position safely regardless of animations
    public Vector2I CurrentCell => _reservedCell ?? _turnManager.WorldToCell(GlobalPosition);

    public override void _Ready()
    {
        AddToGroup("Enemy");
        
        _sprite = GetNode<AnimatedSprite2D>("Sprite2D");
        Random random = new Random();       
        if (this.AttacksSummons)
        {
            _sprite.Animation = "attacker_idle";
            _sprite.Play();
        }
        _sprite.Frame = random.Next(8);
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;

        CurrentHealth = Health;
    }

    public void ResetTurnState(bool resetMovement = true)
    {
        if (resetMovement)
        {
            // Only reset stun if no active StatusEffect child exists
            if (!HasActiveStunEffects())
            {
                IsStunned = false;
                MoveDistance = DefaultMoveDistance;
            }
            if (!HasActiveSlowEffects())
            {
                IsSlowed = false;
                MoveDistance = DefaultMoveDistance;
            }

            RemainingMovement = MoveDistance;
            HasAttackedThisTurn = false;

        }

        WasPathBlocked = false;
        _plannedPath.Clear();
        _reservedCell = null;
        SetBlockedVisualState(false);
    }

    private bool HasActiveStunEffects()
    {
        foreach (Node child in GetChildren())
        {
            if (child is StatusEffect)
            {
                StatusEffect status = child as StatusEffect;
                if (status.TypeName == StatusEffect.Type.Stun)
                {
                    return true;
                }
                
            }
        }
        return false;
    }
    private bool HasActiveSlowEffects()
        {
            foreach (Node child in GetChildren())
            {
                if (child is StatusEffect)
                {
                    StatusEffect status = child as StatusEffect;
                    if (status.TypeName == StatusEffect.Type.Slow)
                    {
                        return true;
                    }
                    
                }
            }
            return false;
        }
    // --- STEP 1: POSITION CALCULATIONS ---

    public void PlanMove(Node2D playerCore)
    {
        _plannedPath.Clear(); 
        WasPathBlocked = false; 

        // Early exit if stunned, dead, or out of movement[cite: 1]
        if (IsStunned || RemainingMovement <= 0 || CurrentHealth <= 0 || !IsInstanceValid(this)) 
            return; 

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition); 
        Vector2I playerCell = _turnManager.WorldToCell(playerCore.GlobalPosition); 

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
    

        List<Vector2I> path = _turnManager.FindPath(myCell, targetCell); 

        if (path == null || path.Count == 0) 
        {
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
            _turnManager.OccupyCell(myCell); 
            _reservedCell = myCell; 
            return; 
        }

        int stepsToTake = 0; 
        for (int i = 0; i < path.Count && stepsToTake < RemainingMovement; i++) 
        {
            Vector2I checkCell = path[i]; 
        
            // Stop if a summon blocks the path (since we aren't passing through them)
            if (WasPathBlocked && _turnManager.IsCellOccupiedBySummon(checkCell)) 
                break; 
        
            // Stop if we reached the target[cite: 1]
            if (checkCell == targetCell) 
                break; 
        
            stepsToTake++; 
        }
        
        // NEW LOGIC: Backtrack if the final intended landing cell is occupied/reserved
        while (stepsToTake > 0 && _turnManager.IsEnemyOccupied(path[stepsToTake - 1]))
        {
            stepsToTake--;
        }
        
        Vector2I destinationCell = myCell;; 

        if (stepsToTake > 0) 
        {
            destinationCell = path[stepsToTake - 1]; 
            for (int i = 0; i < stepsToTake; i++) 
            {
                _plannedPath.Add(path[i]); 
            }
            RemainingMovement -= stepsToTake; 
        }

        _turnManager.OccupyCell(destinationCell); 
        _reservedCell = destinationCell; 
    }

    // --- STEP 2: MOVEMENT ANIMATION ---

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
    
        Vector2 spriteSize = _sprite.SpriteFrames.GetFrameTexture(AttacksSummons? "attacker_idle" : "shield_idle", 0).GetSize() * _sprite.Scale;
        _sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));
    
        if (RestDuration > 0)
        {
            await ToSignal(GetTree().CreateTimer(RestDuration), SceneTreeTimer.SignalName.Timeout);
        }
    }

    // --- PHASE 3: COMBAT ---

    public async Task ExecuteAttackPhaseAsync(Node2D playerCore)
    {
        if (IsStunned || HasAttackedThisTurn || CurrentHealth <= 0 || !IsInstanceValid(this))
            return;
       

        Node2D attackTarget = GetTargetToAttack(playerCore);
        var NearestEnemy = GetNearestEnemyTarget();

        if (IsConfused && NearestEnemy != null)
        {
            GD.Print("Enemy taking damage");
            //NearestEnemy.TakeDamage(AttackDamage, Element);
        }

        if (attackTarget != null)
        {
            await AttackAsync(attackTarget);
            HasAttackedThisTurn = true;
            RemainingMovement = 0;
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

        if (WasPathBlocked)
        {
            Node2D blockingSummon = _turnManager.GetFirstBlockingSummon(myCell, playerCell);
            if (blockingSummon != null && IsInRange(blockingSummon))
                return blockingSummon;
        }

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
    
        Vector2I myCell = CurrentCell;
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);
    
        return _turnManager.GetPathLengthToTarget(myCell, targetCell, ignoreSummons);
    }

    private bool IsInRange(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;

        Vector2I myCell = CurrentCell;
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

    private Enemy GetNearestEnemyTarget()
    {
        var enemies = GetTree().GetNodesInGroup(EnemyGroupName);
        var distances = new Dictionary<Enemy, int>();
        List<Dictionary<Enemy, int>> allEnemyDistances = new List<Dictionary<Enemy, int>>();
        int maxDist = 1;
        Enemy chosenEnemy = null;

        foreach(Node node in enemies)
        {
            Enemy enemy = node as Enemy;
            float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);

            //list out all enemies and their distance - ones with the least gets hit
            //will need a min distance check like summon

            //i can just get all enemies in a certain range
            //if next to multiple enemies - will have to choose one / could be random
            if (GetRouteDistanceTo(enemy, false) == maxDist)
            {
                chosenEnemy = enemy;
                GD.Print($"chosen enemy! {chosenEnemy.AttackDamage}");

            }

        }

        return chosenEnemy;
    }

    public async Task FlashYellowAsync()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(_sprite, "self_modulate", Colors.Orange, 0.25f);
        await ToSignal(tween, Tween.SignalName.Finished);
        UpdateVisualState();
    }

    public async void FlashRed()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.25f);
        await ToSignal(tween, Tween.SignalName.Finished);
        UpdateVisualState();
    }

    public float GetMaxHealth() => Health;
    public float GetCurrentHealth() => CurrentHealth;

    public void TakeDamage(int value, Card.Element element)
    {
        CurrentHealth -= value;
        GD.Print($"Enemy {Name} takes {value} damage");

        PackedScene scene = GD.Load<PackedScene>("res://prefabs/floating_damage_number.tscn");
        FloatingDamageNumber fdn = scene.Instantiate() as FloatingDamageNumber;
        GetParent().AddChild(fdn);
        fdn.GlobalPosition = GlobalPosition;
        fdn.Appear(value, element);       
        FlashRed();

        if (CurrentHealth <= 0)
        {
            GD.Print($"Enemy {Name} IS DESTROYED");

            Vector2I cellToFree = CurrentCell;
            _turnManager.FreeCell(cellToFree);

            SetProcess(false);
            SetPhysicsProcess(false);
            SetDeferred("monitoring", false);

            if (_sprite != null && GodotObject.IsInstanceValid(_sprite))
            {
                _sprite.Visible = false;
            }

            CallDeferred(Node.MethodName.QueueFree);
        }
    }

    private void SetBlockedVisualState(bool blocked)
    {
        WasPathBlocked = blocked;
        UpdateVisualState();
    }

    public void SetHovered(bool hovered)
    {
        _isAoEHovered = hovered;
        UpdateVisualState();
    }

    public void SetMouseHovered(bool hovered)
    {
        _isMouseHovered = hovered;
        UpdateVisualState();
    }

    public void UpdateVisualState()
    {
        if (_sprite == null || !GodotObject.IsInstanceValid(_sprite))
            return;

        bool isHighlighted = _isMouseHovered || _isAoEHovered;

        if (isHighlighted)
        {
            _sprite.SelfModulate = WasPathBlocked ? Colors.Red : Colors.Yellow;
        }
        else
        {
            _sprite.SelfModulate = WasPathBlocked ? Colors.Red : Colors.White;
        }
    }

    public void MouseOver()
    {
        SetMouseHovered(true);
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        if (mouse != null) mouse.SetHoveredEnemy(this);
    }

    public void MouseOff()
    {
        SetMouseHovered(false);
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        if (mouse != null && mouse.GetHoveredEnemy() == this)
        {
            mouse.SetHoveredEnemy(null);
        }
    }

    private void _on_area_2d_mouse_entered() { }
    private void _on_area_2d_mouse_exited() { }

    public bool IsHovered() => _isMouseHovered || _isAoEHovered;

    public void TryTriggerStatusEffect()
    {
        Node[] enemyChildren = GetChildren().ToArray();

        foreach (Node child in enemyChildren)
        {
            if (child is StatusEffect statusEffect)
            {
                statusEffect.TriggerStatusEffect();
            }
        }
    }
}