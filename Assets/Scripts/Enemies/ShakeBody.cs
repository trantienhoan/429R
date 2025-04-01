using System;
//using System.Collections;
using UnityEngine;

namespace Enemies
{
[RequireComponent(typeof(SpiderController))]
public class ShakeBody : MonoBehaviour
{
    [SerializeField] Transform body;

    [SerializeField] BodyShakeData bodyShakeIdle, bodyShakeMove;



    private SpiderController spiderController;
    private Vector3 bodyLocalPos;
    private Quaternion bodyLocalRot;
    private float timeOffset;

    public Transform Body { get => this.body; }


    void Awake()
    {
        Cache();
    }

    void OnDisable()
    {
        body.localPosition = bodyLocalPos;
        body.localRotation = bodyLocalRot;
    }


    void Cache()
    {
        spiderController = GetComponent<SpiderController>(); 
        bodyLocalPos = body.localPosition;
        bodyLocalRot = body.localRotation;
        timeOffset = UnityEngine.Random.value * 1000;
    }

    void Update()
    {
        // Check for null references to avoid exceptions
        if (body == null || spiderController == null || 
            bodyShakeIdle == null || bodyShakeMove == null)
            return;

        // Use spiderController.Speed or another appropriate property
        float speedProgress = Mathf.Clamp01(spiderController.Speed);  // Assuming Speed is normalized; if not, modify as needed

        body.localPosition = bodyLocalPos + Vector3.Lerp(
            bodyShakeIdle.Pos(timeOffset), 
            bodyShakeMove.Pos(timeOffset), 
            speedProgress);
            
        body.localRotation = bodyLocalRot * Quaternion.Lerp(
            bodyShakeIdle.Rot(timeOffset), 
            bodyShakeMove.Rot(timeOffset), 
            speedProgress);
    }
    

    [Serializable]
    public class BodyShakeData
    {
        public float posNoise, rotNoise, ySin;
        public float posNoiseFreq, rotNoiseFreq, ySinFreq;

        public Vector3 Pos(float timeOffset = 0)
        {
            float time = Time.time + timeOffset;

            return new Vector3(
                (0.5f - Mathf.PerlinNoise(000, time * posNoiseFreq)) * posNoise,
                (0.5f - Mathf.PerlinNoise(100, time * posNoiseFreq)) * posNoise + Mathf.Sin(time * Mathf.PI * 2 * ySinFreq) * ySin,
                (0.5f - Mathf.PerlinNoise(200, time * posNoiseFreq)) * posNoise);
        }

        public Quaternion Rot(float timeOffset = 0)
        {
            float time = Time.time + timeOffset;

            return Quaternion.Euler(
                (0.5f - Mathf.PerlinNoise(300, time * rotNoiseFreq)) * rotNoise,
                (0.5f - Mathf.PerlinNoise(400, time * rotNoiseFreq)) * rotNoise,
                (0.5f - Mathf.PerlinNoise(500, time * rotNoiseFreq)) * rotNoise);
        }
    }
}
}