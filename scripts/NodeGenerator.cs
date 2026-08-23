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
    private Overworld _overworld; // Class-level reference to Overworld[cite: 1]

    public override void _Ready()
    {
        // Fetch and store the Overworld reference[cite: 1]
        _overworld = GetParent<Overworld>();
        if (_overworld != null)
        {
            _overworld.GenerateSeed();
            _rng.Seed = (ulong)_overworld.Seed;
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

            float startY = -(numNodes - 1) * YSpacing / 2f;

            for (int row = 0; row < numNodes; row++)
            {
                OverworldNode node = NodePrefab.Instantiate<OverworldNode>();
                node.Position = new Vector2(col * XSpacing, startY + row * YSpacing);

                if (col == 0)
                {
                    node.isCardGain = true;
                }
                else if (col == 1)
                {
                    node.isEnergy = true;
                }
                else if (col < InitialSafeNodes)
                {
                    if (_rng.RandiRange(0, 1) == 0) node.isEnergy = true;
                    else node.isCardGain = true;
                }
                else
                {                                       //to add new node type to generator, add it here
                    int rand = _rng.RandiRange(0, 4);  //add one to the max arg, and add a new case that sets its type true (HORRIB)
                    if (rand == 0) node.isEnergy = true;
                    else if (rand == 1) node.isCardGain = true;
                    else if (rand == 2) node.isCardCombine = true;
                    else node.isDefence = true;
                }

                AddChild(node);
                
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

            foreach (var node in currentColNodes) node.nextNodes = new OverworldNode[0];
            foreach (var node in nextColNodes) node.previousNodes = new OverworldNode[0];

            for (int i = 0; i < currentColNodes.Count; i++)
            {
                var source = currentColNodes[i];
                int targetIndex = Mathf.Clamp(i, 0, nextColNodes.Count - 1);
                AddLink(source, nextColNodes[targetIndex]);

                if (_rng.Randf() < 0.5f)
                {
                    int offset = _rng.RandiRange(0, 1) == 0 ? -1 : 1;
                    int secondaryIndex = targetIndex + offset;
                    if (secondaryIndex >= 0 && secondaryIndex < nextColNodes.Count)
                    {
                        AddLink(source, nextColNodes[secondaryIndex]);
                    }
                }
            }

            foreach (var target in nextColNodes)
            {
                if (target.previousNodes.Length == 0)
                {
                    int closestIndex = 0;
                    float minDistance = float.MaxValue;
                    for (int i = 0; i < currentColNodes.Count; i++)
                    {
                        float dist = Mathf.Abs(currentColNodes[i].Position.Y - target.Position.Y);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestIndex = i;
                        }
                    }
                    AddLink(currentColNodes[closestIndex], target);
                }
            }
        }
    }

    private void AddLink(OverworldNode source, OverworldNode target)
    {
        var nextList = new List<OverworldNode>(source.nextNodes);
        if (!nextList.Contains(target)) nextList.Add(target);
        source.nextNodes = nextList.ToArray();

        var prevList = new List<OverworldNode>(target.previousNodes);
        if (!prevList.Contains(source)) prevList.Add(source);
        target.previousNodes = prevList.ToArray();
    }

    public override void _Process(double delta)
    {
        // Request a redraw every frame so line visibility updates dynamically with InScene changes[cite: 1]
        QueueRedraw();

        bool col0Visited = false;
        foreach (var node in _columns[0])
        {
            if ((bool)node.Get("_visisted")) col0Visited = true;
        }

        for (int col = 0; col < TreeDepth; col++)
        {
            foreach (var node in _columns[col])
            {
                bool isVisited = (bool)node.Get("_visisted"); 
                
                if (isVisited)
                {
                    node.Set("_visitable", false);
                    continue;
                }

                bool shouldBeVisitable = false;

                if (col == 0)
                {
                    shouldBeVisitable = !col0Visited;
                }
                else
                {
                    bool hasVisitedThisCol = false;
                    foreach (var peer in _columns[col])
                    {
                        if ((bool)peer.Get("_visisted")) hasVisitedThisCol = true;
                    }

                    bool hasVisitedFutureCol = false;
                    for (int futureCol = col + 1; futureCol < TreeDepth; futureCol++)
                    {
                        foreach (var fNode in _columns[futureCol])
                        {
                            if ((bool)fNode.Get("_visisted")) hasVisitedFutureCol = true;
                        }
                    }

                    bool hasVisitedPreviousCol = false;
                    if (node.previousNodes != null)
                    {
                        foreach (var prevNode in node.previousNodes)
                        {
                            if ((bool)prevNode.Get("_visisted"))
                            {
                                hasVisitedPreviousCol = true;
                                break;
                            }
                        }
                    }

                    if (hasVisitedPreviousCol && !hasVisitedThisCol && !hasVisitedFutureCol)
                    {
                        shouldBeVisitable = true;
                    }
                }

                node.Set("_visitable", shouldBeVisitable);
            }
        }
    }

    public override void _Draw()
    {
        // Hide lines if Overworld is in a scene[cite: 1]
        if (_overworld != null && _overworld.InScene) return;

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