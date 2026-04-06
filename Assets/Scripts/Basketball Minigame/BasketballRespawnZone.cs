using UnityEngine;

namespace XRMultiplayer.MiniGames
{
    public class BasketballRespawnZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out BasketballBall ball))
                return;

            if (ball.HasScored)
                return;

            ball.RespawnBall();
        }
    }
}