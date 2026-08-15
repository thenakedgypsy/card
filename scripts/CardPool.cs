using Godot;
using System;
using System.Collections.Generic;


public partial class CardPool : Node2D
{
	public Dictionary<string, Card.Rarity> fireCards;
	public Dictionary<string, Card.Rarity> windCards;
	public Dictionary<string, Card.Rarity> waterCards;
	public Dictionary<string, Card.Rarity> earthCards;
	public Dictionary<Card.Element, Dictionary<string, Card.Rarity>> fullCardDatabase;
	public override void _Ready()
	{
		InitialiseFullDB();
		ScanForCardData();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

public Godot.Collections.Dictionary LoadJson(string path)
{
    if (!FileAccess.FileExists(path))
        return new Godot.Collections.Dictionary();

    using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

    if (file == null)
    {
        GD.PrintErr($"Could not open JSON file: {path}");
        return new Godot.Collections.Dictionary();
    }

    string jsonText = file.GetAsText();

    Json json = new Json();
    Error error = json.Parse(jsonText);

    if (error != Error.Ok)
    {
        GD.PrintErr(
            $"JSON parse error in {path}: " +
            $"{json.GetErrorMessage()} at line {json.GetErrorLine()}"
        );

        return new Godot.Collections.Dictionary();
    }

    if (json.Data.VariantType != Variant.Type.Dictionary)
    {
        GD.PrintErr($"JSON root is not an object/dictionary: {path}");
        return new Godot.Collections.Dictionary();
    }

    return json.Data.AsGodotDictionary();
}

public void ScanForCardData()
{
    const string dataFolder = "res://assets/cards/data";

    using DirAccess dir = DirAccess.Open(dataFolder);

    if (dir == null)
    {
        GD.PrintErr($"Could not open card data directory: {dataFolder}");
        return;
    }

    foreach (string fileName in dir.GetFiles())
    {
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            continue;

        string jsonName = System.IO.Path.GetFileNameWithoutExtension(fileName);

        AddToFullDatabase(jsonName);

        GD.Print($"Adding {jsonName} to Full Card Database");
    }
}

	public void InitialiseFullDB()
	{
		fullCardDatabase = new Dictionary<Card.Element, Dictionary<string, Card.Rarity>>();
		fireCards = new Dictionary<string, Card.Rarity>();
		waterCards = new Dictionary<string, Card.Rarity>();
		windCards = new Dictionary<string, Card.Rarity>();
		earthCards = new Dictionary<string, Card.Rarity>();

		fullCardDatabase.Add(Card.Element.Earth, earthCards);
		fullCardDatabase.Add(Card.Element.Fire, fireCards);
		fullCardDatabase.Add(Card.Element.Water, waterCards);
		fullCardDatabase.Add(Card.Element.Wind, windCards);
	}

	public void AddToFullDatabase(string cardID)
	{
		string dataPath = $"res://assets/cards/data/{cardID}.json";
		var data = LoadJson(dataPath);
		Card.Element element = Card.Element.Neutral; //fallback - wont work but fuckit should be fine. 
		Card.Rarity rarity = Card.Rarity.Common; //fallback - will work

		if (data.ContainsKey("element") &&
            Enum.TryParse(data["element"].ToString(), out Card.Element parsedElement))
            element = parsedElement;

		if (data.ContainsKey("rarity") &&
            Enum.TryParse(data["rarity"].ToString(), out Card.Rarity parsedRarity))
            rarity = parsedRarity;
		
		switch(element)
		{
			case Card.Element.Fire:
				fireCards.Add(cardID, rarity);
				break;
			case Card.Element.Water:
				waterCards.Add(cardID, rarity);
				break;
			case Card.Element.Wind:
				windCards.Add(cardID, rarity);
				break;
			case Card.Element.Earth:
				earthCards.Add(cardID, rarity);
				break;
		}
	}
}
