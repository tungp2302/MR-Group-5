using UnityEngine;

namespace ithappy.Animals_FREE
{
    [RequireComponent(typeof(CreatureMover))]
    public class CreatureWaypointPatrol : MonoBehaviour
    {
        [Header("Waypoints")]
        [SerializeField]
        private Transform[] m_Waypoints;

        [SerializeField]
        private float m_ReachDistance = 1f;

        [Header("Movement")]
        [SerializeField]
        private bool m_Run = false;

        private CreatureMover m_CreatureMover;
        private int m_CurrentWaypoint;

        private void Awake()
        {
            m_CreatureMover = GetComponent<CreatureMover>();
        }

        private void Update()
        {
            if (m_Waypoints == null || m_Waypoints.Length == 0)
            {
                m_CreatureMover.SetInput(Vector2.zero, transform.position + transform.forward, false, false);
                return;
            }

            Transform targetWaypoint = m_Waypoints[m_CurrentWaypoint];

            Vector3 direction = targetWaypoint.position - transform.position;
            direction.y = 0f;

            float distance = direction.magnitude;

            // Nächsten Waypoint auswählen
            if (distance <= m_ReachDistance)
            {
                m_CurrentWaypoint++;

                if (m_CurrentWaypoint >= m_Waypoints.Length)
                {
                    m_CurrentWaypoint = 0;
                }

                targetWaypoint = m_Waypoints[m_CurrentWaypoint];
                direction = targetWaypoint.position - transform.position;
                direction.y = 0f;
            }

            direction.Normalize();

            // Vorwärtsbewegung
            Vector2 axis = new Vector2(0f, 1f);

            // Zielpunkt fürs Drehen/LookAt
            Vector3 target = transform.position + direction;

            m_CreatureMover.SetInput(axis, target, m_Run, false);
        }

        private void OnDrawGizmos()
        {
            if (m_Waypoints == null || m_Waypoints.Length == 0)
            {
                return;
            }

            Gizmos.color = Color.green;

            for (int i = 0; i < m_Waypoints.Length; i++)
            {
                if (m_Waypoints[i] == null)
                    continue;

                Gizmos.DrawSphere(m_Waypoints[i].position, 0.3f);

                Transform next = m_Waypoints[(i + 1) % m_Waypoints.Length];

                if (next != null)
                {
                    Gizmos.DrawLine(m_Waypoints[i].position, next.position);
                }
            }
        }
    }
}