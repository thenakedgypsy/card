using Godot;
using System;
using System.Collections.Generic;

public partial class Enemy : CharacterBody2D, IHealth
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [Export] public float Speed = 120f;
    [Export] public int MoveDistance = 3; // tiles per turn
    [Export] public int Health = 10;
    public int CurrentHealth;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;
    [Export] public int AttackRange = 1; // tiles
    [Export] public string SummonGroupName = "Summons";

    private Node2D _target;

    private List<Vector2> _pathWaypoints = new();
    private int _waypointIndex = 0;

    private bool _isTakingTurn = false;
    private TurnManager _turnManager;

    // Did we fail to reach the player because summons blocked navigation?
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
        _pathWaypoints.Clear();
        _waypointIndex = 0;

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
            
            // Fallback just in case no route block was found
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

    


    private void BeginMoveAlong(List<Vector2I> path)
    {
        _pathWaypoints.Clear();
        _waypointIndex = 0;

        int steps = Mathf.Min(path.Count, MoveDistance);

        for (int i = 0; i < steps; i++)
        {
            _pathWaypoints.Add(_turnManager.CellToWorld(path[i]));
        }
    }


    public int GetRouteDistanceTo(Node2D target)
    {
        if (!GodotObject.IsInstanceValid(target))
            return int.MaxValue;

        Vector2I myCell = _turnManager.WorldToCell(GlobalPosition);
        Vector2I targetCell = _turnManager.WorldToCell(target.GlobalPosition);

        List<Vector2I> path = _turnManager.FindPath(myCell, targetCell);

        if (path == null || path.Count == 0)
            return int.MaxValue;

        return path.Count;
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


        if (_waypointIndex >= _pathWaypoints.Count)
        {
            HandleEndOfMovement();
            return;
        }


        // Movement toward current tile waypoint
        Vector2 waypoint = _pathWaypoints[_waypointIndex];
        Vector2 direction = (waypoint - GlobalPosition).Normalized();

        Velocity = direction * Speed;
        MoveAndSlide();

        if (GlobalPosition.DistanceTo(waypoint) <= 4f)
        {
            _waypointIndex++;

            if (_waypointIndex >= _pathWaypoints.Count)
            {
                HandleEndOfMovement();
            }
        }
    }


    private void HandleEndOfMovement()
    {
        Velocity = Vector2.Zero;

        // Since _target is explicitly set to the blocking summon (or player) in TakeTurn,
        // we can simply check if we are in range of our assigned target.
        if (_target != null && IsInRange(_target))
        {
            Attack(_target);
            EndTurn();
            return;
        }

        EndTurn();
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