using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkXRPoseDriver : NetworkBehaviour
{
    [Header("XR Origin tracked transforms (local-only)")]
    public Transform xrHead;
    public Transform xrLeftController;
    public Transform xrRightController;

    [Header("Networked transforms (replicated)")]
    public Transform netHead;
    public Transform netLeftHand;
    public Transform netRightHand;

    void Update()
    {
        if (!IsOwner) return;

        if (xrHead == null || xrLeftController == null || xrRightController == null) return;
        if (netHead == null || netLeftHand == null || netRightHand == null) return;

        netHead.SetPositionAndRotation(xrHead.position, xrHead.rotation);
        netLeftHand.SetPositionAndRotation(xrLeftController.position, xrLeftController.rotation);
        netRightHand.SetPositionAndRotation(xrRightController.position, xrRightController.rotation);
    }
}