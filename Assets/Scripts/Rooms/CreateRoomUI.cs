using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;

namespace XRMultiplayer
{
    public class CreateRoomUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Toggle privateToggle;
        [SerializeField] private RoomTypeDropdownUI roomTypeDropdown;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private int fallbackPlayerCount = 15;

        public static string SelectedRoomTypeId = RoomTypeDropdownUI.ForestRoomTypeId;

        private void Awake()
        {
            if (createButton == null)
                return;

            createButton.onClick.RemoveListener(CreateRoom);
            createButton.onClick.AddListener(CreateRoom);
        }

        private void OnDestroy()
        {
            if (createButton == null)
                return;

            createButton.onClick.RemoveListener(CreateRoom);
        }

        private void CreateRoom()
        {
            string roomName = roomNameInput != null ? roomNameInput.text.Trim() : string.Empty;
            bool isPrivate = privateToggle != null && privateToggle.isOn;

            string selectedRoomType = roomTypeDropdown != null
                ? roomTypeDropdown.SelectedRoomTypeId
                : RoomTypeDropdownUI.ForestRoomTypeId;

            int playerCount = GetPlayerCount();

            SelectedRoomTypeId = selectedRoomType;

            var extraSessionProperties = new Dictionary<string, SessionProperty>
            {
                { SessionManager.k_RoomTypeKeyIdentifier, new SessionProperty(selectedRoomType) }
            };

            if (XRINetworkGameManager.Instance == null)
            {
                return;
            }

            XRINetworkGameManager.Instance.CreateNewLobby(roomName, isPrivate, playerCount, extraSessionProperties);
        }

        private int GetPlayerCount()
        {
            if (playerCountText != null)
            {
                string raw = playerCountText.text.Trim();
                if (int.TryParse(raw, out int parsedValue) && parsedValue > 0)
                    return parsedValue;
            }

            return fallbackPlayerCount;
        }
    }
}