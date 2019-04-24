using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseSource{
    
    public TreeNode<NoiseSource> node;
    public NoiseController controller;
    public Vector3 origin;
    public bool debug = false;
    public Collider hitObject = null;
    public enum HitType {SOURCE, DIRECT, REFLECTION, DIFFRACTION_H, DIFFRACTION_V};
    public HitType type = HitType.SOURCE;
 
	// Use this for initialization
	void Start () {

	}

    public void fireAt(GameObject listener, float maxDist, string direction = "", Collider lastHit = null)
    {
        RaycastHit hit;
        if (Physics.Linecast(origin, listener.transform.position, out hit, controller.reflectionMask))
        {
            //If full path is wanted, reset direction after each building
            if (controller.fullPath && hit.collider != lastHit)
            {
                direction = "";
            }
            lastHit = hit.collider;
            //Push target in a centimeter, so that collisions work
            Vector3 target = hit.point - (hit.normal * 0.01f);
            //Build coordinate system for impact point, use up as reference
            Vector3 side;
            Vector3 up;
            if (hit.normal == Vector3.up)
            {
                //Regain direction by turning one of up's orthogonal axes to face the listener
                up = (listener.transform.position - hit.point);
                up.y = 0;
                up.Normalize();
                side = Vector3.Cross(hit.normal, up).normalized;
            }
            else
            {
                side = Vector3.Cross(hit.normal, Vector3.up).normalized;
                up = Vector3.Cross(side, hit.normal).normalized;
            }
            if (debug) { Debug.DrawLine(hit.point, hit.point + hit.normal, Color.magenta, controller.period); }
            if (debug) { Debug.DrawLine(hit.point, hit.point + side, Color.magenta, controller.period); }
            if (debug) { Debug.DrawLine(hit.point, hit.point + up, Color.magenta, controller.period); }

            //If no direction given, check all four orthgonal vectors
            if (direction == "")
            {
                if (controller.sidewaysDiffraction)
                {
                    scanCast(listener, target, Quaternion.AngleAxis(90, up) * hit.normal, maxDist, "right", lastHit);
                    scanCast(listener, target, Quaternion.AngleAxis(-90, up) * hit.normal, maxDist, "left", lastHit);
                }
                scanCast(listener, target, Quaternion.AngleAxis(90, side) * hit.normal, maxDist, "up", lastHit);
                if (controller.downwardDiffraction)
                {
                    scanCast(listener, target, Quaternion.AngleAxis(-90, side) * hit.normal, maxDist, "down", lastHit);
                }
                
            }
            else if(direction == "right" && controller.sidewaysDiffraction)
            {
                scanCast(listener, target, Quaternion.AngleAxis(90, up) * hit.normal, maxDist, "right", lastHit);
            }
            else if(direction == "left" && controller.sidewaysDiffraction)
            {
                scanCast(listener, target, Quaternion.AngleAxis(-90, up) * hit.normal, maxDist, "left", lastHit);
            }
            else if (direction == "up")
            {
                scanCast(listener, target, Quaternion.AngleAxis(90, side) * hit.normal, maxDist, "up", lastHit);
            }
            else if (direction == "down" && controller.downwardDiffraction)
            {
                scanCast(listener, target, Quaternion.AngleAxis(-90, side) * hit.normal, maxDist, "down", lastHit);
            }
            //If no path reached the listener, remove from noise tree
            if(node.Children.Count == 0)
            {
                node.RemoveSelf();
            }
        }
        else
        {
            if(Vector3.Distance(listener.transform.position, origin) > maxDist)
            {
                if (debug) { Debug.DrawLine(origin, listener.transform.position, Color.yellow, 0.1f); }
                node.RemoveSelf();
            }
            else
            {
                //Hit!
                if (debug) { Debug.DrawLine(origin, listener.transform.position, Color.green, 0.1f); }
                NoiseSource finalSegment = new NoiseSource();
                finalSegment.origin = listener.transform.position;
                finalSegment.debug = debug;
                finalSegment.node = node.AddChild(finalSegment);
                finalSegment.controller = controller;
                finalSegment.type = HitType.DIRECT;
                finalSegment.hitObject = null;
            }
        }
    }

    //Diffractions
    private void scanCast(GameObject listener, Vector3 target, Vector3 direction, float maxDist, string dirName, Collider lastHit)
    {
        for (int i = 0; i < controller.collisionChecks; i++)
        {
            RaycastHit hit;
            //Move target up in each step
            target += direction * controller.rayResolution;
            //Once it hits only air, continue
            if (!Physics.Linecast(origin, target, out hit, controller.reflectionMask))
            {
                //Stop if the sound has travelled too far
                maxDist = maxDist - Vector3.Distance(origin, target);
                if (maxDist < 0)
                {
                    if (debug) { Debug.DrawLine(origin, target, Color.yellow, controller.period); }
                    return;
                }
                //Stop, if new source is farther from the target than the current one
                if (Vector3.Distance(listener.transform.position, origin) < Vector3.Distance(listener.transform.position, target))
                {
                    if (debug) { Debug.DrawLine(origin, target, Color.red, controller.period); }
                    return;
                }
                NoiseSource nextSegment = new NoiseSource();
                nextSegment.origin = target;
                nextSegment.debug = debug;
                nextSegment.node = node.AddChild(nextSegment);
                nextSegment.controller = controller;
                nextSegment.fireAt(listener, maxDist, dirName, lastHit);
                nextSegment.hitObject = hit.collider;
                //Save type of diffraction
                if (dirName == "left" || dirName == "right")
                {
                    nextSegment.type = HitType.DIFFRACTION_H;
                } else {
                    nextSegment.type = HitType.DIFFRACTION_V;
                }
                if (debug) { Debug.DrawLine(origin, nextSegment.origin, Color.blue, controller.period); }
                return;
            }
            lastHit = hit.collider; //For corner case, where we immediately hit another collider
        }
        //If it didn't manage to get away from the obstacle, draw red line
        //Debug.DrawLine(origin, listener.transform.position, Color.red, controller.period);
    }

    //Reflections
    public void reflectToward(GameObject listener, float maxDistance)
    {
        RaycastHit hit;
        Vector3 directionRight = listener.transform.position - origin;
        Vector3 directionLeft = listener.transform.position - origin;
        //We always need to stay on the plane between source and receiver. 
        //But in order for that plane to have the correct "roll" we need another reference vector
        //This is the up vector. To make this work in all 3D configurations, 
        //we need to "pitch" the Up vector back until it is orthogonal to our direction vector
        Vector3 axis;
        if (!directionRight.Equals(Vector3.up))
        {
            axis = Vector3.Cross(directionRight, Vector3.up); // This gives us a sideways vector
            axis = Vector3.Cross(directionRight, axis).normalized; //This gives us the new upwards vector
        }
        else
        {
            //If the direction is directly up, just choose a direction
            axis = Vector3.right;
        }
        
        Quaternion rightStep = Quaternion.AngleAxis(1/controller.angleSteps, axis);
        Quaternion leftStep = Quaternion.AngleAxis(-1/controller.angleSteps, axis);
        for(int i = 0; i < controller.reflectionAngle * controller.angleSteps; i++)
        {
            directionRight = rightStep * directionRight;
            //Debug.DrawLine(origin, origin+directionRight, Color.red, controller.period);
            if (Physics.Raycast(origin, directionRight, out hit, controller.maximumDistance, controller.reflectionMask))
            {
                if (debug) { Debug.DrawLine(origin, hit.point, Color.cyan, controller.period); }
                Vector3 reflectionPoint = hit.point + (hit.normal * 0.01f);
                //Reflection angle check
                if (Mathf.Abs(Vector3.SignedAngle(hit.normal, hit.point - origin, axis) + Vector3.SignedAngle(hit.normal, hit.point - listener.transform.position, axis)) > controller.specularTolerance)
                {
                    if (debug)
                    {
                        Debug.DrawLine(origin, reflectionPoint, Color.cyan, controller.period);
                        Debug.DrawLine(reflectionPoint, listener.transform.position, Color.cyan, controller.period);
                    }
                }
                else
                {
                    NoiseSource nextSegment = new NoiseSource();
                    nextSegment.origin = reflectionPoint;
                    nextSegment.debug = debug;
                    nextSegment.node = node.AddChild(nextSegment);
                    nextSegment.controller = controller;
                    nextSegment.type = HitType.REFLECTION;
                    nextSegment.hitObject = hit.collider;
                    nextSegment.fireAt(listener, maxDistance - Vector3.Distance(origin, hit.point), "right", hit.collider);
                }
            }
            directionLeft = leftStep * directionLeft;
            //Debug.DrawLine(origin, origin + directionLeft, Color.red, controller.period);
            if (Physics.Raycast(origin, directionLeft, out hit, controller.maximumDistance, controller.reflectionMask))
            {
                if (debug) { Debug.DrawLine(origin, hit.point, Color.cyan, controller.period); }
                Vector3 reflectionPoint = hit.point + (hit.normal * 0.01f);
                //Reflection angle check
                if (Mathf.Abs(Vector3.SignedAngle(hit.normal, hit.point - origin, axis) + Vector3.SignedAngle(hit.normal, hit.point - listener.transform.position, axis)) > controller.specularTolerance)
                {
                    if (debug)
                    {
                        Debug.DrawLine(origin, reflectionPoint, Color.cyan, controller.period);
                        Debug.DrawLine(reflectionPoint, listener.transform.position, Color.cyan, controller.period);
                    }
                }
                else
                {
                    NoiseSource nextSegment = new NoiseSource();
                    nextSegment.origin = reflectionPoint;
                    nextSegment.debug = debug;
                    nextSegment.node = node.AddChild(nextSegment);
                    nextSegment.controller = controller;
                    nextSegment.type = HitType.REFLECTION;
                    nextSegment.hitObject = hit.collider;
                    nextSegment.fireAt(listener, maxDistance - Vector3.Distance(origin, hit.point), "left", hit.collider);
                }
            }
        }
    }

}
