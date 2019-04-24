using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MathNet.Numerics;

//Primary source:
//http://publications.jrc.ec.europa.eu/repository/bitstream/JRC72550/cnossos-eu%20jrc%20reference%20report_final_on%20line%20version_10%20august%202012.pdf

//Converts a noise propagation tree into a list of image sources
public class NMBP2008{

    enum stepType { source, listener, propagation, reflection, diffraction, diffractionVerticalEdge };

    //Step in propagation paths, add any necessary storage attributes here
    private struct propagationStep
    {
        public float distance; // Distance from source
        public float groundLevel; // Ground height in world space
        public float height; // Relative height over ground
        public stepType type; 
    }

    private NoiseController currController;
    private NoiseSource currSource;
    private noiseParameters currVehicle;
    List<ImageSource> images;

    public NMBP2008()
    {
    }

    public List<ImageSource> calculateNoise(NoiseController controller,  NoiseSource src, noiseParameters vehicleParams)
    {
        currController = controller;
        currSource = src;
        currVehicle = vehicleParams;
        images = new List<ImageSource>();
        List<propagationStep> start = new List<propagationStep>();

        //turn propagation paths into image sources
        //Recursively work through to the leafs, then add to images
        foreach(TreeNode<NoiseSource> node in src.node.Children)
        {
            pathFromTo(src.node, src.node, node, 0, 0, start);
        }
        //After this loop, the list of images is populated
        return images;
    }

    //1m spaced 2D steps
    //PathStart: Where the whole path started, to get the direct distance in the end
    //StepStart: Start point of the current iteration
    private void pathFromTo(TreeNode<NoiseSource> pathStart, TreeNode<NoiseSource> stepStart, TreeNode<NoiseSource> end, float prevDist, float remainingStep, List<propagationStep> list)
    {
        //Get start and end nodes in 2D
        Vector3 start2D = new Vector3(stepStart.Value.origin.x, 0, stepStart.Value.origin.z);
        Vector3 end2D = new Vector3(end.Value.origin.x, 0, end.Value.origin.z);
        //3D and 2D direction to the next node
        Vector3 dir = (end.Value.origin - stepStart.Value.origin).normalized;
        Vector3 dir2D = (end2D - start2D).normalized;
        //Distance we have to travel in 3D and 2D
        float dist = Vector3.Distance(stepStart.Value.origin, end.Value.origin);
        float dist2D = Vector3.Distance(start2D, end2D);

        //1 meter steps, and on each step we record absolute and relative height and current overall distance
        for (float i = 0 + remainingStep; i < dist2D; i++)
        {
            //Move one step in the 2D direction
            Vector3 currPoint2D = start2D + (dir2D * i);
            //Find fraction of way passed
            float fractionPassed = Vector3.Distance(start2D, currPoint2D) / Vector3.Distance(start2D, end2D);
            //Step that fraction along the 3D direction to get current 3D point
            Vector3 currPos = stepStart.Value.origin + (dir * (dist * fractionPassed));
   
            RaycastHit hit;
            if (Physics.Raycast(currPos, Vector3.down, out hit, currController.maximumDistance, currController.terrainMask))
            {
                float currHeight = hit.distance;
                propagationStep currStep;
                currStep.distance = prevDist + i;
                currStep.groundLevel = hit.point.y;
                currStep.height = currHeight;
                // If the distance is still zero, this is the beginning of the whole path 
                if (currStep.distance == 0)
                {
                    currStep.type = stepType.source;
                } 
                else if (Mathf.Approximately(currStep.distance, prevDist + remainingStep))
                {
                    //If this is the same distance as before with the rest of the 1m step added
                    //then this is the start of the new segment. And because this is a new segment
                    //this means that this is either a diffraction or a reflection
                    //Note that because of the regular 1m step interval this isn't always placed precisely 
                    //at the original incident point
                    if (stepStart.Value.type == NoiseSource.HitType.DIFFRACTION_V)
                    {
                        currStep.type = stepType.diffraction;
                    } else if (stepStart.Value.type == NoiseSource.HitType.DIFFRACTION_H)
                    {
                        currStep.type = stepType.diffractionVerticalEdge;
                    }
                    else
                    {
                        currStep.type = stepType.reflection;
                    }
                }
                else
                {
                    currStep.type = stepType.propagation;
                }
                list.Add(currStep);
                if (currController.debugMode)
                {
                    Debug.DrawRay(currPos, Vector3.down, Color.magenta);
                }
            } else { return; }
            //At final iteration, record remaining distance for the last step
            if(i + 1 >= dist2D)
            {
                remainingStep = 1 - (dist2D - i);
            }
        }
        //Now we stepped up to the end of this path, and we are either at the end point, or go on towards the next path
        if (end.Value.type == NoiseSource.HitType.DIRECT)
        {
            //Finish the path
            RaycastHit lastHit;
            if (Physics.Raycast(end.Value.origin, Vector3.down, out lastHit, currController.maximumDistance, currController.terrainMask))
            {
                float currHeight = lastHit.distance;
                propagationStep lastStep;
                //Make sure that the last step is 1m too, for the calculations, even if it overshoots a bit
                lastStep.distance = prevDist + dist2D + remainingStep;
                lastStep.groundLevel = lastHit.point.y;
                lastStep.height = currHeight;
                lastStep.type = stepType.listener;
                list.Add(lastStep);
            }
            //Add to list of valid paths. Calculate direct distance between path source and end point
            float directPathDistance = Vector3.Distance(pathStart.Value.origin, end.Value.origin);
            createImage(list, dir, directPathDistance);
        }
        else
        {
            foreach (TreeNode<NoiseSource> node in end.Children)
            {
                pathFromTo(pathStart, end, node, prevDist + dist2D, remainingStep, list);
            }
        }
    }

