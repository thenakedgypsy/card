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

    public int TurnsActive;

    public StatusEffect.Type TypeName;

    //Variant = any
    //dictionary = Record - so each line of the card json is a Dictionary?
    public void AddStatus(Card.Element ele, Dictionary<string, Variant> data)
    {
        //is there a way to deconstruct data?
        Element = ele;
        Damage = data["damage"].ToString().ToInt();
        TurnsActive = data["turnsActive"].ToString().ToInt();
        StatusEffect.Type TypeName;
        if (data.ContainsKey("statusType") && Enum.TryParse(data["element"].ToString(), out StatusEffect.Type parsedElement))
        TypeName = parsedElement;
        
    }

    //generic apply status 
    private void _ApplyBurn(int damage, int numberOfTurns,Enemy enemy)
    {
        //safety first!
        if (!GodotObject.IsInstanceValid(enemy))
            return;

        GD.Print($"{enemy.Name} is gonna take {damage} damage for {numberOfTurns} turns");

        
    }

}