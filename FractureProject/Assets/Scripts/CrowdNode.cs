using System.Collections.Generic;
using UnityEngine;

public enum CrowdState 
{ 
    Empty,
    Flowing,
    Stagnant
}

public class CrowdNode
{
    public virtual CrowdNode nextNode { get; private set; }
    public Vector3 position;

    private CrowdState _state;
    public CrowdState state
    {
        get => this._state;
        set
        {
            this._state = value;
            listener?.OnStateChange();
        }
    }
    private INodeStateListener listener;
    
    public bool isConnectedToSource = false;

    public CrowdNode(Vector3 position, CrowdNode nextNode, HashSet<CrowdNode> track = null, INodeStateListener stateListener = null)
    {
        this.position = position;
        this.nextNode = nextNode;
        
        track?.Add(this);
        
        stateListener?.ListenNode(this);
        listener = stateListener;
    }
    
    public bool IsPathValid()
    {
        if (this is ExitCrowdNode) return true;
        if (nextNode == null) return false;
        return nextNode.IsPathValid();
    }

    public virtual void CheckObstacles()
    {
        if (this is ExitCrowdNode) return;
        
        CrowdNode targetNode = this.nextNode;
        if (this is StopCrowdNode stopNode) targetNode = stopNode.GetHiddenNode();
        if (this is SwitchCrowdNode switchNode) targetNode = switchNode.GetHiddenNode();
        
        CheckObstacles(targetNode);
    }

    public void CheckObstacles(CrowdNode targetNode)
    {
        if (state == CrowdState.Empty) return;
        if (targetNode == null) return;

        if (Physics.Linecast(this.position, targetNode.position, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Player"))
            {
                if (targetNode.state == CrowdState.Flowing)
                {
                    Player.instance.SetCrowdToFollow(this);
                }
                else if (targetNode.state == CrowdState.Stagnant)
                {
                    Player.instance.BlockByCrowd();
                }
            }
        }
    }

    public void DisconnectListener() => listener = null;
}

public class SwitchCrowdNode : CrowdNode
{
    public CrowdNode[] nextOriginNodes;
    public int currentDirectionIndex = -1;
    
    public override CrowdNode nextNode 
    {
        get 
        {
            if (currentDirectionIndex >= 0 && currentDirectionIndex < nextOriginNodes.Length)
                return nextOriginNodes[currentDirectionIndex];
            
            return base.nextNode;
        }
    }
    
    public SwitchCrowdNode(Vector3 position, CrowdNode nextNode, CrowdNode[] nextOriginNodes, HashSet<CrowdNode> track = null, INodeStateListener stateListener = null) 
        : base(position, nextNode, track, stateListener)
    {
        this.nextOriginNodes = nextOriginNodes;
    }
    
    public void Switch(int nbOfSwitches)
    {
        int totalStates = nextOriginNodes.Length + 1; //include 0 as null state
        
        // calcul index in range [0 à size]
        int virtualIndex = currentDirectionIndex + 1;
        
        virtualIndex = (virtualIndex + nbOfSwitches) % totalStates;
        
        // convert new index in range [-1 à size-1]
        currentDirectionIndex = virtualIndex - 1;
    }
    
    public CrowdNode GetHiddenNode() => base.nextNode;

    public override void CheckObstacles()
    {
        base.CheckObstacles();
        foreach (CrowdNode linkedOrigin in nextOriginNodes)
        {
            CheckObstacles(linkedOrigin);
        }
    }
}

public class ExitCrowdNode : CrowdNode
{
    public ExitCrowdNode(Vector3 position, CrowdNode nextNode, HashSet<CrowdNode> track = null, INodeStateListener stateListener = null) 
        : base(position, nextNode, track, stateListener) { }
}

public class StopCrowdNode : CrowdNode
{
    public override CrowdNode nextNode => isStopped ? null : base.nextNode;

    public bool isStopped = false;
    
    public StopCrowdNode(Vector3 position, CrowdNode nextNode, HashSet<CrowdNode> track = null, INodeStateListener stateListener = null) 
        : base(position, nextNode, track, stateListener) { }

    public CrowdNode GetHiddenNode() => base.nextNode;
}

public class IntermediateExitCrowdNode : CrowdNode
{
    public Vector2 ejectionDirection;

    public IntermediateExitCrowdNode(Vector3 position, CrowdNode nextNode, Vector2 ejectionDirection, HashSet<CrowdNode> track = null) 
        : base(position, nextNode, track)
    {
        this.ejectionDirection = ejectionDirection;
    }
}