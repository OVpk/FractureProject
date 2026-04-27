using UnityEngine;
using UnityEngine.U2D;

public class CrowdDisplayer : MonoBehaviour
{
    struct CharacterData {
        public Vector3 randomOffset;
        public float absoluteDistance;
        public Vector4 uvRect;
    }

    public Crowd targetCrowd; 
    
    public SpriteAtlas atlas;
    public Mesh characterMesh; 
    public Material crowdMaterialTemplate; 
    
    public int characterCount;
    public float moveSpeed; 
    
    private ComputeBuffer crowdBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer waypointBuffer; 
    private Vector4[] waypointPositions;
    
    private float globalOffset = 0f;
    private MaterialPropertyBlock propertyBlock;
    
    private Sprite[] characters;
    private int currentWaypointCount = 0;
    
    private float lastKnownPathLength = 0f;
    private CharacterData[] cpuData;

    void Start()
    {
        if (targetCrowd == null) {
            Debug.LogError("Aucune foulee");
            return;
        }
        InitializeCrowd();
    }

    void InitializeCrowd()
    {
        if (atlas != null)
        {
            characters = new Sprite[atlas.spriteCount];
            atlas.GetSprites(characters);
        }
        else if (characters == null || characters.Length == 0)
        {
            Debug.LogError("Aucun Atlas");
            return;
        }
        
        propertyBlock = new MaterialPropertyBlock();

        float currentPathLength = CalculatePathLength();
        lastKnownPathLength = currentPathLength;
        
        cpuData = new CharacterData[characterCount];
        for (int i = 0; i < characterCount; i++)
        {
            Sprite s = characters[Random.Range(0, characters.Length)];
            Rect r = s.textureRect;
            float texW = s.texture.width;
            float texH = s.texture.height;

            cpuData[i] = new CharacterData {
                randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 10f), 0),
                absoluteDistance = Random.value * currentPathLength,
                uvRect = new Vector4(r.x / texW, r.y / texH, r.width / texW, r.height / texH)
            };
        }

        crowdBuffer = new ComputeBuffer(characterCount, 32);
        crowdBuffer.SetData(cpuData);
        propertyBlock.SetBuffer("_CrowdBuffer", crowdBuffer);
        
        if (characters.Length > 0 && characters[0] != null) {
            propertyBlock.SetTexture("_MainTex", characters[0].texture);
        }

        int maxPossibleNodes = targetCrowd.allNodes.Length;
        waypointPositions = new Vector4[maxPossibleNodes];
        waypointBuffer = new ComputeBuffer(maxPossibleNodes, 16); 
        
        propertyBlock.SetBuffer("_WaypointBuffer", waypointBuffer);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { characterMesh.GetIndexCount(0), (uint)characterCount, 0, 0, 0 };
        argsBuffer.SetData(args);
    }

    void Update()
    {
        if (propertyBlock == null || crowdMaterialTemplate == null || targetCrowd.rootNode == null) return;

        currentWaypointCount = 0;
        float accumulatedDistance = 0f; 
        
        CrowdNode currentNode = targetCrowd.rootNode;
        Vector3 lastPosition = currentNode.position;
        
        while (currentNode != null)
        {
            float distanceToThisNode = Vector3.Distance(lastPosition, currentNode.position);
            accumulatedDistance += distanceToThisNode;

            waypointPositions[currentWaypointCount] = new Vector4(
                currentNode.position.x, 
                currentNode.position.y, 
                currentNode.position.z, 
                accumulatedDistance
            );
            
            lastPosition = currentNode.position;
            currentWaypointCount++;

            if (currentWaypointCount >= waypointPositions.Length) break;

            currentNode = currentNode.nextNode;
        }

        if (currentWaypointCount < 2) return;

        waypointBuffer.SetData(waypointPositions);
        propertyBlock.SetInt("_WaypointCount", currentWaypointCount);
        propertyBlock.SetFloat("_TotalPathLength", accumulatedDistance);
        
        if (Mathf.Abs(accumulatedDistance - lastKnownPathLength) > 0.01f)
        {
            RescaleAbsoluteDistances(lastKnownPathLength, accumulatedDistance);
            lastKnownPathLength = accumulatedDistance;
        }

        if (targetCrowd.rootNode.state == CrowdState.Flowing) 
        {
            globalOffset += Time.deltaTime * moveSpeed;
            
            propertyBlock.SetFloat("_GlobalOffset", globalOffset);
        }
        
        Graphics.DrawMeshInstancedIndirect(
            characterMesh, 
            0, 
            crowdMaterialTemplate, 
            new Bounds(Vector3.zero, Vector3.one * 1000), 
            argsBuffer,
            0,
            propertyBlock 
        );
    }
    
    float CalculatePathLength()
    {
        float total = 0f;
        CrowdNode current = targetCrowd.rootNode;
        Vector3 last = current.position;

        while (current != null)
        {
            total += Vector3.Distance(last, current.position);
            last = current.position;
            current = current.nextNode;
        }
        return total;
    }
    
    void RescaleAbsoluteDistances(float oldLength, float newLength)
    {
        if (newLength >= oldLength) return;

        crowdBuffer.GetData(cpuData);
        for (int i = 0; i < characterCount; i++)
        {
            cpuData[i].absoluteDistance = Mathf.Repeat(
                cpuData[i].absoluteDistance, 
                newLength
            );
        }
        crowdBuffer.SetData(cpuData);
    }

    void OnDisable() 
    {
        if (crowdBuffer != null) crowdBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (waypointBuffer != null) waypointBuffer.Release(); 
    }
}