using System;
using System.Collections;
using UnityEngine;

public enum EnemyState { Waiting, PreparingAttack, RushingPlayer, Attacking, Hit }

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float stopDistance = 0.5f;
    
    public float attackTriggerDistance = 1.5f;
    public float attackRange = 1f;
    public float hitRadius = 1f;
    public float attackDelay = 0.5f;
    public float attackDuration = 1.5f;
    
    public float hitDuration = 0.5f;
    public float knockbackForce = 10f;
    public float knockbackFriction = 5f;

    public Color hitColor = Color.red;
    public float blinkInterval = 0.1f;
    public int numberOfBlinks = 3;
    
    public int maxHealth = 3;
    public int currentHealth;
    public int attackDamage = 1;
    
    public LayerMask playerLayer;
    
    public EnemyState currentState = EnemyState.Waiting; 
    
    private FightManager manager;
    private Rigidbody rb;
    private Vector3 currentDestination;
    private Vector3 facingDirection = new Vector3(1, 0, 1).normalized; 
    
    public AnimatorController animatorController;
    public SpriteRenderer spriteRenderer;

    private Color originalColor;
    private Coroutine currentBlinkCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; 

        if (spriteRenderer != null) originalColor = spriteRenderer.color;
        
        currentHealth = maxHealth;
    }

    public void Setup(FightManager combatManager)
    {
        manager = combatManager;
    }
    
    public void SetState(EnemyState newState)
    {
        currentState = newState;
        
        if (newState == EnemyState.Attacking)
        {
            animatorController.OnStateChanged(Player.States.Attacking);
        }
        else if (newState == EnemyState.Hit)
        {
            animatorController.OnStateChanged(Player.States.Hit);
        }
    }

    public void SetTargetDestination(Vector3 targetPos)
    {
        currentDestination = targetPos;
    }

    private void Update()
    {
        if (currentState == EnemyState.Hit) return;

        Vector3 animatorDirection = Quaternion.Euler(0, -45, 0) * facingDirection;
        animatorController.UpdateMoveDirection(animatorDirection.x, animatorDirection.z);

        float h = animatorDirection.x;
        float v = animatorDirection.z;
        float threshold = 0.05f;

        if (h > threshold && v < -threshold) spriteRenderer.flipX = true; 
        else if (h < -threshold && v < -threshold) spriteRenderer.flipX = false; 
        else if (h < -threshold && v > threshold) spriteRenderer.flipX = true; 
        else if (h > threshold && v > threshold) spriteRenderer.flipX = false; 
        else if (h > threshold && Mathf.Abs(v) <= threshold) spriteRenderer.flipX = false; 
        else if (h < -threshold && Mathf.Abs(v) <= threshold) spriteRenderer.flipX = true; 
    }
    
    void FixedUpdate()
    {
        if (currentState == EnemyState.Hit)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * knockbackFriction);
            return;
        }

        if (currentState == EnemyState.Attacking) return;

        if (currentState == EnemyState.PreparingAttack || currentState == EnemyState.RushingPlayer)
        {
            float distToPlayer = Vector3.Distance(transform.position, manager.player.position);
            if (distToPlayer <= attackTriggerDistance)
            {
                PerformAttack();
                return;
            }
        }

        MoveTowardsDestination();
    }

    private void MoveTowardsDestination()
    {
        Vector3 directionToTarget = currentDestination - transform.position;
        directionToTarget.y = 0; 

        float distance = directionToTarget.magnitude;

        if (distance > stopDistance)
        {
            Vector3 moveDirection = directionToTarget.normalized;
            rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, moveDirection.z * moveSpeed);
            facingDirection = moveDirection;
            
            if (currentState != EnemyState.Attacking && currentState != EnemyState.Hit)
            {
                animatorController.OnStateChanged(Player.States.Walking);
            }
        }
        else
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            Vector3 lookDir = manager.player.position - transform.position;
            lookDir.y = 0;
            if(lookDir != Vector3.zero) facingDirection = lookDir.normalized;
            
            if (currentState != EnemyState.Attacking && currentState != EnemyState.Hit)
            {
                animatorController.OnStateChanged(Player.States.Idle);
            }
        }
    }

    private void PerformAttack()
    {
        SetState(EnemyState.Attacking);
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0); 
        
        Invoke(nameof(ExecuteAttackDamage), attackDelay);
        Invoke(nameof(EndAttack), attackDuration);
    }

    private void ExecuteAttackDamage()
    {
        if (currentState != EnemyState.Attacking) return; 

        Vector3 hitCenter = transform.position + (facingDirection * attackRange);
        hitCenter.y = transform.position.y;

        Collider[] hitPlayers = Physics.OverlapSphere(hitCenter, hitRadius, playerLayer);

        foreach (Collider playerHit in hitPlayers)
        {
            PlayerAttack playerStats = playerHit.GetComponent<PlayerAttack>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(attackDamage, transform.position);
            }
        }
    }

    private void EndAttack()
    {
        manager.OnEnemyFinishedAttack(this);
    }

    public void TakeDamage(int damage, Vector3 attackerPosition) 
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        CancelInvoke(nameof(ExecuteAttackDamage));
        CancelInvoke(nameof(EndAttack));

        manager.OnEnemyFinishedAttack(this); 

        SetState(EnemyState.Hit);

        Vector3 pushDirection = transform.position - attackerPosition;
        pushDirection.y = 0; 
        pushDirection = pushDirection.normalized; 

        rb.linearVelocity = pushDirection * knockbackForce;

        if (currentBlinkCoroutine != null) StopCoroutine(currentBlinkCoroutine);
        currentBlinkCoroutine = StartCoroutine(BlinkRoutine());

        Invoke(nameof(RecoverFromHit), hitDuration);
    }

    private void Die()
    {
        CancelInvoke();

        if (manager != null)
        {
            manager.OnEnemyFinishedAttack(this);
        }

        Debug.Log(gameObject.name + " est mort !");
        
        Destroy(gameObject);
    }

    private IEnumerator BlinkRoutine()
    {
        if (spriteRenderer == null) yield break;

        for (int i = 0; i < numberOfBlinks; i++)
        {
            spriteRenderer.color = hitColor;
            yield return new WaitForSeconds(blinkInterval);
            
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(blinkInterval);
        }

        spriteRenderer.color = originalColor;
        currentBlinkCoroutine = null;
    }

    private void RecoverFromHit()
    {
        if (currentState == EnemyState.Hit)
        {
            SetState(EnemyState.Waiting);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitCenter = transform.position;

        if (Application.isPlaying) hitCenter += facingDirection * attackRange;
        else hitCenter += transform.forward * attackRange;

        Gizmos.DrawWireSphere(hitCenter, hitRadius);
    }
}