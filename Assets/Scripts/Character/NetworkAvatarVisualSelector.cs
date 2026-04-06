using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkAvatarVisualSelector : NetworkBehaviour
{
    [Header("Avatar Visual Roots")]
    [SerializeField] private GameObject maleVisual;
    [SerializeField] private GameObject femaleVisual;

    private NetworkVariable<FixedString32Bytes> networkCharacterId =
        new NetworkVariable<FixedString32Bytes>(
            "female",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        networkCharacterId.OnValueChanged += OnCharacterChanged;

        ApplyVisual(networkCharacterId.Value.ToString());

        if (IsOwner)
        {
            string savedCharacterId = "female";

            if (PlayerProfileManager.Instance != null &&
                !string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.CurrentCharacterId))
            {
                savedCharacterId = PlayerProfileManager.Instance.CurrentCharacterId;
            }

            networkCharacterId.Value = savedCharacterId;
            ApplyVisual(savedCharacterId);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkCharacterId.OnValueChanged -= OnCharacterChanged;
    }

    private void OnCharacterChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        ApplyVisual(newValue.ToString());
    }

    private void ApplyVisual(string characterId)
    {
        bool useMale = characterId == "male";

        if (maleVisual != null)
            maleVisual.SetActive(useMale);

        if (femaleVisual != null)
            femaleVisual.SetActive(!useMale);
    }

    public void RefreshFromProfile()
    {
        if (!IsOwner)
            return;

        if (PlayerProfileManager.Instance == null)
            return;

        string savedCharacterId = PlayerProfileManager.Instance.CurrentCharacterId;

        if (string.IsNullOrWhiteSpace(savedCharacterId))
            savedCharacterId = "female";

        networkCharacterId.Value = savedCharacterId;
        ApplyVisual(savedCharacterId);
    }
}