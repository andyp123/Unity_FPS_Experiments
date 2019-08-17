using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConvertToColliders : MonoBehaviour
{
    public bool removeRenderer = false;
    public bool addContactViewer = false;

    void Awake()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            GameObject go = mf.gameObject;

            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = mf.sharedMesh;

            if (addContactViewer)
            {
                ContactPointViewer contactViewer = go.AddComponent<ContactPointViewer>();
                contactViewer.colorFromID = true;
            }

            if (removeRenderer)
            {
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Destroy(renderer);
                    Destroy(mf);
                }
            }
        }
    }

}
