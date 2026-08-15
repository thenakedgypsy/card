using Godot;
using System;

public partial class EnergyButton : Button
{
	[Export]
	public string element;
	[Export]
	public bool negative;
	public Card.Element Element;
	private EnergyGain Manager;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Manager = GetParent() as EnergyGain;
		Element = Enum.Parse<Card.Element>(element);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Clicked()
	{
		Manager.ButtonPressed(Element);		
	}
}
