using UnityEngine;
using UnityEngine.U2D;

public class CrowdDisplayer : MonoBehaviour
{
    public struct CharacterData {
        public Vector3 randomOffset;
        public float absoluteDistance;
        public Vector4 uvRect;
    }
    
    public struct SymbolData {
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
    public float catchUpSpeed;
    public float characterRotationY = 0f;

    [Header("Dispersion Settings")]
    public float dispersionDelay = 3f;
    public float dispersionDuration = 2f;
    public float dispersionDistance = 5f;

    [Header("Density Smoothing")]
    public float densitySmoothSpeed = 1f;
    
    [Header("Symbol Settings")]
    public SpriteAtlas symbolAtlas;
    public Mesh symbolMesh;
    public Material symbolMaterialTemplate;
    public int symbolCount = 20;
    
    [Header("Symbol Settings (Joie)")]
    public SpriteAtlas symbolJoyAtlas;

    private ComputeBuffer symbolBuffer;
    private ComputeBuffer symbolJoyBuffer;
    private Texture2D angerTexture;
    private Texture2D joyTexture;

    private ComputeBuffer symbolArgsBuffer;
    private Material runtimeSymbolMaterial;
    private SymbolData[] symbolCpuData;
    
    private float[] targetDistances;
    private bool isSmoothingDensity = false;
    
    private ComputeBuffer crowdBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer waypointBuffer; 
    private Vector4[] waypointPositions;
    private CrowdNode[] currentPathNodes;
    
    private float globalOffset = 0f;
    private MaterialPropertyBlock propertyBlock;
    
    private Material runtimeMaterial; 
    
    private Sprite[] characters;
    private int currentWaypointCount = 0;
    
    private float currentPathLength = 0f; 
    private CharacterData[] cpuData;
    private bool hasStartedLooping = false;
    
    private MaterialPropertyBlock symbolPropertyBlock;
    
    private float resumeTime = 0f;
    private bool wasMoving = false;
    private bool wasCruising = false;
    

    void Start()
    {
        if (targetCrowd == null) return;
        InitializeCrowd();
        targetCrowd.OnCrowdPathChanged += UpdatePathData;
        UpdatePathData();
    }

    void InitializeCrowd()
    {
        if (atlas != null) { characters = new Sprite[atlas.spriteCount]; atlas.GetSprites(characters); }
        
        propertyBlock = new MaterialPropertyBlock();
        float initialLength = CalculateInitialPathLength();
        
        cpuData = new CharacterData[characterCount];
        targetDistances = new float[characterCount];

        for (int i = 0; i < characterCount; i++)
        {
            Sprite s = characters[Random.Range(0, characters.Length)];
            Rect r = s.textureRect;
            cpuData[i] = new CharacterData {
                randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 10f), 0),
                absoluteDistance = Random.value * initialLength,
                uvRect = new Vector4(r.x / s.texture.width, r.y / s.texture.height, r.width / s.texture.width, r.height / s.texture.height)
            };
        }

        crowdBuffer = new ComputeBuffer(characterCount, 32);
        crowdBuffer.SetData(cpuData);
        propertyBlock.SetBuffer("_CrowdBuffer", crowdBuffer);

        int maxPossibleNodes = targetCrowd.allNodes.Length;
        waypointPositions = new Vector4[maxPossibleNodes];
        currentPathNodes = new CrowdNode[maxPossibleNodes];
        waypointBuffer = new ComputeBuffer(maxPossibleNodes, 16); 
        propertyBlock.SetBuffer("_WaypointBuffer", waypointBuffer);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(new uint[5] { characterMesh.GetIndexCount(0), (uint)characterCount, 0, 0, 0 });
        
        propertyBlock.SetFloat("_RotationY", characterRotationY);