    private float calculateGeometricDivergence(float distance, int octaveBand)
    {
        //Get direct distance from last piece of the path
        return 20 * Mathf.Log10(distance) + 11.0f;
    }

    private float calculateAtmosphericAbsorption(List<propagationStep> path, int octaveBand)
    {
        //NOTE: Values for 20°C and 70% humidity, according to the standard it should be 15°C, but should be very close
        //For more accurate values, see ISO 9631-1
        float a;
        if (octaveBand == 63)
        {
            a = 0.09f;
        }
        else if (octaveBand == 125)
        {
            a = 0.26f;
        }
        else if (octaveBand == 250)
        {
            a = 1.13f;
        }
        else if (octaveBand == 500)
        {
            a = 2.80f;
        }
        else if (octaveBand == 1000)
        {
            a = 4.98f;
        }
        else if (octaveBand == 2000)
        {
            a = 9.02f;
        }
        else if (octaveBand == 4000)
        {
            a = 22.9f;
        }
        else if (octaveBand == 8000)
        {
            a = 76.6f;
        }
        else
        {
            a = 1f;
        }
        
        return a * path.Last().distance/1000.0f;
    }

    //Returns the closest point between the mean plane and a point
    private Vector2 crossMeanPlane(Vector2 currPoint, Vector2 start, Vector2 end)
    {
        float u = ((currPoint.x - start.x) * (end.x - start.x) + (currPoint.y - start.y) * (end.y - start.y)) / Mathf.Pow(Vector2.Distance(start, end), 2);
        Vector2 intersection;
        intersection.x = start.x + u * (end.x - start.x);
        intersection.y = start.y + u * (end.y - start.y);
        return intersection;
    }

