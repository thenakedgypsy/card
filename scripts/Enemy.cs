using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [Export] public float Speed = 120f;
    [Export] public float MoveDistance = 100f;
    [Export] public int Health = 10;
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


    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent2D>("NavigationAgent2D");

        _agent.PathDesiredDistance = 4.0f;
        _agent.TargetDesiredDistance = 4.0f;
        _agent.AvoidanceEnabled = true;
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


        if (target.HasMethod("TakeDamage"))
            target.Call("TakeDamage", AttackDamage);
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
}