using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
public class ScanArmsAdvanced : Scan
    {
        [Header("Arm Configuration")]
        [SerializeField] int armCount = 11;
        [SerializeField] float armLength = 2f;
        [SerializeField] Vector2 armLengthCoef = new Vector2(1, 1);
        [SerializeField] int armPoints = 4;
        
        [Header("Movement Adaptation")]
        [SerializeField] SpiderController controller; // Replaced Player3D with SpiderController
        [SerializeField] float armLengthSpeedCoef;
        
        [Header("Scanning Settings")]
        [SerializeField] bool weightByDist;
        [SerializeField, Range(0, 360)] float arcAngle = 270;
        [SerializeField] int arcResolution = 4;
        [SerializeField] LayerMask arcLayer;
        
        [Header("Debug Visualization")]
        [SerializeField] bool gizmoDrawPoint = true;
        [SerializeField] bool gizmoDrawLink = true;
        
        private void OnDrawGizmosSelected()
        {
            Scan(true);
        }
        
        public override List<(Vector3 pos, Quaternion rot, float weight)> Points()
        {
            return Scan(false);
        }
        
        // Removed the default parameter value since it was redundant
        private List<(Vector3, Quaternion, float)> Scan(bool gizmo)
        {
            List<(Vector3 pos, Quaternion rot, float weight)> points = new List<(Vector3 pos, Quaternion rot, float weight)>();
            
            for (int i = 0; i < armCount; i++)
            {
                float angle = 360f * i / armCount; // Added 'f' to prevent loss of fraction
                float rad = angle * Mathf.Deg2Rad;
                
                // Calculate base arm radius with appropriate order of operations
                float arcRadius = armLength / armPoints;
                float cosComponent = Mathf.Pow(Mathf.Cos(rad), 2) * armLengthCoef.y;
                float sinComponent = Mathf.Pow(Mathf.Sin(rad), 2) * armLengthCoef.x;
                arcRadius *= Mathf.Sqrt(cosComponent + sinComponent);
                
                // Adjust radius based on controller velocity if available
                if (controller != null && controller.Velocity != Vector2.zero)
                {
                    // Create a 3D velocity vector from the controller's 2D velocity
                    Vector3 velocity3D = new Vector3(controller.Velocity.x, 0, controller.Velocity.y);
                    float angleArmVelocity = Vector3.Angle(velocity3D, Quaternion.Euler(0, angle, 0) * Vector3.forward);
                    float progress = Mathf.InverseLerp(90, 0, angleArmVelocity);
                    arcRadius += progress * controller.Speed * armLengthSpeedCoef;
                }
                
                Vector3 pos = transform.position; // Removed 'this.'
                Quaternion rot = transform.rotation * Quaternion.Euler(0, angle, 0); // Removed 'this.'
                
                for (int j = 0; j < armPoints; j++)
                {
                    if (!PhysicsExtension.ArcCast(pos, rot, arcAngle, arcRadius, arcResolution, arcLayer, out RaycastHit hit))
                    {
                        break;
                    }
                    
                    float weight = weightByDist ? 1 - (float)j / armPoints : 1;
                    
                    if (gizmo)
                    {
                        Gizmos.color = new Color(1, 1, 1, weight);
                        
                        if (gizmoDrawLink)
                        {
                            Gizmos.DrawLine(pos, hit.point);
                        }
                    }
                    
                    pos = hit.point;
                    rot.MatchUp(hit.normal);
                    
                    points.Add((pos, rot * Quaternion.Euler(0, -angle, 0), weight));
                    
                    if (gizmo && gizmoDrawPoint)
                    {
                        Gizmos.DrawSphere(pos, 0.1f);
                    }
                }
            }
            
            return points;
        }
    }
}