using Godot;
using System;

public partial class Enemy : CharacterBody2D, IHealth
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [Export] public float Speed = 120f;
    [Export] public float MoveDistance = 100f;
    [Export] public int Health = 10;
    public int CurrentHealth;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;
    [Export] public float AttackRange = 50f;
    [Export] public string SummonGroupName = "Summons";
    [Export] public float PathEndTolerance = 100f;

    private NavigationAgent2D _agent;
    private Node2D _target;

    private float _remainingMoveDistance = 0f;
    private Vector2 _lastPosition;
    private bool _isTakingTurn = false;
    private bool _hasMovedAtLeastOneFrame = false;

    // Did we fail to reach the player because summons blocked navigation?
    private bool _playerPathBlocked = false;
    private Sprite2D _sprite;
    private bool _isHovered;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent2D>("NavigationAgent2D");

        _agent.PathDesiredDistance = 4.0f;
        _agent.TargetDesiredDistance = 4.0f;
        _agent.AvoidanceEnabled = true;

        _sprite = GetNode<Sprite2D>("Sprite2D");

        CurrentHealth = Health;
    }


    public void TakeTurn(Node2D playerCore)
    {
        _remainingMoveDistance = MoveDistance;
        _lastPosition = GlobalPosition;
        _isTakingTurn = true;
        _hasMovedAtLeastOneFrame = false;
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
        _agent.TargetPosition = playerCore.GlobalPosition;
        _agent.GetNextPathPosition();

        var path = _agent.GetCurrentNavigationPath();

        bool hasPath = path.Length > 1;

        float pathEndDist = hasPath
            ? path[path.Length - 1].DistanceTo(playerCore.GlobalPosition)
            : float.MaxValue;


        _playerPathBlocked = !hasPath || pathEndDist > PathEndTolerance;


        // 3. If blocked, move toward nearest summon
        if (_playerPathBlocked || AttacksSummons)
        {
            Node2D blockingSummon = GetNearestSummon();

            if (blockingSummon != null)
            {
                if (IsInRange(blockingSummon))
                {
                    Attack(blockingSummon);
                    EndTurn();
                    return;
                }


                if (TrySetTarget(blockingSummon))
                    return;
            }
        }


        // 4. Otherwise move toward player
        _target = playerCore;
    }


    private bool TrySetTarget(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;


        _agent.TargetPosition = target.GlobalPosition;
        _agent.GetNextPathPosition();

        var path = _agent.GetCurrentNavigationPath();


        if (path.Length > 1 || IsInRange(target))
        {
            _target = target;
            return true;
        }


        return false;
    }


    public override void _PhysicsProcess(double delta)
    {
        if (!_isTakingTurn || _target == null)
            return;


        // Target reached
        if (IsInRange(_target))
        {
            Velocity = Vector2.Zero;
            Attack(_target);
            EndTurn();
            return;
        }


        bool navFinished = _agent.IsNavigationFinished();

        bool shouldStop =
            (_hasMovedAtLeastOneFrame && navFinished) ||
            (_remainingMoveDistance <= 0f);



        if (shouldStop)
        {
            /*
             * If allowed, attack any summon nearby.
             */
            if (AttacksSummons)
            {
                Node2D summon = GetNearestSummon();

                if (summon != null && IsInRange(summon))
                {
                    Velocity = Vector2.Zero;
                    Attack(summon);
                    EndTurn();
                    return;
                }
            }


            /*
             * Otherwise only attack summons when they blocked
             * access to the player.
             */
            else if (_playerPathBlocked || AttacksSummons)
            {
                Node2D blockingSummon = GetNearestSummon();

                if (blockingSummon != null && IsInRange(blockingSummon))
                {
                    Velocity = Vector2.Zero;
                    Attack(blockingSummon);
                    EndTurn();
                    return;
                }
            }


            EndTurn();
            return;
        }



        // Movement
        Vector2 nextPoint = _agent.GetNextPathPosition();

        Vector2 direction = (nextPoint - GlobalPosition).Normalized();


        Velocity = direction * Speed;

        MoveAndSlide();


        float moved = GlobalPosition.DistanceTo(_lastPosition);

        _remainingMoveDistance -= moved;

        _lastPosition = GlobalPosition;


        _hasMovedAtLeastOneFrame = true;
    }



    private bool IsInRange(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return false;


        return GlobalPosition.DistanceTo(target.GlobalPosition) <= AttackRange;
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
        Color original = SelfModulate;
        Tween tween = CreateTween();
        // Flash red
        tween.TweenProperty(_sprite, "self_modulate", Colors.Orange, 0.25f);
        // Return to original color
        tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
    
    public async void FlashRed()
    {
        Color original = SelfModulate;
        Tween tween = CreateTween();
        // Flash red
        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.25f);
        // Return to original color
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

        _hasMovedAtLeastOneFrame = false;

        Velocity = Vector2.Zero;

        _target = null;


        EmitSignal(SignalName.TurnFinished, this);
    }

	public float GetMaxHealth()
	{
		return Health;
	}

	public float GetCurrentHealth()
	{
		return CurrentHealth;
	}
    
    public void TakeDamage(int value)
	{
	    CurrentHealth -= value;
	    GD.Print($"Enemy {Name} takes {value} damage");

		FlashRed();
	
	    if (CurrentHealth <= 0)
	    {
	        GD.Print($"Enemy {Name} IS DESTROYED");
	
	        // Prevent double-death logic
	        SetProcess(false);
	        SetPhysicsProcess(false);
	
	        // Optional: stop collisions if you have them
	        SetDeferred("monitoring", false);
	
	        // Safely remove from tree at end of frame
	        CallDeferred(Node.MethodName.QueueFree);
	    }
	}

    public void MouseOver()
    {
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
    
        if(mouse != null)
            mouse.SetHoveredEnemy(this);
    
        _sprite.SelfModulate = Colors.Yellow;
    }
    
    
    public void MouseOff()
    {
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
    
        if(mouse != null)
            mouse.SetHoveredEnemy(null);
    
        _sprite.SelfModulate = Colors.White;
    }

    private void _on_area_2d_mouse_entered()
    {
        MouseOver();
    }


    private void _on_area_2d_mouse_exited()
    {
        MouseOff();
    }
    
    public bool IsHovered()
    {
        return _isHovered;
    }
}