    //Pure diffraction calculation, needs both left and right ground plane for the h0 calculations
    private float calculateDiffraction(Vector2 source, Vector2 receiver, int octaveBand, List<Vector2> diffractionPoints, Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2)
    {
        //Calculate pure diffraction
        //Height of diffraction points over mean ground plane:
        float lefth0 = Vector2.Distance(diffractionPoints.First(), crossMeanPlane(diffractionPoints.First(), start1, end1));
        float righth0 = Vector2.Distance(diffractionPoints.Last(), crossMeanPlane(diffractionPoints.Last(), start2, end2));
        float h0 = Mathf.Max(lefth0, righth0);
        //VI-22
        float Ch = Mathf.Min((octaveBand * h0) / 250, 1);
        //Wavelength of current octave band:
        float lambda = 340.0f / octaveBand;
        //Multi-diffraction coefficient
        float C1;
        float e = 0; //Distance over multiple diffractions
        if (diffractionPoints.Count == 1)
        {
            C1 = 1;
        }
        else
        {
            //Get distance spanning over the points
            e = convexDistance(diffractionPoints);
            //See if the distance is long enough to matter
            if (e > 0.3f)
            {
                C1 = (1 + Mathf.Pow((5f * lambda / e), 2)) / ((1 / 3) + Mathf.Pow((5f * lambda / e), 2));
            }
            else
            {
                C1 = 1;
            }
        }

        //Path difference, VI.4.4.c.1
        float delta;
        if (diffractionPoints.Count == 1)
        {
            Vector2 diffpt = diffractionPoints.First();
            //Type 1 or 2
            //Check whether diffraction point is above or below direct line
            if (isLeft(source, receiver, diffpt))
            {
                delta = Vector2.Distance(source, diffpt) + Vector2.Distance(diffpt, receiver) - Vector2.Distance(source, receiver);
            }
            else
            {
                delta = -(Vector2.Distance(source, diffpt) + Vector2.Distance(diffpt, receiver) - Vector2.Distance(source, receiver));
            }
        }
        else
        {
            //Type 3 or 4
            int diffNr = diffractionPoints.Count;
            Vector2 firstDiff = diffractionPoints.First();
            Vector2 lastDiff = diffractionPoints.Last();
            delta = Vector2.Distance(source, firstDiff) + e + Vector2.Distance(lastDiff, receiver) - Vector2.Distance(source, receiver);
        }
        //Diffraction equation VI-21
        float diffraction = 0;
        if (((40 / lambda) * C1 * delta) >= -2)
        {
            diffraction = 10 * Ch * Mathf.Log10(3 + (40 / lambda) * C1 * delta);
        }
        return diffraction;
    }

    //Pure ground effect calculation
    //Start and End are definition points for the mean plane
    private float calculateGroundEffect(Vector2 source, Vector2 receiver, int octaveBand, Vector2 start, Vector2 end)
    {
        //Section VI.4.3.b.
        //Ground absorption
        //Project source and receiver onto the mean plane
        Vector2 eqSrc = crossMeanPlane(source, start, end);
        Vector2 eqRec = crossMeanPlane(receiver, start, end);
        //Determine their height, set to zero if negative
        float zSrc = Mathf.Max(Vector2.Distance(source, eqSrc), 0);
        float zRec = Mathf.Max(Vector2.Distance(receiver, eqRec), 0);
        float dp = Vector2.Distance(eqSrc, eqRec);
        //Set Gpath according to VI-14 (No ground class data available, so approximated as 0.5)
        float Gpath = 0.5f;
        if (dp <= 30 * (zSrc + zRec))
        {
            Gpath = Gpath * ((dp) / (30 * (zSrc + zRec)));
        }

        float k = (2 * Mathf.PI * octaveBand) / 340;
        //VI-17
        float w = 0.0185f * (Mathf.Pow(octaveBand, 2.5f) * Mathf.Pow(Gpath, 2.6f))
            / (Mathf.Pow(octaveBand, 1.5f) * Mathf.Pow(Gpath, 2.6f)
                + 1.3f * Mathf.Pow(10, 3) * Mathf.Pow(octaveBand, 0.75f) * Mathf.Pow(Gpath, 1.3f)
                + 1.16f * Mathf.Pow(10, 6)
                );
        //VI-16
        float Cf = dp * (1 + 3 * w * dp * Mathf.Exp(-Mathf.Sqrt(w * dp))) / (1 + w * dp);

        //VI-15
        float groundEffect = -10 * Mathf.Log10(4 * (Mathf.Pow(k, 2)) / (Mathf.Pow(dp, 2))
            * (Mathf.Pow(zSrc, 2) - Mathf.Sqrt((2 * Cf) / (k)) * zSrc + (Cf) / (k))
            * (Mathf.Pow(zRec, 2) - Mathf.Sqrt((2 * Cf) / (k)) * zRec + (Cf) / (k))
            );
        //Check against lower bound
        groundEffect = Mathf.Max(groundEffect, -3 * (1 - Gpath));

        return groundEffect;
    }