        if (crowdMaterialTemplate != null)
        {
            runtimeMaterial = new Material(crowdMaterialTemplate);
            runtimeMaterial.SetBuffer("_CrowdBuffer", crowdBuffer);
            runtimeMaterial.SetBuffer("_WaypointBuffer", waypointBuffer);
            
            if (characters.Length > 0 && characters[0] != null) 
            {
                propertyBlock.SetTexture("_MainTex", characters[0].texture);
                runtimeMaterial.SetTexture("_MainTex", characters[0].texture);
            }
        }
        
        if (symbolMaterialTemplate != null && symbolAtlas != null && symbolCount > 0)
        {
            float symbolSpacing = initialLength / Mathf.Max(1, symbolCount);
            
            Sprite[] symbolSprites = new Sprite[symbolAtlas.spriteCount]; 
            symbolAtlas.GetSprites(symbolSprites);
            if (symbolSprites.Length > 0 && symbolSprites[0] != null) angerTexture = symbolSprites[0].texture;
    
            symbolCpuData = new SymbolData[symbolCount];
            for (int i = 0; i < symbolCount; i++)
            {
                Sprite s = symbolSprites[Random.Range(0, symbolSprites.Length)];
                Rect r = s.textureRect;
                symbolCpuData[i] = new SymbolData {
                    randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), Random.Range(0f, 100f)),
                    absoluteDistance = (i * symbolSpacing) + Random.Range(-symbolSpacing * 0.2f, symbolSpacing * 0.2f),
                    uvRect = new Vector4(r.x / s.texture.width, r.y / s.texture.height, r.width / s.texture.width, r.height / s.texture.height)
                };
            }
            symbolBuffer = new ComputeBuffer(symbolCount, 32);
            symbolBuffer.SetData(symbolCpuData);

            if (symbolJoyAtlas != null)
            {
                Sprite[] joySprites = new Sprite[symbolJoyAtlas.spriteCount];
                symbolJoyAtlas.GetSprites(joySprites);
                if (joySprites.Length > 0 && joySprites[0] != null) joyTexture = joySprites[0].texture;

                SymbolData[] symbolJoyCpuData = new SymbolData[symbolCount];
                for (int i = 0; i < symbolCount; i++)
                {
                    Sprite s = joySprites[Random.Range(0, joySprites.Length)];
                    Rect r = s.textureRect;
                    symbolJoyCpuData[i] = new SymbolData {
                        randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), Random.Range(0f, 100f)),
                        absoluteDistance = (i * symbolSpacing) + Random.Range(-symbolSpacing * 0.2f, symbolSpacing * 0.2f),
                        uvRect = new Vector4(r.x / s.texture.width, r.y / s.texture.height, r.width / s.texture.width, r.height / s.texture.height)
                    };
                }
                symbolJoyBuffer = new ComputeBuffer(symbolCount, 32);
                symbolJoyBuffer.SetData(symbolJoyCpuData);
            }

            symbolArgsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            symbolArgsBuffer.SetData(new uint[5] { symbolMesh.GetIndexCount(0), (uint)symbolCount, 0, 0, 0 });

            symbolPropertyBlock = new MaterialPropertyBlock();
            symbolPropertyBlock.SetFloat("_RotationY", characterRotationY);

            runtimeSymbolMaterial = new Material(symbolMaterialTemplate);
            runtimeSymbolMaterial.SetBuffer("_WaypointBuffer", waypointBuffer);
        }
    }

    private void UpdatePathData()
    {
        if (targetCrowd == null || targetCrowd.rootNode == null) return;

        int newWaypointCount = 0;
        float newAccumulatedDistance = 0f; 
        CrowdNode currentNode = targetCrowd.rootNode; 
        Vector3 lastPosition = currentNode.position;
        
        Vector4[] newWaypoints = new Vector4[waypointPositions.Length];
        CrowdNode[] newPathNodes = new CrowdNode[waypointPositions.Length];
        
        float commonPathLength = 0f;
        bool diverged = false;
        int divergeIndex = -1;

        while (currentNode != null)
        {
            newAccumulatedDistance += Vector3.Distance(lastPosition, currentNode.position);
            newWaypoints[newWaypointCount] = new Vector4(currentNode.position.x, currentNode.position.y, currentNode.position.z, newAccumulatedDistance);
            newPathNodes[newWaypointCount] = currentNode;
            
            if (!diverged)
            {
                if (newWaypointCount >= currentWaypointCount) 
                {
                    diverged = true; 
                } 
                else 
                {
                    Vector3 oldPos = new Vector3(waypointPositions[newWaypointCount].x, waypointPositions[newWaypointCount].y, waypointPositions[newWaypointCount].z);
                    if (Vector3.Distance(oldPos, currentNode.position) > 0.01f) 
                    {
                        diverged = true; 
                        divergeIndex = newWaypointCount; 
                    } 
                    else 
                    {
                        commonPathLength = newAccumulatedDistance; 
                    }
                }
            }
            
            lastPosition = currentNode.position;
            newWaypointCount++;
            
            if (newWaypointCount >= newWaypoints.Length) break;
            currentNode = currentNode.nextNode;
        }
        
        if (newWaypointCount < 2) return;

        if (currentPathLength > 0f && commonPathLength < currentPathLength)
        {
            CrowdNode splitNode = null;
            if (divergeIndex >= 0 && divergeIndex < currentWaypointCount) 
            {
                splitNode = currentPathNodes[divergeIndex]; 
            } 
            else if (newWaypointCount < currentWaypointCount) 
            {
                splitNode = currentPathNodes[newWaypointCount];
            }

            ExtractCutCharacters(currentPathLength, commonPathLength, splitNode);
            RescaleAbsoluteDistances(currentPathLength, commonPathLength, newAccumulatedDistance);
            hasStartedLooping = false; 
        }
        else if (newAccumulatedDistance > currentPathLength + 0.01f)
        {
            float oldLength = currentPathLength;
            hasStartedLooping = false;
    
            if (oldLength > 0f)
            {
                RescaleAbsoluteDistances(oldLength, float.MaxValue, newAccumulatedDistance);
            }
        }

        currentPathLength = newAccumulatedDistance;
        currentWaypointCount = newWaypointCount;

        for (int i = 0; i < currentWaypointCount; i++) 
        {
            waypointPositions[i] = newWaypoints[i];
            currentPathNodes[i] = newPathNodes[i];
        }

        waypointBuffer.SetData(waypointPositions);
        propertyBlock.SetInt("_WaypointCount", currentWaypointCount);
        propertyBlock.SetFloat("_TotalPathLength", currentPathLength);
    }
    
    void ExtractCutCharacters(float oldLength, float cutLength, CrowdNode refNode)
    {
        if (refNode == null) return;

        crowdBuffer.GetData(cpuData);
        System.Collections.Generic.List<CharacterData> cutChars = new System.Collections.Generic.List<CharacterData>();

        for (int i = 0; i < characterCount; i++)
        {
            float currentRealPos = cpuData[i].absoluteDistance + globalOffset;
            
            if (cutLength < oldLength && currentRealPos > cutLength)
            {
                CharacterData copy = cpuData[i];
                copy.absoluteDistance = currentRealPos; 
                cutChars.Add(copy);
            }
        }

        if (cutChars.Count > 0)
        {
            Vector4[] oldPath = new Vector4[currentWaypointCount];
            System.Array.Copy(waypointPositions, oldPath, currentWaypointCount);

            CrowdNode[] oldNodes = new CrowdNode[currentWaypointCount];
            System.Array.Copy(currentPathNodes, oldNodes, currentWaypointCount);

            GameObject go = new GameObject("IndependentCrowd_Cut");
            IndependentCrowdManager mgr = go.AddComponent<IndependentCrowdManager>();
            
            Texture tex = (characters != null && characters.Length > 0) ? characters[0].texture : null;
            
            mgr.Initialize(cutChars.ToArray(), oldPath, oldNodes, currentWaypointCount, oldLength, characterMesh, crowdMaterialTemplate, tex, catchUpSpeed, dispersionDelay, dispersionDuration, dispersionDistance, characterRotationY);
        }
    }

    void Update()
    {
        if (propertyBlock == null || runtimeMaterial == null || targetCrowd.rootNode == null || currentWaypointCount < 2) return;

        bool isFlowing = targetCrowd.rootNode.state == CrowdState.Flowing;
        
        bool isCruising = isFlowing && hasStartedLooping; 
        
        float currentSpeed = isCruising ? moveSpeed : catchUpSpeed;
        
        bool canMove = true;
        bool bufferDirty = false;
        
        bool isVisuallyMoving = false;

        if (!isFlowing) 
        {
            float maxCurrentPos = -float.MaxValue;
            for (int i = 0; i < characterCount; i++) {
                float currentPos = cpuData[i].absoluteDistance + globalOffset;
                if (currentPos > maxCurrentPos) maxCurrentPos = currentPos;
            }
            float distanceToEnd = currentPathLength - maxCurrentPos;
            float step = Time.deltaTime * currentSpeed; 
            canMove = !(step >= distanceToEnd);

            if (!canMove && distanceToEnd > 0) globalOffset += distanceToEnd;
        }

        if (canMove)
        {
            globalOffset += Time.deltaTime * currentSpeed;
            isVisuallyMoving = true;
        } 
        
        if (!isVisuallyMoving && wasMoving) 
        {
            resumeTime = Time.time;
        }
        wasMoving = isVisuallyMoving;

        if (isCruising && !wasCruising)
        {
            resumeTime = Time.time;
        }
        wasCruising = isCruising;

        if (isSmoothingDensity && canMove)
        {
            bool stillSmoothing = false;
            float maxSmoothDelta = Mathf.Min(densitySmoothSpeed, currentSpeed * 0.9f) * Time.deltaTime;

            for (int i = 0; i < characterCount; i++)
            {
                if (Mathf.Abs(cpuData[i].absoluteDistance - targetDistances[i]) > 0.001f)
                {
                    cpuData[i].absoluteDistance = Mathf.MoveTowards(cpuData[i].absoluteDistance, targetDistances[i], maxSmoothDelta);
                    stillSmoothing = true;
                    bufferDirty = true;
                }
            }
            isSmoothingDensity = stillSmoothing;
        }

        if (isFlowing)
        {
            float averageSpacing = currentPathLength / Mathf.Max(1, characterCount);
            float minRealPos = float.MaxValue;
            
            for (int j = 0; j < characterCount; j++) {
                float realPos = cpuData[j].absoluteDistance + globalOffset;
                if (realPos < minRealPos) minRealPos = realPos;
            }

            for (int i = 0; i < characterCount; i++) {
                float currentRealPos = cpuData[i].absoluteDistance + globalOffset;
                if (currentRealPos > currentPathLength) {
                    hasStartedLooping = true;
                    float newRealPos = minRealPos - averageSpacing;
                    cpuData[i].absoluteDistance = newRealPos - globalOffset;
                    
                    if (targetDistances != null && targetDistances.Length > i) {
                        targetDistances[i] = cpuData[i].absoluteDistance; 
                    }

                    minRealPos = newRealPos; 
                    bufferDirty = true;
                }
            }
        }

        if (bufferDirty) NormalizeDistances();

        propertyBlock.SetFloat("_GlobalOffset", globalOffset);
        
        Graphics.DrawMeshInstancedIndirect(characterMesh, 0, runtimeMaterial, new Bounds(Vector3.zero, Vector3.one * 1000), argsBuffer, 0, propertyBlock);
        
        if ((isCruising || !isVisuallyMoving) && runtimeSymbolMaterial != null && symbolArgsBuffer != null && symbolPropertyBlock != null)
        {
            Texture2D activeTexture = isCruising ? joyTexture : angerTexture;
            ComputeBuffer activeBuffer = isCruising ? symbolJoyBuffer : symbolBuffer;

            if (activeTexture != null && activeBuffer != null)
            {
                symbolPropertyBlock.SetTexture("_MainTex", activeTexture);
                symbolPropertyBlock.SetBuffer("_SymbolBuffer", activeBuffer);
            }

            symbolPropertyBlock.SetInt("_WaypointCount", currentWaypointCount);
            symbolPropertyBlock.SetFloat("_TotalPathLength", currentPathLength);
            
            symbolPropertyBlock.SetFloat("_ResumeTime", resumeTime);

            Graphics.DrawMeshInstancedIndirect(
                symbolMesh, 
                0, 
                runtimeSymbolMaterial, 
                new Bounds(Vector3.zero, Vector3.one * 1000), 
                symbolArgsBuffer, 
                0, 
                symbolPropertyBlock
            );
        }
    }
    
    void RescaleAbsoluteDistances(float oldLength, float cutLength, float trueNewLength)
    {
        crowdBuffer.GetData(cpuData);
        float averageSpacing = trueNewLength / Mathf.Max(1, characterCount);

        System.Collections.Generic.List<int> keptIndices = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<int> cutIndices = new System.Collections.Generic.List<int>();

        for (int i = 0; i < characterCount; i++) {
            cpuData[i].absoluteDistance += globalOffset;
            float currentRealPos = cpuData[i].absoluteDistance;
            
            if (cutLength < oldLength && currentRealPos > cutLength) cutIndices.Add(i);
            else keptIndices.Add(i);
        }
        globalOffset = 0f;

        keptIndices.Sort((a, b) => cpuData[b].absoluteDistance.CompareTo(cpuData[a].absoluteDistance));
        cutIndices.Sort((a, b) => cpuData[b].absoluteDistance.CompareTo(cpuData[a].absoluteDistance));

        float startPos = trueNewLength;
        if (keptIndices.Count > 0) {
            startPos = cpuData[keptIndices[0]].absoluteDistance;
        }
        
        float currentTarget = startPos;

        foreach (int i in keptIndices) {
            targetDistances[i] = currentTarget;
            currentTarget -= averageSpacing;
        }

        foreach (int i in cutIndices) {
            cpuData[i].absoluteDistance = currentTarget;
            targetDistances[i] = currentTarget;
            currentTarget -= averageSpacing;
        }

        isSmoothingDensity = true;
        crowdBuffer.SetData(cpuData);
    }

    void NormalizeDistances()
    {
        float minDistance = float.MaxValue;
        for (int i = 0; i < characterCount; i++) {
            if (cpuData[i].absoluteDistance < minDistance) minDistance = cpuData[i].absoluteDistance;
        }

        if (minDistance > 0f) {
            for (int i = 0; i < characterCount; i++) {
                cpuData[i].absoluteDistance -= minDistance;
                
                if (targetDistances != null && targetDistances.Length > i) {
                    targetDistances[i] -= minDistance;
                }
            }
            globalOffset += minDistance;
        }
        crowdBuffer.SetData(cpuData);
    }

    float CalculateInitialPathLength()
    {
        float total = 0f;
        CrowdNode current = targetCrowd.rootNode;
        Vector3 last = current.position;
        while (current != null) {
            total += Vector3.Distance(last, current.position);
            last = current.position;
            current = current.nextNode;
        }
        return total;
    }

    void OnDisable() 
    {
        if (targetCrowd != null) targetCrowd.OnCrowdPathChanged -= UpdatePathData;
        if (crowdBuffer != null) crowdBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (waypointBuffer != null) waypointBuffer.Release(); 
        
        if (runtimeMaterial != null) 
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
        
        if (symbolBuffer != null) symbolBuffer.Release();
        if (symbolJoyBuffer != null) symbolJoyBuffer.Release();
        if (symbolArgsBuffer != null) symbolArgsBuffer.Release();
        if (runtimeSymbolMaterial != null) { Destroy(runtimeSymbolMaterial); runtimeSymbolMaterial = null; }
    }
}