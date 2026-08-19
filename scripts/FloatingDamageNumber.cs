using Godot;
using System;

public partial class FloatingDamageNumber : Node2D
{
	public RichTextLabel label;

    private Card.Element damageElement;

    private int damageNumber;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	 	label = GetNode<RichTextLabel>("RichTextLabel");
        
        GD.Print($"{damageElement} Card Element");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	public void Appear(int number, Card.Element element) //placeholder ele
	{
        damageNumber = number;
        damageElement = element;
		label.Text = _getTextString();
		Animate();
        GD.Print($"{_getTextString()}");
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

    public string _getTextString()
    {
        switch (damageElement)
        {
            case Card.Element.Earth:
                return $"[center][color=#{Colors.Brown.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/brown.png[/img]";
            case Card.Element.Wind:
                return $"[center][color=#{Colors.White.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/pale.png[/img]";
            case Card.Element.Fire:
                return $"[center][color=#{Colors.Orange.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/orange.png[/img]";
            case Card.Element.Water:
                return $"[center][color=#{Colors.Blue.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/blue.png[/img]";
            case Card.Element.Neutral:
                //will need a neutral colour/image
                return $"[center][color=#{Colors.Gray.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/pale.png[/img]";
            default:
            return $"[center][color=#{Colors.Gray.ToHtml()}]{damageNumber} [img=32x32]res://assets/interface/pale.png[/img]";
        }       
    }
}