using UnityEngine;
using UnityEngine.U2D;
using System.Collections.Generic;

public class CrowdDisplayer : MonoBehaviour
{
    struct CharacterData {
        public Vector3 randomOffset;   // 12 bytes
        public float absoluteDistance; // 4 bytes
        public Vector4 uvRect;         // 16 bytes
        public int pathIndex;          // 4 bytes
        public Vector3 _padding;       // 12 bytes
        // Total : 48 bytes
    }

    struct PathInfo {
        public int waypointCount;  // 4 bytes
        public float totalLength;  // 4 bytes
        public Vector2 _padding;   // 8 bytes
        // Total : 16 bytes
    }

    public Crowd targetCrowd;
    public SpriteAtlas atlas;
    public Mesh characterMesh;
    public Material crowdMaterialTemplate;
    public int characterCount;
    public float moveSpeed;

    private ComputeBuffer crowdBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer allPathsBuffer;
    private ComputeBuffer pathInfoBuffer;

    private Vector4[] waypointPositions;
    private int currentWaypointCount = 0;

    private float globalOffset = 0f;
    private MaterialPropertyBlock propertyBlock;
    private Sprite[] characters;

    private float lastKnownPathLength = 0f;
    private CharacterData[] cpuData;

    private List<PathInfo> pathInfoList = new List<PathInfo>();
    private const int MAX_PATHS = 16;

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
                uvRect = new Vector4(r.x / texW, r.y / texH, r.width / texW, r.height / texH),
                pathIndex = 0
            };
        }

        crowdBuffer = new ComputeBuffer(characterCount, 48);
        crowdBuffer.SetData(cpuData);
        propertyBlock.SetBuffer("_CrowdBuffer", crowdBuffer);

        if (characters.Length > 0 && characters[0] != null)
            propertyBlock.SetTexture("_MainTex", characters[0].texture);

        int maxNodes = targetCrowd.allNodes.Length;
        waypointPositions = new Vector4[maxNodes];

        // Un seul buffer de waypoints partagé par tous les chemins
        allPathsBuffer = new ComputeBuffer(maxNodes, 16);
        pathInfoBuffer = new ComputeBuffer(MAX_PATHS, 16);

        propertyBlock.SetBuffer("_AllPathsBuffer", allPathsBuffer);
        propertyBlock.SetBuffer("_PathInfoBuffer", pathInfoBuffer);

        // Index 0 = chemin actif, sera écrasé chaque frame
        pathInfoList.Add(new PathInfo());

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

        // Raccourcissement → snapshot avant d'écraser le buffer
        if (accumulatedDistance < lastKnownPathLength - 0.01f)
        {
            CreateHistoricalSnapshot(accumulatedDistance);
            lastKnownPathLength = accumulatedDistance;
        }
        else if (Mathf.Abs(accumulatedDistance - lastKnownPathLength) > 0.01f)
        {
            lastKnownPathLength = accumulatedDistance;
        }

        // Upload chemin actif — tous les snapshots lisent ce même buffer
        allPathsBuffer.SetData(waypointPositions, 0, 0, currentWaypointCount);

        pathInfoList[0] = new PathInfo {
            waypointCount = currentWaypointCount,
            totalLength = accumulatedDistance
        };
        pathInfoBuffer.SetData(pathInfoList.ToArray());
        propertyBlock.SetInt("_PathCount", pathInfoList.Count);

        MigrateFinishedCharacters();
        CleanupEmptySnapshots();

        if (targetCrowd.rootNode.state == CrowdState.Flowing)
        {
            globalOffset += Time.deltaTime * moveSpeed;

            if (globalOffset > lastKnownPathLength)
                globalOffset -= lastKnownPathLength;

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

    void CreateHistoricalSnapshot(float newLength)
    {
        // Bake globalOffset dans cpuData avant de créer le snapshot
        for (int i = 0; i < characterCount; i++)
        {
            if (cpuData[i].pathIndex != 0) continue;

            float baked = Mathf.Repeat(
                cpuData[i].absoluteDistance + globalOffset,
                lastKnownPathLength
            );
            cpuData[i].absoluteDistance = baked;

            // Au-delà du nouveau chemin → assigné au snapshot
            if (baked > newLength)
                cpuData[i].pathIndex = pathInfoList.Count;
        }
        globalOffset = 0f;

        // Snapshot : mêmes waypoints (rootNode fixe), juste waypointCount et totalLength différents
        pathInfoList.Add(new PathInfo {
            waypointCount = currentWaypointCount,
            totalLength = lastKnownPathLength
        });

        pathInfoBuffer.SetData(pathInfoList.ToArray());
        crowdBuffer.SetData(cpuData);
    }

    void MigrateFinishedCharacters()
    {
        bool changed = false;
        for (int i = 0; i < characterCount; i++)
        {
            if (cpuData[i].pathIndex == 0) continue;

            PathInfo info = pathInfoList[cpuData[i].pathIndex];
            if (cpuData[i].absoluteDistance >= info.totalLength)
            {
                cpuData[i].absoluteDistance = 0f;
                cpuData[i].pathIndex = 0;
                changed = true;
            }
        }
        if (changed) crowdBuffer.SetData(cpuData);
    }

    void CleanupEmptySnapshots()
    {
        for (int p = pathInfoList.Count - 1; p >= 1; p--)
        {
            bool hasUsers = false;
            for (int i = 0; i < characterCount; i++)
                if (cpuData[i].pathIndex == p) { hasUsers = true; break; }

            if (!hasUsers)
            {
                pathInfoList.RemoveAt(p);

                for (int i = 0; i < characterCount; i++)
                    if (cpuData[i].pathIndex > p) cpuData[i].pathIndex--;

                crowdBuffer.SetData(cpuData);
                pathInfoBuffer.SetData(pathInfoList.ToArray());
            }
        }
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

    void OnDisable()
    {
        if (crowdBuffer != null) crowdBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (allPathsBuffer != null) allPathsBuffer.Release();
        if (pathInfoBuffer != null) pathInfoBuffer.Release();
    }
}