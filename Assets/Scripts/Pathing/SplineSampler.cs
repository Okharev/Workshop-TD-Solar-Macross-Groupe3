using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class SplineSampler : MonoBehaviour
{
    [SerializeField] private SplineContainer _SplineContainer;

    private float3 _forward;

    private float3 _position;
    private Vector3 _roadLeft;

    private Vector3 _roadRight;
    private float3 _upVector;

    public int NumSplines => _SplineContainer.Splines.Count;

    public void SampleSplineWidth(int splineIndex, float t, float desiredWidth, out Vector3 rightPt, out Vector3 leftPt)
    {
        _SplineContainer.Evaluate(splineIndex, t, out _position, out _forward, out _upVector);
        float3 right = Vector3.Cross(_forward, _upVector).normalized;
        rightPt = _position + right * desiredWidth;
        leftPt = _position + -right * desiredWidth;
    }
}