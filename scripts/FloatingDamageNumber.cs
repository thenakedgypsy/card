using Godot;
using System;

public partial class FloatingDamageNumber : Node2D
{
	public RichTextLabel label;

    private Card.Element damageElement;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	 	label = GetNode<RichTextLabel>("RichTextLabel");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	public void Appear(int number, Card.Element element) //placeholder ele
	{
		label.Text = $"[center][color=#{_getColour().ToHtml()}]{number}";
		Animate();
        GD.Print($"ELEMENT IS ${element} IN FLOATY");
	}

	public void Animate()
    {
        // 1. Create a Tween bound to this node
        Tween tween = CreateTween();

        // 2. Run float up and fade out at the same time
        tween.SetParallel(true);

        // Move upward by 50 pixels over 0.8 seconds
        tween.TweenProperty(this, "position", Position + new Vector2(0, -50), 0.8f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.In);

        // Fade out alpha over 0.8 seconds
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.8f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);

        // 3. Free the node automatically when the animations finish
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    public Color _getColour()
    {
        switch (damageElement)
        {
            case Card.Element.Earth:
                return Colors.Brown;
            case Card.Element.Wind:
                return Colors.White;
            case Card.Element.Fire:
                return Colors.Red;
            case Card.Element.Water:
                return Colors.Blue;
            case Card.Element.Neutral:
                return Colors.Gray;
            default:
            return Colors.WhiteSmoke;
        }       
    }
}