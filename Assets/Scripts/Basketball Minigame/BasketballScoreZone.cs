using UnityEngine;

namespace XRMultiplayer.MiniGames
{
    public class BasketballScoreZone : MonoBehaviour
    {
        MiniGame_Basketball miniGameBasket;

        void Awake()
        {
            miniGameBasket = GetComponentInParent<MiniGame_Basketball>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out BasketballBall ball))
                return;

            if (ball.HasScored)
                return;

            if (!ball.IsMovingDownward())
                return;

            ball.MarkScored();

            if (miniGameBasket != null)
                miniGameBasket.LocalPlayerScored(1);

            ball.RespawnBall();
        }
    }
}