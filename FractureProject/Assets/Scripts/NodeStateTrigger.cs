using UnityEngine;
using UnityEngine.Events;

public class NodeStateTrigger : MonoBehaviour, INodeStateListener
{
    [SerializeField] private UnityEvent action;

    [SerializeField] private CrowdState targetState;

    [SerializeField] private bool oneTimeOnly;
    
    private CrowdNode node;
    
    public void ListenNode(CrowdNode node)
    {
        this.node = node;
    }

    public void OnStateChange()
    {
        if (node.state == targetState)
            action.Invoke();

        if (oneTimeOnly)
            node.DisconnectListener();
    }
}
