using Godot;
using System;
using System.Collections.Generic;

public partial class EnergyManager : Node2D
{
	public Dictionary<Card.Element, int> CurrentEnergy;
	public Dictionary<Card.Element, int> EnergyRegen;
	private RichTextLabel FireLabel;
	private RichTextLabel WaterLabel;
	private RichTextLabel WindLabel;
	private RichTextLabel EarthLabel;
	private RichTextLabel FireRegenLabel;
	private RichTextLabel WaterRegenLabel;
	private RichTextLabel WindRegenLabel;
	private RichTextLabel EarthRegenLabel;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{		
		CurrentEnergy = new Dictionary<Card.Element, int>();
		EnergyRegen = new Dictionary<Card.Element, int>();
		FireLabel = GetNode<RichTextLabel>("FireLabel");
		WaterLabel = GetNode<RichTextLabel>("WaterLabel");
		EarthLabel = GetNode<RichTextLabel>("EarthLabel");
		WindLabel = GetNode<RichTextLabel>("WindLabel");
		FireRegenLabel = GetNode<RichTextLabel>("FireLabel2");
		WaterRegenLabel = GetNode<RichTextLabel>("WaterLabel2");
		EarthRegenLabel = GetNode<RichTextLabel>("EarthLabel2");
		WindRegenLabel = GetNode<RichTextLabel>("WindLabel2");
		Reset();

	}

	public void Reset()
	{
		CurrentEnergy = new Dictionary<Card.Element, int>();
		EnergyRegen = new Dictionary<Card.Element, int>();

		CurrentEnergy.Add(Card.Element.Fire, 0);
		CurrentEnergy.Add(Card.Element.Water, 0);
		CurrentEnergy.Add(Card.Element.Wind, 0);
		CurrentEnergy.Add(Card.Element.Earth, 0);

		EnergyRegen.Add(Card.Element.Fire, 0);
		EnergyRegen.Add(Card.Element.Water, 0);
		EnergyRegen.Add(Card.Element.Wind, 0);
		EnergyRegen.Add(Card.Element.Earth, 0);
		UpdateLabels();
	}

	public void UpdateLabels()
	{
		FireLabel.Text = CurrentEnergy[Card.Element.Fire].ToString();
		WaterLabel.Text = CurrentEnergy[Card.Element.Water].ToString();
		WindLabel.Text = CurrentEnergy[Card.Element.Wind].ToString();
		EarthLabel.Text = CurrentEnergy[Card.Element.Earth].ToString();
		
		FireRegenLabel.Text = EnergyRegen[Card.Element.Fire].ToString();
		WaterRegenLabel.Text = EnergyRegen[Card.Element.Water].ToString();
		WindRegenLabel.Text = EnergyRegen[Card.Element.Wind].ToString();
		EarthRegenLabel.Text = EnergyRegen[Card.Element.Earth].ToString();
	}


	public void RegenerateEnergy()
	{
		foreach (Card.Element element in CurrentEnergy.Keys)
		{
			CurrentEnergy[element] = EnergyRegen[element];
		}
		UpdateLabels();
	}

	public bool TrySpendEnergy(int cost, Card.Element element)
	{
		if (CurrentEnergy[element] >= cost)
		{
			CurrentEnergy[element] -= cost;
			UpdateLabels();
			return true;
		}
		else
		{
			UpdateLabels();
			return false;
		}
	}

	public void GainRegen(int amount, Card.Element element)
	{
		CurrentEnergy[element] += amount;
		EnergyRegen[element] += amount;
		UpdateLabels();
	}

}
