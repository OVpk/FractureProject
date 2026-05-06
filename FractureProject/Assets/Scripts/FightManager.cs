using System.Collections.Generic;
using UnityEngine;

public class FightManager : MonoBehaviour
{
    [HideInInspector] public Transform player;
    
    [Header("zones")]
    public float innerRadius = 3f;  
    public float outerRadius = 7f;  
    public LayerMask obstacleMask;  
    public float enemyRadius = 0.5f; 

    [Header("Gestion")]
    public int maxSimultaneousAttackers = 3;
    public List<EnemyController> aliveEnemies = new List<EnemyController>();
    
    [Header("Rythme")]
    public float timeBetweenAttacks = 2f;
    private float attackTimer = 0f;

    private List<EnemyController> attackList = new List<EnemyController>();
    private List<EnemyController> waitList = new List<EnemyController>();

    void Start()
    {
        player = Player.instance.transform;
        
        foreach (EnemyController enemy in aliveEnemies)
        {
            enemy.Setup(this);
            waitList.Add(enemy);
        }
    }

    void Update()
    {
        aliveEnemies.RemoveAll(item => item == null);
        waitList.RemoveAll(item => item == null);
        attackList.RemoveAll(item => item == null);

        if (aliveEnemies.Count == 0) return;

        ManageAttackerSlots();
        ManageAttackTiming();
        UpdateEnemyDestinations();
    }

    private void ManageAttackerSlots()
    {
        if (attackList.Count < maxSimultaneousAttackers && waitList.Count > 0)
        {
            EnemyController nextAttacker = waitList[0];
            waitList.RemoveAt(0);
            attackList.Add(nextAttacker);
            nextAttacker.SetState(EnemyState.PreparingAttack);
        }
    }

    private void ManageAttackTiming()
    {
        bool isSomeoneRushing = false;
        foreach (var e in attackList)
        {
            if (e.currentState == EnemyState.RushingPlayer || e.currentState == EnemyState.Attacking)
            {
                isSomeoneRushing = true;
                break;
            }
        }

        if (!isSomeoneRushing)
        {
            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f)
            {
                foreach (var e in attackList)
                {
                    if (e.currentState == EnemyState.PreparingAttack)
                    {
                        e.SetState(EnemyState.RushingPlayer);
                        attackTimer = timeBetweenAttacks;
                        break;
                    }
                }
            }
        }
        else
        {
            attackTimer = timeBetweenAttacks;
        }
    }

    private void UpdateEnemyDestinations()
    {
        float angleSlice = 360f / aliveEnemies.Count;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            EnemyController enemy = aliveEnemies[i];
            float currentAngle = i * angleSlice;

            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            
            float maxDistanceAllowed = outerRadius;
            if (Physics.Raycast(player.position, direction, out RaycastHit hit, outerRadius, obstacleMask))
            {
                maxDistanceAllowed = hit.distance - enemyRadius;
            }

            Vector3 targetPosition = transform.position;

            if (attackList.Contains(enemy))
            {
                if (enemy.currentState == EnemyState.RushingPlayer)
                {
                    targetPosition = player.position; 
                }
                else
                {
                    float attackDist = Mathf.Min(innerRadius, maxDistanceAllowed);
                    targetPosition = player.position + (direction * attackDist);
                }
            }
            else
            {
                float idealWaitDist = (innerRadius + outerRadius) / 2f;
                float waitDist = Mathf.Clamp(idealWaitDist, innerRadius, maxDistanceAllowed);
                targetPosition = player.position + (direction * waitDist);
            }

            enemy.SetTargetDestination(targetPosition);
        }
    }

    public void OnEnemyFinishedAttack(EnemyController enemy)
    {
        if (attackList.Contains(enemy))
        {
            attackList.Remove(enemy);
            waitList.Add(enemy);
            enemy.SetState(EnemyState.Waiting);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = Color.red;
        DrawGizmoCircle(player.position, innerRadius);

        Gizmos.color = Color.yellow;
        DrawGizmoCircle(player.position, outerRadius);
    }

    private void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 36;
        float angle = 0f;
        Vector3 lastPos = center + new Vector3(Mathf.Sin(0) * radius, 0, Mathf.Cos(0) * radius);

        for (int i = 1; i <= segments; i++)
        {
            angle += (360f / segments) * Mathf.Deg2Rad;
            Vector3 newPos = center + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            Gizmos.DrawLine(lastPos, newPos);
            lastPos = newPos;
        }
    }
}