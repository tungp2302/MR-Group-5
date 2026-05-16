using UnityEngine;
using System.Collections;

public class WolfWaypointWalker : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        public Transform point;
        public float waitTime = 0f;
    }

    [Header("Waypoints")]
    public Waypoint[] waypoints;
    public float moveSpeed = 2f;
    public float rotationSpeed = 5f;
    public float waypointReachDistance = 0.3f;

    private int currentIndex = 0;
    private bool isWaiting = false;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetBool("IsWalking", true);
    }

    void Update()
    {
        if (waypoints.Length == 0 || isWaiting) return;
        MoveTowardsWaypoint();
    }

    void MoveTowardsWaypoint()
    {
        Transform target = waypoints[currentIndex].point;
        Vector3 direction = (target.position - transform.position).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= waypointReachDistance)
        {
     
            StartCoroutine(WaitAtWaypoint(waypoints[currentIndex].waitTime));
        }
    }

    IEnumerator WaitAtWaypoint(float waitTime)
    {
        isWaiting = true; 

        if (waitTime > 0f)
        {
            animator.SetBool("IsWalking", false);
            yield return new WaitForSeconds(waitTime);
            animator.SetBool("IsWalking", true);
        }

        
        currentIndex = (currentIndex + 1) % waypoints.Length;
        isWaiting = false;
    }
}
