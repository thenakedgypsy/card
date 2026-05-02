using Godot;
using System;
using System.Collections.Generic;

public partial class Mouse : Node2D
{
    private List<Node2D> _overNodes = new List<Node2D>();
    private Card _activeCard;

    public override void _Ready()
    {
    }

    public override void _Process(double delta)
    {
        GlobalPosition = GetGlobalMousePosition();
        CheckInput();
    }

    // =========================
    // INPUT HANDLING
    // =========================

    private void CheckInput()
    {
        // Mouse pressed → pick a card
        if (Input.IsActionJustPressed("lClick"))
        {
            _activeCard = GetTopCard();

            if (_activeCard != null)
            {
                _activeCard.StartDrag();
            }
            else
            {
                //check other nodes here we can reorganise this stuff later if needs be
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
    // CARD SELECTION
    // =========================

    private Card GetTopCard()
    {
        Card topCard = null;
        int highestZ = int.MinValue;

        foreach (Node2D node in _overNodes)
        {
            if (node.GetParent() is Card card)
            {
                if (card.ZIndex > highestZ)
                {
                    highestZ = card.ZIndex;
                    topCard = card;
                }
            }
        }

        return topCard;
    }

    // =========================
    // TRACK MOUSE OVER NODES
    // =========================

    public void MouseOverNode(Node2D node)
    {
        if (!_overNodes.Contains(node))
            _overNodes.Add(node);
    }

    public void MouseOffNode(Node2D node)
    {
        _overNodes.Remove(node);
    }
}