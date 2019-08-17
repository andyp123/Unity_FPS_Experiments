using UnityEngine;
using System.Collections.Generic;

public class ContactPointViewer : MonoBehaviour
{
    public float normalScale = 0.25f;
    public Color normalColor = Color.magenta;
    public bool colorFromID = true;

    private static List<ContactPoint> _contacts;

    void Awake ()
    {
        if (_contacts == null)
        {
            _contacts = new List<ContactPoint>(8);
        }

        if (colorFromID)
        {   
            int id = GetInstanceID();
            float h = (float)(id & 0xFF) / 255f;
            normalColor = Color.HSVToRGB(h, 1f, 1f);
        }
    }

    void OnCollisionStay (Collision collision)
    {
        int numContacts = collision.GetContacts(_contacts);
        for (int i = 0; i < numContacts; ++i)
        {
            ContactPoint cp = _contacts[i];
            Debug.DrawLine(cp.point, cp.point + cp.normal * normalScale, normalColor);
        }
    }
}