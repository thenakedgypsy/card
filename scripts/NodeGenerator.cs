using Godot;
using System;
using System.Collections.Generic;

public partial class NodeGenerator : Node2D
{
    [ExportCategory("Tree Generation")]
    [Export] public PackedScene NodePrefab;
    [Export] public int TreeDepth = 15;
    [Export] public int InitialSafeNodes = 6; // First 6 columns will only be Energy or Card Gains
    
    [ExportCategory("Tree Layout")]
    [Export] public int MinNodesPerColumn = 2;
    [Export] public int MaxNodesPerColumn = 4;
    [Export] public float XSpacing = 150f;
    [Export] public float YSpacing = 100f;

    private List<List<OverworldNode>> _columns = new List<List<OverworldNode>>();
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        // Fetch the seed created by the parent Overworld
        var overworld = GetParent<Overworld>();
        if (overworld != null)
        {
            _rng.Seed = (ulong)overworld.Seed;
        }
        else
        {
            _rng.Randomize();
        }

        GenerateTree();
        LinkNodes();
    }

    private void GenerateTree()
    {
        for (int col = 0; col < TreeDepth; col++)
        {
            int numNodes = _rng.RandiRange(MinNodesPerColumn, MaxNodesPerColumn);
            List<OverworldNode> currentColumn = new List<OverworldNode>();

            // Calculate starting Y to center the column vertically
            float startY = -(numNodes - 1) * YSpacing / 2f;

            for (int row = 0; row < numNodes; row++)
            {
                OverworldNode node = NodePrefab.Instantiate<OverworldNode>();
                
                // Position nodes from left to right across the screen
                node.Position = new Vector2(col * XSpacing, startY + row * YSpacing);

                // Set initial properties before AddChild triggers _Ready
                if (col < InitialSafeNodes)
                {
                    // First exported # of choices are Card or Energy Gain
                    if (_rng.RandiRange(0, 1) == 0) node.isEnergy = true;
                    else node.isCardGain = true;
                }
                else
                {
                    // The rest are completely random
                    int rand = _rng.RandiRange(0, 2);
                    if (rand == 0) node.isEnergy = true;
                    else if (rand == 1) node.isCardGain = true;
                    else node.isDefence = true;
                }

                AddChild(node);
                
                // Force nodes after the first column to be unvisitable by default
                // Using Set() bypasses the private access modifier of _visitable
                if (col > 0)
                {
                    node.Set("_visitable", false);
                }

                currentColumn.Add(node);
            }
            _columns.Add(currentColumn);
        }
    }

    private void LinkNodes()
    {
        for (int col = 0; col < TreeDepth - 1; col++)
        {
            var currentColNodes = _columns[col];
            var nextColNodes = _columns[col + 1];

            // Initialize connection arrays
            foreach (var node in currentColNodes) node.nextNodes = new OverworldNode[0];
            foreach (var node in nextColNodes) node.previousNodes = new OverworldNode[0];

            // Ensure every node in the current column connects to at least one in the next
            foreach (var node in currentColNodes)
            {
                var target = nextColNodes[_rng.RandiRange(0, nextColNodes.Count - 1)];
                AddLink(node, target);
            }

            // Ensure every node in the next column has at least one incoming connection
            foreach (var target in nextColNodes)
            {
                if (target.previousNodes.Length == 0)
                {
                    var source = currentColNodes[_rng.RandiRange(0, currentColNodes.Count - 1)];
                    AddLink(source, target);
                }
            }
        }
    }

    private void AddLink(OverworldNode source, OverworldNode target)
    {
        // Resize and append to nextNodes
        var nextList = new List<OverworldNode>(source.nextNodes);
        if (!nextList.Contains(target)) nextList.Add(target);
        source.nextNodes = nextList.ToArray();

        // Resize and append to previousNodes
        var prevList = new List<OverworldNode>(target.previousNodes);
        if (!prevList.Contains(source)) prevList.Add(source);
        target.previousNodes = prevList.ToArray();
    }

    public override void _Process(double delta)
    {
        // Continously check if a node has been visited to unlock its linked nextNodes
        for (int col = 0; col < TreeDepth; col++)
        {
            foreach (var node in _columns[col])
            {
                // Using Get() bypasses the private access modifier of _visisted
                bool isVisited = (bool)node.Get("_visisted"); 
                
                if (isVisited && node.nextNodes != null)
                {
                    foreach (var nextNode in node.nextNodes)
                    {
                        nextNode.Set("_visitable", true);
                    }
                }
            }
        }
    }

    // Optional: Draw the path lines between linked nodes for visualization
    public override void _Draw()
    {
        if (_columns == null || _columns.Count == 0) return;

        foreach (var col in _columns)
        {
            foreach (var node in col)
            {
                if (node.nextNodes != null)
                {
                    foreach (var next in node.nextNodes)
                    {
                        DrawLine(node.Position, next.Position, new Color(0.5f, 0.5f, 0.5f, 0.8f), 4f);
                    }
                }
            }
        }
    }
}