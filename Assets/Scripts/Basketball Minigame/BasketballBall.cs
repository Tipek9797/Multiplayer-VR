using System.Collections;
using UnityEngine;

namespace XRMultiplayer.MiniGames
{
    public class BasketballBall : MonoBehaviour
    {
        [SerializeField] Rigidbody rb ;
        [SerializeField] Transform homeSpawnPoint;
        [SerializeField] float respawnDelay = 0.2f;

        public bool HasScored { get; private set; }

        Coroutine respawnRoutine;

        void Awake()
        {
            if (rb == null)
                TryGetComponent(out rb);
        }

        void Start()
        {
            ResetBall();
        }

        public void MarkScored()
        {
            HasScored = true;
        }

        public void RespawnBall()
        {
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            ResetBall();
            respawnRoutine = null;
        }

        public void ResetBall()
        {
            HasScored = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }

            if (homeSpawnPoint != null)
                transform.SetPositionAndRotation(homeSpawnPoint.position, homeSpawnPoint.rotation);
        }

        public bool IsMovingDownward()
        {
            if (rb == null)
                return false;

            return rb.linearVelocity.y < 0f;
        }
    }
}