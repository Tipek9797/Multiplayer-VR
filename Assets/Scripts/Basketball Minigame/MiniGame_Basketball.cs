using UnityEngine;

namespace XRMultiplayer.MiniGames
{
    public class MiniGame_Basketball : MiniGameBase
    {
        [SerializeField] BasketballBall[] balls;

        int currentPlayerScore = 0;
        bool finalScoreSubmitted = false;

        public override void Start()
        {
            base.Start();

            if (balls == null || balls.Length == 0)
                balls = GetComponentsInChildren<BasketballBall>(true);
        }

        public override void SetupGame()
        {
            base.SetupGame();

            currentPlayerScore = 0;
            finalScoreSubmitted = false;

            ResetAllBalls();
        }

        public override void StartGame()
        {
            base.StartGame();

            currentPlayerScore = 0;
            finalScoreSubmitted = false;

            ResetAllBalls();
        }

        public override void UpdateGame(float deltaTime)
        {
            base.UpdateGame(deltaTime);

            if (finalScoreSubmitted)
                return;

            if (m_MiniGameManager == null)
                return;

            if (m_MiniGameManager.currentNetworkedGameState != MiniGameManager.GameState.InGame)
                return;

            if (!m_MiniGameManager.LocalPlayerInGame)
                return;

            if (m_CurrentTimer <= 0f)
            {
                finalScoreSubmitted = true;
                m_MiniGameManager.SubmitScoreRpc(
                    currentPlayerScore,
                    XRINetworkPlayer.LocalPlayer.OwnerClientId,
                    true
                );
            }
        }

        public override void FinishGame(bool submitScore = true)
        {
            base.FinishGame(submitScore);
            ResetAllBalls();
        }

        public void LocalPlayerScored(int points = 1)
        {
            if (finalScoreSubmitted)
                return;

            if (m_MiniGameManager == null)
                return;

            if (m_MiniGameManager.currentNetworkedGameState != MiniGameManager.GameState.InGame)
                return;

            if (!m_MiniGameManager.LocalPlayerInGame)
                return;

            currentPlayerScore += points;

            if (currentPlayerScore < 0)
                currentPlayerScore = 0;

            m_MiniGameManager.SubmitScoreRpc(
                currentPlayerScore,
                XRINetworkPlayer.LocalPlayer.OwnerClientId
            );
        }

        void ResetAllBalls()
        {
            if (balls == null)
                return;

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null)
                    balls[i].ResetBall();
            }
        }
    }
}