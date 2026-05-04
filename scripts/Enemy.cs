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

    /// <summary>
    /// The Godot group name that summon nodes are added to.
    /// Make sure every summon scene calls AddToGroup("summons") in _Ready,
    /// or set this to match whatever group name you're using.
    /// </summary>
    [Export] public string SummonGroupName = "summons";

    /// <summary>
    /// If the last point of the computed path to PlayerCore is further than this
    /// from the PlayerCore position, the path is considered blocked by summons and
    /// the enemy will retarget to the nearest summon instead.
    /// </summary>
    [Export] public float PathEndTolerance = 100f;

    private NavigationAgent2D _agent;
    private Node2D _target;

    private float _remainingMoveDistance = 0f;
    private Vector2 _lastPosition;
    private bool _isTakingTurn = false;
    private bool _hasMovedAtLeastOneFrame = false;

    // Throttle per-frame logs so the output stays readable
    private int _logFrameCounter = 0;
    private const int LogEveryNFrames = 10;

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
        _logFrameCounter = 0;
        _target = null;

        GD.Print($"[{Name}] TakeTurn START — pos={GlobalPosition}, movebudget={MoveDistance}, attackRange={AttackRange}");

        // Already in range of the player — attack immediately without moving
        if (IsInRange(playerCore))
        {
            GD.Print($"[{Name}] Already in range of PlayerCore — attacking directly");
            Attack(playerCore);
            EndTurn();
            return;
        }

        // Probe the nav path to PlayerCore
        _agent.TargetPosition = playerCore.GlobalPosition;
        _agent.GetNextPathPosition(); // trigger synchronous path computation
        var path = _agent.GetCurrentNavigationPath();

        bool hasPath = path.Length > 1;
        float pathEndDist = hasPath
            ? path[path.Length - 1].DistanceTo(playerCore.GlobalPosition)
            : float.MaxValue;
        bool pathReachesPlayer = hasPath && pathEndDist <= PathEndTolerance;

        GD.Print($"[{Name}] Path to PlayerCore: points={path.Length}" +
                 $", hasPath={hasPath}" +
                 $", lastPoint={(hasPath ? path[path.Length - 1].ToString() : "n/a")}" +
                 $", distFromPlayer={pathEndDist:F1}" +
                 $", pathReachesPlayer={pathReachesPlayer}");

        if (pathReachesPlayer)
        {
            GD.Print($"[{Name}] Path reaches PlayerCore — targeting PlayerCore");
            _target = playerCore;
            return;
        }

        // Path is truncated by summons — try to retarget to nearest summon
        GD.Print($"[{Name}] Path blocked (end dist={pathEndDist:F1} > tolerance={PathEndTolerance}) — looking for summon to attack");

        Node2D summon = GetNearestSummon();

        if (summon != null)
        {
            GD.Print($"[{Name}] Redirecting to summon '{summon.Name}'");

            if (IsInRange(summon))
            {
                GD.Print($"[{Name}] Already in range of summon '{summon.Name}' — attacking directly");
                Attack(summon);
                EndTurn();
                return;
            }

            if (TrySetTarget(summon))
                return;
        }

        // GetNearestSummon returned null (summons may not be in group yet) —
        // fall back to following the truncated path. The physics loop will
        // do a last-chance range scan when it stops moving.
        if (hasPath)
        {
            GD.Print($"[{Name}] No summon in group '{SummonGroupName}' — following truncated path; will scan on arrival");
            _target = playerCore;
            return;
        }

        GD.Print($"[{Name}] No valid target and no path — ending turn immediately");
        EndTurn();
    }

    private bool TrySetTarget(Node2D target)
    {
        _agent.TargetPosition = target.GlobalPosition;
        _agent.GetNextPathPosition();

        var path = _agent.GetCurrentNavigationPath();
        bool validPath = path.Length > 1;
        bool inRange = IsInRange(target);
        float distToTarget = GlobalPosition.DistanceTo(target.GlobalPosition);

        GD.Print($"[{Name}] TrySetTarget '{target.Name}'" +
                 $" — targetPos={target.GlobalPosition}" +
                 $", distToTarget={distToTarget:F1}" +
                 $", pathLength={path.Length}" +
                 $", validPath={validPath}" +
                 $", alreadyInRange={inRange}");

        if (validPath)
        {
            GD.Print($"[{Name}] Path accepted. First={path[0]}, last={path[path.Length - 1]}");
            _target = target;
            return true;
        }

        if (inRange)
        {
            GD.Print($"[{Name}] No path but already in range — will attack without moving");
            _target = target;
            return true;
        }

        GD.Print($"[{Name}] TrySetTarget FAILED for '{target.Name}' — no path and out of range");
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isTakingTurn || _target == null)
            return;

        _logFrameCounter++;
        bool shouldLog = (_logFrameCounter % LogEveryNFrames == 0);

        float distToTarget = GlobalPosition.DistanceTo(_target.GlobalPosition);
        bool navFinished = _agent.IsNavigationFinished();

        if (shouldLog)
        {
            GD.Print($"[{Name}] Frame {_logFrameCounter}" +
                     $" — pos={GlobalPosition}" +
                     $", target='{_target.Name}'" +
                     $", distToTarget={distToTarget:F1}" +
                     $", remainingMove={_remainingMoveDistance:F1}" +
                     $", navFinished={navFinished}" +
                     $", hasMovedAtLeastOneFrame={_hasMovedAtLeastOneFrame}");
        }

        // ── Primary range check: attack whatever we're currently targeting ──
        if (IsInRange(_target))
        {
            GD.Print($"[{Name}] In range of '{_target.Name}' (dist={distToTarget:F1}) — attacking");
            Velocity = Vector2.Zero;
            Attack(_target);
            EndTurn();
            return;
        }

        // ── Stopping conditions ──
        bool shouldStop = (_hasMovedAtLeastOneFrame && navFinished) || (_remainingMoveDistance <= 0f);

        if (shouldStop)
        {
            string reason = navFinished ? "Navigation finished" : "Move budget exhausted";

            // Last-chance scan: check every summon right now regardless of group membership.
            // This catches the case where the path ended at the summon wall but
            // GetNearestSummon() returned null because summons weren't in the group.
            Node2D nearbySummon = GetNearestSummon();
            if (nearbySummon != null && IsInRange(nearbySummon))
            {
                GD.Print($"[{Name}] {reason} — last-chance scan found '{nearbySummon.Name}' in range (dist={GlobalPosition.DistanceTo(nearbySummon.GlobalPosition):F1}) — attacking");
                Velocity = Vector2.Zero;
                Attack(nearbySummon);
                EndTurn();
                return;
            }

            GD.Print($"[{Name}] {reason} after {_logFrameCounter} frames" +
                     $" — pos={GlobalPosition}, distToTarget={distToTarget:F1}, attackRange={AttackRange}" +
                     $" — no attackable target in range, ending turn");
            EndTurn();
            return;
        }

        // ── Move along path ──
        Vector2 nextPoint = _agent.GetNextPathPosition();
        Vector2 direction = (nextPoint - GlobalPosition).Normalized();

        if (shouldLog)
            GD.Print($"[{Name}] Moving toward nextPathPoint={nextPoint}");

        Velocity = direction * Speed;
        MoveAndSlide();

        float movedThisFrame = GlobalPosition.DistanceTo(_lastPosition);
        _remainingMoveDistance -= movedThisFrame;
        _lastPosition = GlobalPosition;

        _hasMovedAtLeastOneFrame = true;
    }

    private bool IsInRange(Node2D target)
    {
        return GlobalPosition.DistanceTo(target.GlobalPosition) <= AttackRange;
    }

    private void Attack(Node2D target)
    {
        GD.Print($"[{Name}] ATTACK → '{target.Name}' for {AttackDamage} damage");

        if (target.HasMethod("TakeDamage"))
            target.Call("TakeDamage", AttackDamage);
    }

    private Node2D GetNearestSummon()
    {
        var summons = GetTree().GetNodesInGroup("Summons");

        GD.Print($"[{Name}] GetNearestSummon — found {summons.Count} node(s) in group '{SummonGroupName}'");

        Node2D nearest = null;
        float minDist = float.MaxValue;

        foreach (Node node in summons)
        {
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

        GD.Print($"[{Name}] GetNearestSummon → {(nearest != null ? $"'{nearest.Name}' at dist {minDist:F1}" : "none found")}");
        return nearest;
    }

    private void EndTurn()
    {
        GD.Print($"[{Name}] EndTurn — final pos={GlobalPosition}");
        _isTakingTurn = false;
        _hasMovedAtLeastOneFrame = false;
        Velocity = Vector2.Zero;
        _target = null;

        EmitSignal(SignalName.TurnFinished, this);
    }
}
