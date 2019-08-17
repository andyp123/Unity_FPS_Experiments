using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Platform : MonoBehaviour
{
    public List<Vector3> points;
    public float maxSpeed = 2f;
    public float acceleration = 1f;
    public bool startAtFirstPoint = true;
    public bool loop = true;

    private Rigidbody _rigidbody;
    private Vector3 _target;
    private int _targetIndex = 0;
    private int _dir = 1;

    void Awake ()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;

        if (points == null)
        {
            points = new List<Vector3>();
        }

        if (points.Count > 0)
        {
            if (startAtFirstPoint)
            {
                transform.position = points[0];
            }
            _target = points[0];
        }
        else
        {
            _target = transform.position;
        }
    }

    bool AtTarget
    {
        get {
            float distTolerance = 0.01f;
            return (_target - transform.position).sqrMagnitude < distTolerance * distTolerance;
        }
    }

    float GetAccelDistance (float u, float v, float a)
    {
        return Mathf.Abs((v * v - u * u) / (2 * a));
    }

    void FixedUpdate ()
    {
        if (points.Count < 2)
        {
            return;
        }

        if (!AtTarget)
        {
            Vector3 offset = _target - transform.position;
            float targetDistance = offset.magnitude;
            float currentSpeed = _rigidbody.velocity.magnitude;
            float stoppingDistance = GetAccelDistance(currentSpeed, 0f, acceleration);
            float accel = (stoppingDistance < targetDistance) ? acceleration : -acceleration;
            float speed = Mathf.Clamp(currentSpeed + accel * Time.deltaTime, 0f, maxSpeed);
            Vector3 moveDir = offset.normalized;
            Vector3 move = moveDir * speed * Time.deltaTime;
            _rigidbody.MovePosition(transform.position + move);
        }
        else
        {
            _rigidbody.position = _target;
            _rigidbody.velocity = Vector3.zero;
            _targetIndex += _dir;

            if (_targetIndex == points.Count)
            {
                if (loop)
                {
                    _targetIndex = 0;
                }
                else
                {
                    _targetIndex = points.Count - 2;
                    _dir = -1;
                }
            }
            else if (_targetIndex < 0)
            {
                if (loop)
                {
                    _targetIndex = points.Count - 1;
                }
                else
                {
                    _targetIndex = 1;
                    _dir = 1;
                }                
            }

            _target = points[_targetIndex];     
        }
    }
}
