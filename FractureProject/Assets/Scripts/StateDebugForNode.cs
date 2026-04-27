using UnityEngine;

public class StateDebugForNode : MonoBehaviour
{
    private CrowdNode node;
    
    public void Bind(CrowdNode node)
    {
        this.node = node;
    }
    
    [ContextMenu("Display State")]
    public void TriggerDisplay() => Debug.Log(node.state);
}
