using Godot;
using System;
using System.Collections.Generic;

public partial class Mouse : Node2D
{
    private Card _activeCard;
    private bool OverBoard;
    private Enemy HoveredEnemy;

    public override void _Process(double delta)
    {
        GlobalPosition = GetGlobalMousePosition();
        HandleInput();
    }

    // =========================
    // INPUT HANDLING
    // =========================

    private void HandleInput()
    {
        // Mouse pressed → pick a card under cursor
        if (Input.IsActionJustPressed("lClick"))
        {
            _activeCard = GetCardUnderMouse();

            if (_activeCard != null)
            {
                _activeCard.StartDrag();
            }
        }

        // Mouse held → drag active card
        if (Input.IsActionPressed("lClick") && _activeCard != null)
        {
            _activeCard.UpdateDrag(GlobalPosition);
        }

        // Mouse released → drop card
        if (Input.IsActionJustReleased("lClick") && _activeCard != null)
        {
            _activeCard.EndDrag();
            _activeCard = null;
        }
    }

    // =========================
    // CARD PICKING (ROBUST)
    // =========================

    private Card GetCardUnderMouse()
    {
        var space = GetWorld2D().DirectSpaceState;

        var query = new PhysicsPointQueryParameters2D
        {
            Position = GlobalPosition,
            CollideWithAreas = true,
            CollideWithBodies = false
        };

        var results = space.IntersectPoint(query);

        Card topCard = null;
        int highestZ = int.MinValue;

        foreach (var hit in results)
        {
            var colliderObj = hit["collider"].AsGodotObject();
            
            if (colliderObj is Node collider)
            {
                // Adjust depending on your structure:
                // If collider IS the card → cast directly
                // If collider is child → use GetParent()
                Card card = collider.GetParent() as Card;

                if (card != null && card.ZIndex > highestZ)
                {
                    highestZ = card.ZIndex;
                    topCard = card;
                }
            }
        }

        return topCard;
    }

    public void setOverBoard(bool value)
    {
        OverBoard = value;
    }

    public bool getOverBoard()
    {
        return OverBoard;
    }

    public void SetHoveredEnemy(Enemy enemy)
    {
        HoveredEnemy = enemy;
    }


    public Enemy GetHoveredEnemy()
    {
        return HoveredEnemy;
    }

}