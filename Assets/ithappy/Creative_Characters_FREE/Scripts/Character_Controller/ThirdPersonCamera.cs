using UnityEngine;

namespace Controller
{
    public class ThirdPersonCamera : PlayerCamera
    {
        [Header("Player")]
        [SerializeField] private Transform player; // ← לגרור כאן את ה-ROOT של השחקן

        [Header("Camera Settings")]
        [SerializeField, Range(0f, 2f)]
        private float m_Offset = 1.5f;

        [SerializeField, Range(0f, 360f)]
        private float m_CameraSpeed = 90f;

        private Vector3 m_LookPoint;
        private Vector3 m_TargetPos;

        private void Awake()
        {
            // חיבור חד-משמעי לשחקן
            if (player != null)
                m_Player = player;
        }

        private void LateUpdate()
        {
            // ❌ בלי שחקן – לא מזיזים מצלמה
            if (m_Player == null)
                return;

            Move(Time.deltaTime);
        }

        public override void SetInput(in Vector2 delta, float scroll)
        {
            base.SetInput(delta, scroll);

            var dir = new Vector3(0, 0, -m_Distance);
            var rot = Quaternion.Euler(m_Angles.x, m_Angles.y, 0f);

            var playerPos = m_Player.position;
            m_LookPoint = playerPos + m_Offset * Vector3.up;
            m_TargetPos = m_LookPoint + rot * dir;
        }

        private void Move(float deltaTime)
        {
            // Camera movement
            var direction = m_TargetPos - m_Transform.position;
            var delta = m_CameraSpeed * deltaTime;

            if (delta * delta > direction.sqrMagnitude)
                m_Transform.position = m_TargetPos;
            else
                m_Transform.position += delta * direction.normalized;

            m_Transform.LookAt(m_LookPoint);

            // Target follow (אם קיים)
            if (m_Target != null)
            {
                m_Target.position = m_Transform.position + m_Transform.forward * TargetDistance;
            }
        }
    }
}
