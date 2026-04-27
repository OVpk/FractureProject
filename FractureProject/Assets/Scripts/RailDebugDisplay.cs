using System.Collections.Generic;
using UnityEngine;

public class RailDebugDisplay : MonoBehaviour
{
    [Header("Editor Colors (Transform)")]
    private Color nodeColor = Color.blue;
    private Color startNodeColor = Color.red;
    private Color switchNodeColor = Color.magenta;
    private float nodeSize = 0.2f;
    
    #region RuntimeRender

    public List<Crowd> branchesOrigins = new List<Crowd>();
    
    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            foreach (Crowd crowd in branchesOrigins)
            {
                if (crowd != null && crowd.allNodes != null)
                {
                    DrawRuntimeCrowd(crowd);
                }
            }
        }
    }
    
    private void DrawRuntimeCrowd(Crowd crowd)
    {
        for (int i = 0; i < crowd.allNodes.Length; i++)
        {
            CrowdNode node = crowd.allNodes[i];
            if (node == null) continue;

            Color currentColor = Color.gray;

            if (node.state == CrowdState.Flowing)
            {
                currentColor = Color.green;
            }
            else if (node.state == CrowdState.Stagnant)
            {
                currentColor = Color.red;
            }

            GizmosRuntimeCustom.color = currentColor;

            if (node is ExitCrowdNode)
            {
                GizmosRuntimeCustom.color = Color.yellow;
                GizmosRuntimeCustom.DrawCube(node.position, Vector3.one * nodeSize);
            }
            else if (node is SwitchCrowdNode)
            {
                GizmosRuntimeCustom.DrawWireCube(node.position, Vector3.one * (nodeSize * 1.5f));
            }
            else if (node is StopCrowdNode)
            {
                GizmosRuntimeCustom.DrawSphere(node.position, nodeSize * 1.2f);
            }
            else
            {
                GizmosRuntimeCustom.DrawSphere(node.position, nodeSize);
            }
            
            if (node is ExitCrowdNode) continue;
            
            CrowdNode targetNode = node.nextNode;
            if (node is StopCrowdNode stopNode) targetNode = stopNode.GetHiddenNode();
            if (node is SwitchCrowdNode switchNode)
            {
                targetNode = switchNode.GetHiddenNode();
                foreach (CrowdNode linkedOrigin in switchNode.nextOriginNodes)
                {
                    DrawSegment(node, linkedOrigin);
                }
            }
            
            DrawSegment(node, targetNode);
        }
    }

    private void DrawSegment(CrowdNode startNode, CrowdNode targetNode)
    {
        if (targetNode == null) return;
        
        Color currentColor = Color.gray;
            
        if (targetNode.state == CrowdState.Flowing)
        {
            currentColor = Color.green;
        }
        else if (targetNode.state == CrowdState.Stagnant)
        {
            currentColor = Color.red;
        }
            
        if (targetNode.state == CrowdState.Empty)
        {
            GizmosRuntimeCustom.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        }
        else
        {
            GizmosRuntimeCustom.color = currentColor;
        }

        DrawLineWithArrow(startNode.position, targetNode.position);
    }
    
    private void DrawLineWithArrow(Vector3 start, Vector3 end)
    {
        GizmosRuntimeCustom.DrawLine(start, end);
        Vector3 direction = (start - end).normalized;
        
        if (direction != Vector3.zero) 
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float arrowSize = 0.15f;
            GizmosRuntimeCustom.DrawLine(end, end + direction * arrowSize + right * arrowSize);
            GizmosRuntimeCustom.DrawLine(end, end + direction * arrowSize - right * arrowSize);
        }
    }

    #endregion

    #region EditorRender

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            foreach (Crowd crowd in branchesOrigins)
            {
                if (crowd != null && crowd.transform != null)
                {
                    DrawNewBranch(crowd.transform);
                }
            }
        }
    }
    
    private void DrawNewBranch(Transform originNode)
    {
        Gizmos.color = startNodeColor;
        Gizmos.DrawSphere(originNode.position, nodeSize);
        
        if (originNode.childCount > 0) 
            DrawChildrenNodes(originNode);
    }

    private void DrawChildrenNodes(Transform parent)
    {
        Gizmos.color = nodeColor;
        
        if (parent.GetChild(0) != null) 
            Gizmos.DrawLine(parent.position, parent.GetChild(0).position);
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform currentPoint = parent.GetChild(i);
            
            if(currentPoint.gameObject.activeSelf == false) break;

            if (currentPoint.childCount > 0)
            {
                for (int j = 0; j < currentPoint.childCount; j++)
                {
                    if(currentPoint.GetChild(j).gameObject.activeSelf == false) continue;
                    
                    DrawNewBranch(currentPoint.GetChild(j));
                    
                    Gizmos.color = switchNodeColor;
                    Gizmos.DrawLine(currentPoint.position, currentPoint.GetChild(j).position);
                }
                Gizmos.color = switchNodeColor;
                Gizmos.DrawSphere(currentPoint.position, nodeSize);
            }
            else
            {
                Gizmos.color = nodeColor;
                Gizmos.DrawSphere(currentPoint.position, nodeSize);
            }
            
            Transform previousPoint = (i - 1) >= 0 ? parent.GetChild(i - 1) : null;
            
            Gizmos.color = nodeColor;
            if (previousPoint != null) 
                Gizmos.DrawLine(previousPoint.position, currentPoint.position);
        }
    }

    #endregion
}