using TMPro;
using UnityEngine;

namespace XRMultiplayer
{
    public class RoomTypeDropdownUI : MonoBehaviour
    {
        public const string ForestRoomTypeId = "forest";
        public const string AlchemistRoomTypeId = "alchemist";

        public const string ForestSceneName = "LobbyStyle_Forest";
        public const string AlchemistSceneName = "LobbyStyle_Alchemist";

        [SerializeField] private TMP_Dropdown roomTypeDropdown;

        public string SelectedRoomTypeId => GetRoomTypeIdFromIndex(roomTypeDropdown != null ? roomTypeDropdown.value : 0);

        public static string GetRoomTypeIdFromIndex(int index)
        {
            switch (index)
            {
                case 1:
                    return AlchemistRoomTypeId;
                default:
                    return ForestRoomTypeId;
            }
        }

        public static string GetSceneNameForRoomType(string roomTypeId)
        {
            switch (roomTypeId)
            {
                case AlchemistRoomTypeId:
                    return AlchemistSceneName;
                case ForestRoomTypeId:
                default:
                    return ForestSceneName;
            }
        }

        private void Reset()
        {
            if (roomTypeDropdown == null)
                roomTypeDropdown = GetComponentInChildren<TMP_Dropdown>(true);
        }
    }
}