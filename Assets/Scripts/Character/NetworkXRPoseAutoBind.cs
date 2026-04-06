using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkXRPoseAutoBind : NetworkBehaviour
{
    [Header("Pose driver on this Player Character")]
    public NetworkXRPoseDriver poseDriver;

    [Header("Paths inside XR Origin (adjust only if your names differ)")]
    public string xrOriginName = "XR Origin";
    public string headPath = "Camera Offset/Main Camera";
    public string leftControllerPath = "Camera Offset/Left Controller";
    public string rightControllerPath = "Camera Offset/Right Controller";

    [Header("Paths inside Player prefab (adjust only if your names differ)")]
    public string netHeadPath = "Head";
    public string netLeftHandPath = "Left Hand";
    public string netRightHandPath = "Right Hand";

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (poseDriver == null)
            poseDriver = GetComponent<NetworkXRPoseDriver>();

        if (poseDriver == null)
        {
            return;
        }

        GameObject xrOriginGO = GameObject.Find(xrOriginName);
        if (xrOriginGO == null)
        {
            return;
        }

        Transform xrOrigin = xrOriginGO.transform;

        Transform xrHead = FindByPath(xrOrigin, headPath);
        Transform xrLeft = FindByPath(xrOrigin, leftControllerPath);
        Transform xrRight = FindByPath(xrOrigin, rightControllerPath);

        if (xrHead == null || xrLeft == null || xrRight == null)
        {
            return;
        }

        Transform netHead = FindByPath(transform, netHeadPath);
        Transform netLeftHand = FindByPath(transform, netLeftHandPath);
        Transform netRightHand = FindByPath(transform, netRightHandPath);

        if (netHead == null || netLeftHand == null || netRightHand == null)
        {
            return;
        }

        poseDriver.xrHead = xrHead;
        poseDriver.xrLeftController = xrLeft;
        poseDriver.xrRightController = xrRight;

        poseDriver.netHead = netHead;
        poseDriver.netLeftHand = netLeftHand;
        poseDriver.netRightHand = netRightHand;
    }

    private static Transform FindByPath(Transform root, string path)
    {
        return root.Find(path);
    }
}