using UnityEngine;

public class GravityModifier : MonoBehaviour
{
    public float defaultGravity = -9.81f;
    public float gravityScale = 2f;

    void OnEnable ()
    {
        Physics.gravity = Vector3.up * defaultGravity * gravityScale;
    }

    void OnDisable ()
    {
        Physics.gravity = Vector3.up * defaultGravity;
    }

    void OnValidate ()
    {
        if (enabled && Application.isPlaying)
        {
            Physics.gravity = Vector3.up * defaultGravity * gravityScale;
        }
    }
}