using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerDisplayNameSync : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameTagText;

    private NetworkVariable<FixedString64Bytes> networkDisplayName =
        new NetworkVariable<FixedString64Bytes>(
            "Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        networkDisplayName.OnValueChanged += OnDisplayNameChanged;

        UpdateNameUI(networkDisplayName.Value.ToString());

        if (IsOwner)
            StartCoroutine(SetNameWhenReady());
    }

    public override void OnNetworkDespawn()
    {
        networkDisplayName.OnValueChanged -= OnDisplayNameChanged;
    }

    private IEnumerator SetNameWhenReady()
    {
        while (PlayerProfileManager.Instance == null)
            yield return null;

        while (string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.GetNetworkName()))
            yield return null;

        string loadedName = PlayerProfileManager.Instance.GetNetworkName();
        networkDisplayName.Value = loadedName;
        UpdateNameUI(loadedName);
    }

    private void OnDisplayNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        UpdateNameUI(newValue.ToString());
    }

    private void UpdateNameUI(string newName)
    {
        if (nameTagText != null)
            nameTagText.text = newName;
    }

    public void RefreshMyName()
    {
        if (!IsOwner)
            return;

        if (PlayerProfileManager.Instance == null)
            return;

        string loadedName = PlayerProfileManager.Instance.GetNetworkName();

        if (string.IsNullOrWhiteSpace(loadedName))
            return;

        networkDisplayName.Value = loadedName;
        UpdateNameUI(loadedName);
    }
}