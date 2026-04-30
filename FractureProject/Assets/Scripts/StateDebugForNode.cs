using UnityEngine;

public class StateDebugForNode : MonoBehaviour, INodeStateListener
{
    private CrowdNode node;

    public void ListenNode(CrowdNode node)
    {
        this.node = node;
    }

    public void OnStateChange()
    {
        TriggerDisplay();
    }

    [ContextMenu("Display State")]
    public void TriggerDisplay() => Debug.Log(node.state);
}
