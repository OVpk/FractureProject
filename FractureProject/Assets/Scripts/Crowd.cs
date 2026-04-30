using System;
using System.Collections.Generic;
using UnityEngine;

public class Crowd : MonoBehaviour
{
    public CrowdNode rootNode { get; private set; }
    
    private HashSet<CrowdNode> allNodesSet = new HashSet<CrowdNode>();

    public CrowdNode[] allNodes { get; private set; }
    
    private void Awake()
    {
        rootNode = CreateNewBranch(gameObject.transform);
        
        allNodes = new CrowdNode[allNodesSet.Count];
        allNodesSet.CopyTo(allNodes);
        allNodesSet.Clear();
    }

    private void Start()
    {
        RefreshCrowdStates();
    }

    private void Update()
    {
        for (int i = 0; i < allNodes.Length; i++)
        {
            allNodes[i].CheckObstacles();
        }
    }

    private CrowdNode CreateNewBranch(Transform newBranchOrigin)
    {
        INodeStateListener stateListener = newBranchOrigin.GetComponent<INodeStateListener>();
        
        if (newBranchOrigin.childCount == 0)
        {
            return new ExitCrowdNode(newBranchOrigin.position, null, allNodesSet, stateListener);
        }
        
        IntermediateExitFlag intermediateExit = newBranchOrigin.GetComponent<IntermediateExitFlag>();
        if (intermediateExit != null)
        {
            return new IntermediateExitCrowdNode(
                newBranchOrigin.position, 
                GenerateNodeByChildren(newBranchOrigin),
                intermediateExit.GetNormalizedDirection(),
                allNodesSet
            );
        }
        
        return new CrowdNode(
            newBranchOrigin.position,
            GenerateNodeByChildren(newBranchOrigin),
            allNodesSet,
            stateListener
        );
    }
    
    private CrowdNode GenerateNodeByChildren(Transform origin, int nodeIndex = 0)
    {
        if (nodeIndex >= origin.childCount) return null;
        
        Transform nodeObject = origin.GetChild(nodeIndex);
        
        INodeStateListener stateListener = nodeObject.GetComponent<INodeStateListener>();

        if (nodeObject.childCount > 0)
        {
            CrowdNode[] nextOriginNodes = new CrowdNode[nodeObject.childCount];
            for (int i = 0; i < nodeObject.childCount; i++)
                nextOriginNodes[i] = CreateNewBranch(nodeObject.GetChild(i));
            
            SwitchCrowdNode newSwitchNode = 
                new SwitchCrowdNode(
                    nodeObject.position, 
                    GenerateNodeByChildren(origin, nodeIndex+1), 
                    nextOriginNodes,
                    allNodesSet,
                    stateListener
                    );
            
            SwitchNodeEvent eventLinked = nodeObject.GetComponent<SwitchNodeEvent>();
            if (eventLinked != null)
            {
                eventLinked.Bind(newSwitchNode, this);
            }
            
            return newSwitchNode;
        }
        
        if (nodeIndex == origin.childCount - 1) {
            return new ExitCrowdNode(nodeObject.position, null, allNodesSet, stateListener);
        }
        
        IntermediateExitFlag intermediateExit = nodeObject.GetComponent<IntermediateExitFlag>();
        if (intermediateExit != null)
        {
            return new IntermediateExitCrowdNode(
                nodeObject.position, 
                GenerateNodeByChildren(origin, nodeIndex + 1),
                intermediateExit.GetNormalizedDirection(),
                allNodesSet
            );
        }
        
        StopNodeEvent stopEvent = nodeObject.GetComponent<StopNodeEvent>();
        if (stopEvent != null)
        {
            StopCrowdNode stopNode = new StopCrowdNode(
                nodeObject.position, 
                GenerateNodeByChildren(origin, nodeIndex + 1),
                allNodesSet,
                stateListener
            );
        
            stopEvent.Bind(stopNode, this);
            return stopNode;
        }

        return new CrowdNode(nodeObject.position, GenerateNodeByChildren(origin, nodeIndex+1), allNodesSet, stateListener);
    }
    
    
    public void RefreshCrowdStates()
    {
        foreach (var node in allNodes) node.isConnectedToSource = false;

        CrowdNode current = rootNode;
    
        while (current != null)
        {
            current.isConnectedToSource = true;
            current = current.nextNode;
        }

        foreach (var node in allNodes)
        {
            bool hasSource = node.isConnectedToSource;
            bool hasExit = node.IsPathValid();

            if (hasSource && hasExit) 
            {
                node.state = CrowdState.Flowing;
            }
            else if (hasSource && !hasExit) 
            {
                node.state = CrowdState.Stagnant;
            }
            else if (!hasSource && hasExit) 
            {
                node.state = CrowdState.Empty; 
            }
            else
            {
                if (node.state == CrowdState.Flowing || node.state == CrowdState.Stagnant)
                {
                    node.state = CrowdState.Stagnant; 
                }
            }
        }
    }
    
}
