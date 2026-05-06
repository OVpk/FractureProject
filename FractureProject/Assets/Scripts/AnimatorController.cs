using UnityEngine;

public class AnimatorController : MonoBehaviour
{
    public Animator animator;

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsTransportedHash = Animator.StringToHash("isTransported");
    private static readonly int IsPushingHash = Animator.StringToHash("isPushing");//Nico
    private static readonly int IsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    public void OnStateChanged(Player.States newState)
    {
        if (newState == Player.States.Hit)
        {
            animator.SetTrigger(Hit);
        }
        
        animator.SetBool(IsMovingHash, newState == Player.States.Walking);
        animator.SetBool(IsTransportedHash, newState == Player.States.Transported);
        animator.SetBool(IsPushingHash,newState == Player.States.Pushing);//Nico
        animator.SetBool(IsAttacking, newState == Player.States.Attacking);
    }

    public void UpdateMoveDirection(float dirX, float dirY)
    {
        animator.SetFloat(MoveXHash, dirX);
        animator.SetFloat(MoveYHash, dirY);
    }
}
