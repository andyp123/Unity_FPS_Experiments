using System;
using UnityEngine;

[RequireComponent (typeof (Camera))]
public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 100.0f;
    public float clampAngle = 80.0f;

    private Camera _camera;
    private float _rotY = 0.0f; // rotation around the up/y axis
    private float _rotX = 0.0f; // rotation around the right/x axis

    void Start ()
    {
        _camera = gameObject.GetComponent<Camera>();

        Vector3 euler = transform.localRotation.eulerAngles;
        _rotY = euler.y;
        _rotX = euler.x;
    }

    void Update ()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = -Input.GetAxis("Mouse Y");

        _rotY += mouseX * mouseSensitivity * Time.deltaTime;
        _rotX += mouseY * mouseSensitivity * Time.deltaTime;

        _rotX = Mathf.Clamp(_rotX, -clampAngle, clampAngle);

        Quaternion localRotation = Quaternion.Euler(_rotX, _rotY, 0.0f);
        transform.rotation = localRotation;
    }
}
