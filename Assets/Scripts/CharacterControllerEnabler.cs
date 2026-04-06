using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterControllerEnabler : MonoBehaviour
{
    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cc != null && !cc.enabled)
            cc.enabled = true;
    }

    private void OnEnable()
    {
        if (cc == null)
            cc = GetComponent<CharacterController>();

        if (cc != null && !cc.enabled)
            cc.enabled = true;
    }

    private void LateUpdate()
    {
        if (cc != null && !cc.enabled)
        {
            cc.enabled = true;
        }
    }
}