    private float calculateDownwardRefraction(List<propagationStep> path, int octaveBand)
    {
        return 0.0f;
    }

    private float calculateHomogenousConditions(List<propagationStep> path, int octaveBand)
    {
        //Calculate mean plane through linear regression
        Vector2 start = new Vector2();
        Vector2 end = new Vector2();
        calcMeanPlane(path, out start, out end);

        //Get 2D source and receiver coordinates
        Vector2 source = new Vector2(path.First().distance, path.First().groundLevel + path.First().height);
        Vector2 receiver = new Vector2(path.Last().distance, path.Last().groundLevel + path.Last().height);

        //Calculate ground effect
        float groundEffect = calculateGroundEffect(source, receiver, octaveBand, start, end);

        return groundEffect;
    }

    private float calculateDownwardRefractionDiffracting(List<propagationStep> path, int octaveBand)
    {
        return 0.0f;
    }

    private float calculateHomogenousConditionsDiffracting(List<propagationStep> path, int octaveBand)
    {
        //Find diffraction points
        List<int> diffractionPoints = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i].type == stepType.diffraction || path[i].type == stepType.diffractionVerticalEdge)
            {
                diffractionPoints.Add(i);
            }
        }

        //Get point of each diffraction edge
        List<Vector2> diffpts = new List<Vector2>();
        foreach (int i in diffractionPoints)
        {
            diffpts.Add(new Vector2(path[i].distance, path[i].groundLevel + path[i].height));
        }
        //Source and receiver
        Vector2 source = new Vector2(path.First().distance, path.First().groundLevel + path.First().height);
        Vector2 receiver = new Vector2(path.Last().distance, path.Last().groundLevel + path.Last().height);

        //Calculate mean planes for the first and last diffraction points
        Vector2 start, diff1, diff2, end;
        calcMeanPlane(path.GetRange(0, diffractionPoints.First()), out start, out diff1);
        calcMeanPlane(path.GetRange(diffractionPoints.Last(), (path.Count - 1)-diffractionPoints.Last()), out diff2, out end);


        //Pure diffraction
        float pureDiff = calculateDiffraction(source, receiver, octaveBand, diffpts, start, diff1, diff2, end);
        //Keep in bounds
        pureDiff = Mathf.Max(Mathf.Min(pureDiff, 25), 0);

        //Ground attenuation between source and first diffraction point:
        float sourceAttenuation = calculateGroundEffect(
            source, new Vector2(path[diffractionPoints.First()].distance, path[diffractionPoints.First()].groundLevel + path[diffractionPoints[0]].height),
            octaveBand, start, diff1);
        //Diffractions between image source and receiver:
        Vector2 projectedSource = crossMeanPlane(source, start, diff1);
        Vector2 imageSource = projectedSource -(source - projectedSource);
        //Give same mean plane twice, because there only is one in this case
        float imageSourceDiff = calculateDiffraction(imageSource, receiver, octaveBand, diffpts, start, diff1, start, diff1);
        //VI-31
        float groundSO = -20 * Mathf.Log10(1 + (Mathf.Pow(10, -sourceAttenuation / 20) - 1)
            * Mathf.Pow(10, -(imageSourceDiff - pureDiff) / 20));

        //Ground attenuation between last diffraction point and receiver
        float receiverAttenuation = calculateGroundEffect(
            new Vector2(path[diffractionPoints.Last()].distance, path[diffractionPoints.Last()].groundLevel + path[diffractionPoints.Last()].height),
            receiver,
            octaveBand, diff2, end);
        //Diffractions between source and image receiver
        Vector2 projectedReceiver = crossMeanPlane(receiver, diff2, end);
        Vector2 imageReceiver = projectedReceiver - (receiver - projectedReceiver);
        //Give same mean plane twice, because there only is one in this case
        float imageReceiverDiff = calculateDiffraction(source, imageReceiver, octaveBand, diffpts, start, diff1, diff2, end);
        //VI-32
        float groundOR = -20 * Mathf.Log10(1 + (Mathf.Pow(10, -receiverAttenuation / 20) - 1)
            * Mathf.Pow(10, -(imageReceiverDiff - pureDiff) / 20));

        //Final attenuation calculation
        float diffAttenuation = pureDiff + groundSO + groundOR;

        return diffAttenuation;
    }

    private float calculateDownwardRefractionLateralDiffracting(List<propagationStep> path, int octaveBand)
    {
        return 0.0f;
    }

    private float calculateHomogenousConditionsLateralDiffracting(List<propagationStep> path, int octaveBand)
    {
        //Combining ground effect of the direct path and diffraction of the diffracated path, according to V-33
        float a_ground = calculateHomogenousConditions(path, octaveBand);

        float a_dif= calculateHomogenousConditionsDiffracting(path, octaveBand);
    
        return a_ground+a_dif;
    }

    private void calcMeanPlane(List<propagationStep> path, out Vector2 start, out Vector2 end)
    {
        List<double> xdata = new List<double>();
        List<double> ydata = new List<double>();
        foreach (propagationStep step in path)
        {
            xdata.Add(step.distance);
            ydata.Add(step.groundLevel);
        }
        //Check if there is only one point, then set the mean plane to a straight line
        if (xdata.Count < 2 || ydata.Count < 2)
        {
            start = new Vector2((float)xdata[0], (float)ydata[0]);
            end = start + Vector2.right;
            return;
        }
        //If there are more then two, we need to use numerics
        System.Tuple<double,double> p = Fit.Line(xdata.ToArray(), ydata.ToArray());
        //In this tuple we have the coefficients of the line equation a and b
        //Translate into start and end vectors
        float a = (float) p.Item1;
        float b = (float) p.Item2;
        start.x = path.First().distance;
        start.y = a + path.First().distance * b;
        end.x = path.Last().distance;
        end.y = a + path.Last().distance * b;
        return;
    }

    private bool isLeft(Vector2 a, Vector2 b, Vector2 c)
    {
        //Check whether c is above the line ab
        return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 0;
    }

    private float convexDistance(List<Vector2> pts)
    {
        //Returns the length of a convex line over an ordered set of points
        bool complete = false;
        int currPt = 0;
        float distance = 0;
        //Avoid endless loops: maximum path length divided by resolution is the maximum amount of iterations possible 
        int maxCount = (int) currController.maximumDistance / currController.rayResolution;
        while (!complete)
        {
            maxCount--;
            float maxAngle = float.MinValue;
            int maxPt = currPt;
            for(int i = currPt + 1; i < pts.Count; i++)
            {
                Vector2 dir = pts[i] - pts[currPt];
                float angle = Vector2.Angle(Vector2.right, dir);
                if (angle > maxAngle)
                {
                    maxAngle = angle;
                    maxPt = i;
                }
            }
            distance = distance + Vector2.Distance(pts[currPt], pts[maxPt]);
            //Check if we reached the end
            if (maxPt == pts.Count-1)
            {
                complete = true;
            } else
            {
                currPt = maxPt;
            }
            if (maxCount <= 0) { break; }
        }
        return distance;
    }

    private void createImage(List<propagationStep> path, Vector3 incomingDir, float directPathDistance)
    {
        if(path.Count == 0)
        {
            return;
        }
        GameObject listener = currController.listener;
        ImageSource image = new ImageSource();
        image.soundLevel = new Dictionary<int, float>();
        image.geometricDivergence = new Dictionary<int, float>();
        Dictionary<int, float> soundEmission = currVehicle.soundEmission(currController.temperature);
        
        //Check for diffraction and reflection
        bool hasDiffraction = false;
        bool hasLateralDiffraction = false;
        bool hasReflection = false;
        foreach (propagationStep s in path)
        {
            if(s.type == stepType.diffraction)
            {
                hasDiffraction = true;
            }
            if(s.type == stepType.diffractionVerticalEdge)
            {
                hasLateralDiffraction = true;
            }
            if(s.type == stepType.reflection)
            {
                hasReflection = true;
            }
            
        }
        //Go through each octave band
        foreach (KeyValuePair<int, float> entry in soundEmission)
        {
            //Calculate the three coefficients for attenuation of sound power
            float soundPower = entry.Value;
            if (hasReflection)
            {
                //Only first order reflections modelled, so the calculation is simple
                //Absorption coefficient assumed as 0.10 here, should be read from building data if possible
                float absorptionCoefficient = 0.10f;
                soundPower = soundPower + 10 * Mathf.Log10(1 - absorptionCoefficient);
            }
            float geometricDivergence;
            if (hasLateralDiffraction)
            {
                geometricDivergence = calculateGeometricDivergence(directPathDistance, entry.Key);
                image.source = listener.transform.position - (incomingDir * directPathDistance);
                image.geometricDivergence[entry.Key] = geometricDivergence;
            }
            else
            {
                geometricDivergence = calculateGeometricDivergence(path.Last().distance, entry.Key);
                image.source = listener.transform.position - (incomingDir * path.Last().distance);
                image.geometricDivergence[entry.Key] = geometricDivergence;
            }
            float atmosphericAbsorption = calculateAtmosphericAbsorption(path, entry.Key);
            float refractiveSoundLevel = 0.0f, homogenousSoundLevel = 0.0f, finalSoundLevel = 0.0f;
            //Check which sound levels need to be calculated
            if (currController.longTerm || currController.downwardRefraction)
            {
                float boundaryAttenuation;
                if (hasDiffraction)
                {
                    boundaryAttenuation = calculateDownwardRefractionDiffracting(path, entry.Key);
                } else if (hasLateralDiffraction)
                {
                    boundaryAttenuation = calculateDownwardRefractionLateralDiffracting(path, entry.Key);
                }
                else
                {
                    boundaryAttenuation = calculateDownwardRefraction(path, entry.Key);
                }
                float attenuation = (geometricDivergence + atmosphericAbsorption + boundaryAttenuation);
                refractiveSoundLevel = entry.Value - attenuation;
            }
            if (currController.longTerm || !currController.downwardRefraction)
            {
                float boundaryAttenuation;
                if (hasDiffraction)
                {
                    boundaryAttenuation = calculateHomogenousConditionsDiffracting(path, entry.Key);
                } 
                else if (hasLateralDiffraction)
                {
                    boundaryAttenuation = calculateHomogenousConditionsLateralDiffracting(path, entry.Key);
                }
                else
                {
                    boundaryAttenuation = calculateHomogenousConditions(path, entry.Key);
                }
                float attenuation = (geometricDivergence + atmosphericAbsorption + boundaryAttenuation);
                homogenousSoundLevel =  entry.Value - attenuation;
                //Debug.Log("Dezibel: " + (entry.Value - geometricDivergence - atmosphericAbsorption) + " / Boundary: " + boundaryAttenuation);
            }
            //Long term sound level or short?
            if (currController.longTerm)
            {
                //Weight according to likelihood
                finalSoundLevel = 10.0f * Mathf.Log10(
                    currController.downwardLikelihood * Mathf.Pow(10.0f, refractiveSoundLevel / 10.0f)
                    + (1 - currController.homogenousLikelihood) * Mathf.Pow(10.0f, homogenousSoundLevel / 10.0f)
                    );
            }
            else if(currController.downwardRefraction)
            {
                finalSoundLevel = refractiveSoundLevel;
            }
            else
            {
                finalSoundLevel = homogenousSoundLevel;
            }

            image.soundLevel[entry.Key] = finalSoundLevel;
        }
        //Add all the calculated values to the list of images for this source.
        images.Add(image);
    }

}
