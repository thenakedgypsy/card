using Godot;
using System;
using System.Collections.Generic;

public partial class StatusEffect : Node2D
{    
    public enum Type
    {
        Burn,
        Slow,
        Confuse,
        Haste,
        Stun,
        Disarm
    }

	public Card.Element Element;
	public int Damage;

    public int TurnsLeftActive;

    public StatusEffect.Type TypeName;

    //Variant = any
    //dictionary = Record - so each line of the card json is a Dictionary?
    public void AddStatus(Card.Element ele, Dictionary<string, Variant> data)
    {
        //is there a way to deconstruct data?
        Element = ele;
        Damage = data["damage"].ToString().ToInt();
        TurnsLeftActive = data["turnsActive"].ToString().ToInt();
        StatusEffect.Type TypeName;
        if (data.ContainsKey("statusType") && Enum.TryParse(data["element"].ToString(), out StatusEffect.Type parsedElement))
        TypeName = parsedElement;
    }

    //generic apply status 
    //dont think we'll need this for burn at least - just does a damage per turn.
    private void _ApplyBurn(int damage, int numberOfTurns)
    {
        //safety first!


        
    }

    //Add reduce function in? Might leave for now but sometimes we may reduce by more than 1
    private void _ReduceTurnsLeftActive()
    {
        
    }

}