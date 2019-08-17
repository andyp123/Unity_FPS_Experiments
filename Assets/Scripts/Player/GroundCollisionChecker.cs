using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class GroundCollisionChecker : MonoBehaviour
{
    public Collider groundCollider;
    public Collider[] excludedColliders;
    public float slopeTolerance = 0.5f;

    public float checkSphereRadius = 0.5f;
    public Vector3 checkSphereOffset = Vector3.zero;
    public LayerMask checkSphereLayerMask = Physics.DefaultRaycastLayers;

    public bool OnGround { get { return (_colliderIDs.Count > 0); }}

    public delegate void OnGroundStateChangeEventHandler();
    public event OnGroundStateChangeEventHandler EnterGroundEvent;
    public event OnGroundStateChangeEventHandler ExitGroundEvent;

    List<int> _colliderIDs;
    List<int> _excludedColliderIDs;

    void Awake ()
    {
        groundCollider = groundCollider ?? GetComponentInChildren<Collider>();
        _colliderIDs = new List<int>(4);
        _excludedColliderIDs = new List<int>();

        foreach(Collider c in excludedColliders)
        {
            int id = c.GetInstanceID();
            _excludedColliderIDs.Add(id);
        }
    }

    void OnCollisionEnter (Collision collisionInfo)
    {
        bool onGround = (_colliderIDs.Count > 0);

        foreach (ContactPoint contact in collisionInfo.contacts)
        {
            float dot = Vector3.Dot(contact.normal, Vector3.up);

            if (dot > (1f - slopeTolerance))
            {
                int id = contact.otherCollider.GetInstanceID();

                if (!_colliderIDs.Contains(id) && !_excludedColliderIDs.Contains(id))
                {
                    _colliderIDs.Add(id);
                }
            }
        }

        if (!onGround && (_colliderIDs.Count > 0))
        {
            if (EnterGroundEvent != null) { EnterGroundEvent(); }
        }
    }

    void OnCollisionExit (Collision collisionInfo)
    {
        bool onGround = (_colliderIDs.Count > 0);

        int id = collisionInfo.collider.GetInstanceID();
        if (_colliderIDs.Contains(id))
        {
            _colliderIDs.Remove(id);
        }

        if (onGround && (_colliderIDs.Count == 0))
        {
            if (ExitGroundEvent != null) { ExitGroundEvent(); }
        }
    }

    void OnValidate ()
    {
        slopeTolerance = Mathf.Clamp01(slopeTolerance);
    }

    // This function is used to allow more forgiving ground collision checks
    public bool CheckSphere ()
    {
        Vector3 center = groundCollider.transform.position + checkSphereOffset;
        return Physics.CheckSphere(center, checkSphereRadius, checkSphereLayerMask, QueryTriggerInteraction.Ignore);
    }
}