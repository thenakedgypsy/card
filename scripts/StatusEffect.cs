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
    Card.Element element;

    //Variant = any
    //dictionary = Record - so each line of the card json is a Dictionary?

    public void AddStatus(Dictionary<string, Variant> data)
    {
        //filter the type here?
        
        

    }

    //create a function for each type?
    //so a burn function which i can call and pass in the damage to?

    //contruct effect?
    //already in Card Effect 
    private void _ApplyBurn(int damage, int numberOfTurns,Enemy enemy)
    {
        //safety first!
        if (!GodotObject.IsInstanceValid(enemy))
            return;

        GD.Print($"{enemy.Name} is gonna take {damage} damage for {numberOfTurns} turns");

        
    }

}