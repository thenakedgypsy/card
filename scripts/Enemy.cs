using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Signal]
    public delegate void TurnFinishedEventHandler(Enemy enemy);

    [Export] public float Speed = 120f;
    [Export] public float moveDistance = 10;
    [Export] public int Health = 10;
    [Export] public int AttackDamage = 1;
    [Export] public bool AttacksSummons = false;

    private NavigationAgent2D _agent;
    private Node2D _target;

    private float _remainingMoveDistance = 0f;
    private Vector2 _lastPosition;
    private bool _isTakingTurn = false;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent2D>("NavigationAgent2D");

        _agent.PathDesiredDistance = 4.0f;
        _agent.TargetDesiredDistance = 4.0f;
        _agent.AvoidanceEnabled = true;
    }

    public void TakeTurn(Node2D playerCore)
    {
        _target = playerCore;
        _agent.TargetPosition = playerCore.GlobalPosition;

        _remainingMoveDistance = moveDistance;
        _lastPosition = GlobalPosition;
        _isTakingTurn = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isTakingTurn || _target == null)
            return;

        if (_agent.IsNavigationFinished())
        {
            EndTurn();
            return;
        }

        Vector2 nextPoint = _agent.GetNextPathPosition();
        Vector2 direction = (nextPoint - GlobalPosition).Normalized();

        Velocity = direction * Speed;
        MoveAndSlide();

        float movedThisFrame = GlobalPosition.DistanceTo(_lastPosition);
        _remainingMoveDistance -= movedThisFrame;
        _lastPosition = GlobalPosition;

        if (_remainingMoveDistance <= 0f)
        {
            EndTurn();
        }
    }

    private void EndTurn()
    {
        _isTakingTurn = false;
        Velocity = Vector2.Zero;

        EmitSignal(SignalName.TurnFinished, this);
    }
}