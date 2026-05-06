using System;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player instance { get; private set; }
    
    public AnimatorController animatorController;
    
    //Stoian
    public SpriteRenderer spriteRenderer;
    //Stoian
    
    public enum States
    {
        Idle,
        Walking,
        Transported,
        Ejected,
        Pushing, //Nico
        Attacking,
        Hit
    }
    
    public States currentState = States.Idle;

    [HideInInspector] 
    public Vector3 lastFacingDirection = new Vector3(1, 0, 1).normalized;

    private void Awake()
    {
        if (instance != null)
            throw new Exception("Multiple players in scene");
        
        instance = this;
    }

    private void Start()
    {
        animatorController = GetComponent<AnimatorController>();
        rb = GetComponent<Rigidbody>();
    }

    public float moveSpeed;
    
    private Vector3 direction;
    private Vector3 skewedDirection;

    public float crowdSpeed;

    private CrowdNode targetCrowdPoint;
    
    private Vector3 lastPositionAllowed;
    
    public float ejectionDistance; 
    public float ejectionSpeed;
    private Vector3 ejectionDirection;
    private Vector3 ejectionTargetPosition;

    public Rigidbody rb;

    public bool locked;
    
    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        
        direction = new Vector3(h, 0, v).normalized;
        
        if (direction.magnitude > 0.1f)
        {
            Vector3 snappedInput;
            
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                snappedInput = new Vector3(Mathf.Sign(direction.x), 0, 0); 
            }
            else
            {
                snappedInput = new Vector3(0, 0, Mathf.Sign(direction.z)); 
            }

            lastFacingDirection = Quaternion.Euler(0, 45, 0) * snappedInput;
        }
        
        if (currentState != States.Transported && currentState != States.Ejected && currentState != States.Pushing && currentState != States.Attacking && currentState != States.Hit)
        {
            ChangeState(direction.magnitude > 0.1f ? States.Walking : States.Idle);
        }
        
        if (currentState == States.Walking)
        {
            animatorController.UpdateMoveDirection(direction.x, direction.z);
        }
        
        //Stoian
        if (currentState == States.Pushing)
        {
            return;
        }
        
        if (h > 0 && v < 0) //Down Right
        {
            spriteRenderer.flipX = true;
        }
        else if (h < 0 && v < 0) //Down Left
        {
            spriteRenderer.flipX = false;
        } 
        else if (h < 0 && v > 0) //Up Left
        {
            spriteRenderer.flipX = true;
        } 
        else if (h > 0 && v > 0) //Up Right
        {
            spriteRenderer.flipX = false;
        }
        else if (h > 0 && v == 0) //Right
        {
            spriteRenderer.flipX = false;
        }
        else if (h < 0 && v == 0) //Left
        {
            spriteRenderer.flipX = true;
        }
        //Stoian
    }
    
    void FixedUpdate()
    {
        lastPositionAllowed = rb.position;
        
        switch (currentState)
        {
            case States.Idle:
            case States.Attacking:
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0); 
                break;
            case States.Hit:
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0); 
                break;
            case States.Walking: 
                Move();
                break;
            case States.Transported: 
                FollowCrowd(); 
                break;
            case States.Ejected:
                ApplyEjection();
                break;
            //Stoian
            case States.Pushing:
                Move();
                break;
            //Stoian
        }
    }

    public void ChangeState(States newState)
    {
        if (currentState == newState) return;
        
        if (newState == States.Transported || newState == States.Ejected)
        {
            rb.isKinematic = true; 
        }
        else
        {
            rb.isKinematic = false; 
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        currentState = newState;
        animatorController.OnStateChanged(newState);
    }

    public void Move()
    {
        if (locked) return;
        skewedDirection = Quaternion.Euler(0, 45, 0) * direction;

        rb.linearVelocity = new Vector3(skewedDirection.x * moveSpeed, rb.linearVelocity.y, skewedDirection.z * moveSpeed);
    }

    public void FollowCrowd()
    {
        Vector3 flatTargetPos = new Vector3(targetCrowdPoint.position.x, rb.position.y, targetCrowdPoint.position.z);
        
        Vector3 newPos = Vector3.MoveTowards(
            rb.position, 
            flatTargetPos, 
            crowdSpeed * Time.fixedDeltaTime
        );
        rb.MovePosition(newPos);

        if (Vector3.Distance(rb.position, flatTargetPos) < 0.1f)
        {
            if (targetCrowdPoint is IntermediateExitCrowdNode intermediateNode)
            {
                Vector3 flatEjectionDir = new Vector3(intermediateNode.ejectionDirection.x, 0, intermediateNode.ejectionDirection.y).normalized;
                ejectionTargetPosition = rb.position + (flatEjectionDir * ejectionDistance);
                
                ChangeState(States.Ejected);
                return;
            }
            
            if (targetCrowdPoint.nextNode is ExitCrowdNode)
            {
                Vector3 flatEjectionDir = new Vector3(ejectionDirection.x, 0, ejectionDirection.z).normalized;
                ejectionTargetPosition = rb.position + (flatEjectionDir * ejectionDistance);
                
                ChangeState(States.Ejected);
            }
            else
            {
                if (targetCrowdPoint.nextNode.nextNode is ExitCrowdNode)
                    ejectionDirection = (targetCrowdPoint.nextNode.position - targetCrowdPoint.position).normalized;
                
                targetCrowdPoint = targetCrowdPoint.nextNode;
            }
        }
    }

    private void ApplyEjection()
    {
        Vector3 flatEjectionTarget = new Vector3(ejectionTargetPosition.x, rb.position.y, ejectionTargetPosition.z);

        Vector3 newPos = Vector3.MoveTowards(
            rb.position, 
            flatEjectionTarget, 
            ejectionSpeed * Time.fixedDeltaTime
        );
        rb.MovePosition(newPos);
        
        if (Vector3.Distance(rb.position, flatEjectionTarget) < 0.01f)
        {
            ChangeState(States.Idle);
        }
    }

    public void SetCrowdToFollow(CrowdNode startNode)
    {
        if (currentState == States.Transported || currentState == States.Ejected) return;
        
        if (startNode.nextNode.nextNode is ExitCrowdNode)
            ejectionDirection = (startNode.nextNode.position - startNode.position).normalized;

        targetCrowdPoint = startNode.nextNode;
        ChangeState(States.Transported);
    }

    public void BlockByCrowd()
    {
        Vector3 targetPos = lastPositionAllowed;
        
        if (skewedDirection.magnitude > 0.01f)
        {
            targetPos -= skewedDirection * 0.02f;
        }
        
        rb.MovePosition(targetPos);
    }
    
    public Vector3 GetPushDirection()
    {
        if (skewedDirection.magnitude > 0.1f)
        {
            return skewedDirection.normalized;
        }
        return Vector3.zero;
    }

    public void LockPlayer(bool Locked)
    {
        locked = Locked;
    }
}