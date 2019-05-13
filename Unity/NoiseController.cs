using System.Collections.Generic;
using Mapbox.Unity.Map;
using UnityEngine;

//Creates a noise source from each child object set in the editor

public class NoiseController : MonoBehaviour {

    public GameObject listener;
    public LoadBuildings buildingLoader;
    public AbstractMap map;
    public GameObject player;
    private bool rasterGenerated = false;
    public float period = 0.1f;
    public int collisionChecks = 30;
    public int rayResolution = 1;
    public bool fullPath = false; //Combines vertical and horizontal diffraction, but this isn't part of the standard
    public LayerMask collisionMask;
    public LayerMask reflectionMask;
    public LayerMask terrainMask;
    public float maximumDistance = 800;
    public GameObject audioSourceObject; //Game object with audio source that is configured as needed (spatialization)
    public bool reflections = true;
    public bool sidewaysDiffraction = true;
    public bool downwardDiffraction = false; //Not part of the standard
    public int reflectionAngle = 180;
    public int angleSteps = 1;
    public bool debugMode = false; //Full debug that shows every collision path and diffraction point
    public bool showTree = true; //Show paths relevant to the noise as colored lines
    LineDrawer lineDrawer;
    public float lineWidth = 0.025f;
    public List<NoiseSource> currTrees = new List<NoiseSource>();
    public float specularTolerance = 0.5f; //Deviance from specular angle at which a reflection is still counted as specular
    private float maximumRaycastDistance = 200f; //For non noise-related stuff, specifically for scanning for the ground when creating the raster
    public bool raster = true;
    public int gridSize = 5;
    public int rasterRadius = 50; //Radius of the raster from the starting point of the listener
    public float rasterHeight = 2; //Raster height during noise calculation
    public float rasterHeightRender = 1f; //Raster height for visualization
    public Vector3 rasterPointScale = new Vector3(1f, 1f, 1f);
    public bool toggleData = false;
    public float scaleFactor = 0.05f;
    public float playerHeight = 2; //Player height in world scale, set to same value as teleport height
    public bool downscaled = false;
    public GameObject rasterContainer;
    private GameObject playerReference;
    private GameObject lineContainer;

    //Environmental factors
    public bool downwardRefraction = false; //Downward refractive conditions or homogenous atmospheric conditions
    public bool longTerm = false; //Sum the sound levels for long term?
    public float downwardLikelihood = 0.00f; //Chance of occurence of downward refractive conditions
    public float homogenousLikelihood = 1.00f; //Chance of occurence of homogenous atmospheric conditions
    public float temperature = 20.0f; //In celcius, technically expected is the yearly average

    // Use this for initialization
    void Start () {
        playerReference = new GameObject();
        playerReference.transform.position = player.transform.position;
        playerReference.transform.parent = map.transform;
        
        //Initialize line drawer helper
        lineDrawer = new LineDrawer();
        lineDrawer.lines = new List<LineRenderer>();
        lineContainer = new GameObject("LineContainer");
        lineContainer.transform.position = new Vector3(0, 0, 0);
    }

    // Update is called once per frame
    void Update() {
        //OVRInput.Update();
        //If the lower part of the touchpad is pressed, show data
        if (OVRInput.Get(OVRInput.Button.PrimaryTouchpad) && (OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad).y < -0.5f) && !toggleData)
        {
            rasterContainer.SetActive(true);
            showTree = true;
            toggleData = true;
        }
        else if(!OVRInput.Get(OVRInput.Button.PrimaryTouchpad) && toggleData)
        {
            rasterContainer.SetActive(false);
            showTree = false;
            //Erase existing lines
            lineDrawer.Erase();
            toggleData = false;
        }

        //Handle scale change
        if(OVRInput.GetDown(OVRInput.Button.Two) && !downscaled)
        {
            //Scale change
            downscaled = true;

            //Adjust map transform (Is shifted up/down at initialization, so position has to be scaled)
            map.transform.position = new Vector3(map.transform.position.x, map.transform.position.y * scaleFactor, map.transform.position.z);
            //Scale world
            buildingLoader.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            map.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            rasterContainer.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            
            //Set player to correct position on downscaled map (2m above reference)
            player.transform.position = playerReference.transform.position + Vector3.up * playerHeight;
        }
        else if (OVRInput.GetDown(OVRInput.Button.Two) && downscaled)
        {
            //Scale change
            downscaled = false;
            //Set player reference to where the player is standing on the downscaled map
            playerReference.transform.position = player.transform.position + Vector3.down * playerHeight;
            
            //Scale world
            buildingLoader.transform.localScale = new Vector3(1f, 1f, 1f);
            map.transform.localScale = new Vector3(1f, 1f, 1f);
            rasterContainer.transform.localScale = new Vector3(1f, 1f, 1f);
            //Adjust map transform (Is shifted up/down at initialization, so position has to be scaled)
            map.transform.position = new Vector3(map.transform.position.x, map.transform.position.y * 1 / scaleFactor, map.transform.position.z);

            //Reset player now that the reference has been scaled with the map
            player.transform.position = playerReference.transform.position + Vector3.up * playerHeight;
        }

        //Wait until the buildings are fully loaded and in place, so after the first update
        if (!buildingLoader.firstUpdate && !rasterGenerated && raster)
        {
            rasterGenerated = true;
            //Construct a one meter grid over the whole map
            int iterations = 0;
            for (int i = -rasterRadius; i <= rasterRadius; i = i + gridSize)
            {
                for(int j = -rasterRadius; j <= rasterRadius; j = j + gridSize)
                {
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.position = listener.transform.position + new Vector3(i, 0, j);
                    sphere.GetComponent<Collider>().enabled = false;
                    
                    RaycastHit hit;
                    if (Physics.Raycast(sphere.transform.position, Vector3.down, out hit, maximumRaycastDistance, collisionMask))
                    {
                        sphere.transform.position = hit.point + (Vector3.up * rasterHeightRender); //Render height
                        GameObject sphereListener = new GameObject();
                        sphereListener.transform.position = hit.point + (Vector3.up * rasterHeight); //Computational height
                        sphere.transform.localScale = rasterPointScale;
                        sphere.transform.parent = rasterContainer.transform;
                        sphere.GetComponent<MeshRenderer>().material = rasterContainer.GetComponent<MeshRenderer>().material;
                        sphere.GetComponent<MeshRenderer>().receiveShadows = false;
                        sphere.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        MaterialPropertyBlock _propBlock = new MaterialPropertyBlock();
                        sphere.GetComponent<MeshRenderer>().GetPropertyBlock(_propBlock);
                        _propBlock.SetColor("_Color", calculateNoiseLevelColor(sphereListener));
                        Destroy(sphereListener);
                        sphere.GetComponent<MeshRenderer>().SetPropertyBlock(_propBlock);
                    }
                    else
                    {
                        Destroy(sphere);
                    }
                    iterations++;
                }
            }
            StaticBatchingUtility.Combine(rasterContainer);
        }

        //Check whether listener moved in real scale, if yes, update sound and lines
        if(playerReference.transform.position != player.transform.position + Vector3.down * playerHeight && !downscaled)
        {
            updateListener();
            playerReference.transform.position = player.transform.position + Vector3.down * playerHeight;
            lineDrawer.Erase();
        } 

        //Iterate through the current tree if its supposed to be drawn and there are no lines
        if (showTree && lineDrawer.lines.Count == 0 && currTrees.Count > 0)
        {
            foreach (NoiseSource tree in currTrees)
            {
                tree.node.Traverse(drawNoise);
            }
            
        }
        //Retain scale, because lineRenderers try to reset themselves
        if (downscaled)
        {
            foreach (Transform line in lineContainer.transform)
            {
                line.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            }
        } else
        {
            foreach (Transform line in lineContainer.transform)
            {
                line.localScale = new Vector3(1, 1, 1);
            }
        }

    }
    
    Color calculateNoiseLevelColor(GameObject dataPoint)
    {
        List<float> pathSoundLevels = new List<float>();
        foreach (Transform child in transform)
        {
            noiseParameters vehicleParams = child.GetComponent<noiseParameters>();
            if (vehicleParams == null) { return Color.white; }; //Skip this object if parameters are missing
            NoiseSource noise = new NoiseSource();
            noise.debug = false;
            noise.origin = child.position;
            noise.controller = this;
            noise.node = new TreeNode<NoiseSource>(noise);
            noise.fireAt(dataPoint, maximumDistance);
            if (reflections) { noise.reflectToward(dataPoint, maximumDistance); }
            NMBP2008 noiseModel = new NMBP2008();
            List<ImageSource> images = noiseModel.calculateNoise(this, noise, vehicleParams);
            foreach(ImageSource img in images)
            {
                pathSoundLevels.Add(aWeighting(img));
            }
        }
        if (pathSoundLevels.Count == 0)
        {
            return Color.white;
        }
        //Long term sound level
        float finalSoundLevel = 0;
        foreach(float dezibel in pathSoundLevels)
        {
            finalSoundLevel = finalSoundLevel + Mathf.Pow(10.0f, (dezibel / 10));
        }
        finalSoundLevel = 10.0f * Mathf.Log10(finalSoundLevel);

        //Map value to something between 0 and 1, then map to color
        //Same mapping as in NoisePlayer.cs
        //float value = Mathf.Pow(10f, finalSoundLevel / 20f) * 0.00002f / 2f;
        //float minHue = 60f / 360; //corresponds to green
        //float maxHue = 1f / 360; //corresponds to red
        //float hue = value * maxHue + (1 - value) * minHue;
        //return Color.HSVToRGB(hue, 1, 0.7f);
        //Debug.Log("Soundlevel: " + finalSoundLevel + " Value: " + value + " Hue: " + hue);

        //Map to different danger levels
        if(finalSoundLevel < 65)
        {
            return Color.green;
        } else if (finalSoundLevel < 80)
        {
            return Color.yellow;
        } else
        {
            return Color.red;
        }
    }

    public float aWeighting(ImageSource image)
    {
        float summation = 0;
        foreach (KeyValuePair<int, float> entry in image.soundLevel)
        {
            //Sum up to 4 kHz according to CNOSSOS-EU, A-weighting correction from IEC 61672-1
            if (entry.Key == 63)
            {
                summation += Mathf.Pow(10, (entry.Value - 26.2f) / 10);
            }
            else if ((entry.Key == 125))
            {
                summation += Mathf.Pow(10, (entry.Value - 16.1f) / 10);
            }
            else if ((entry.Key == 250))
            {
                summation += Mathf.Pow(10, (entry.Value - 8.6f) / 10);
            }
            else if ((entry.Key == 500))
            {
                summation += Mathf.Pow(10, (entry.Value - 3.2f) / 10);
            }
            else if ((entry.Key == 1000))
            {
                summation += Mathf.Pow(10, (entry.Value) / 10);
            }
            else if ((entry.Key == 2000))
            {
                summation += Mathf.Pow(10, (entry.Value + 1.2f) / 10);
            }
            else if ((entry.Key == 4000))
            {
                summation += Mathf.Pow(10, (entry.Value + 1.0f) / 10);
            }
        }

        return 10 * Mathf.Log10(summation);
    }

    //Called on teleport, so that the noise levels for the listener can be recalculated
    void updateListener()
    {
        currTrees.Clear();
        foreach (Transform child in transform)
        {
            noiseParameters vehicleParams = child.GetComponent<noiseParameters>();
            if (vehicleParams == null) { continue; }; //Skip this object if parameters are missing
            NoiseSource noise = new NoiseSource();
            noise.debug = debugMode;
            noise.origin = child.position;
            noise.controller = this;
            noise.node = new TreeNode<NoiseSource>(noise);
            noise.fireAt(listener, maximumDistance);
            if (reflections) { noise.reflectToward(listener, maximumDistance); }
            //Transform tree into audio
            generateAudio(child.gameObject, noise, vehicleParams);
        }
    }

    private void generateAudio(GameObject noiseObject, NoiseSource noiseTree, noiseParameters vehicleParams)
    {
        //Save tree
        currTrees.Add(noiseTree);

        //Convert tree of noise propagation paths into list of image sources for audio playback
        NMBP2008 noiseModel = new NMBP2008();
        List<ImageSource> audioSources = noiseModel.calculateNoise(this, noiseTree, vehicleParams);

        //Check if sound is already running
        NoisePlayer nP = noiseObject.GetComponent<NoisePlayer>();
        if(nP == null)
        {
            nP = noiseObject.AddComponent<NoisePlayer>();
        }
        nP.play(audioSources, audioSourceObject);
    }

    //Iterates through the tree of noise paths, and at every point draws debug lines to all children
    private void drawNoise(NoiseSource src)
    {
        foreach (TreeNode<NoiseSource> child in src.node.Children)
        {
            Vector3 start = src.origin;
            Vector3 end = child.Value.origin;
            float width = lineWidth;
            
            Color lineColor = Color.cyan;
            lineColor.a = 0.3f;

            //If world is miniaturized, change width and color so that they fit but are still visible.
            if (downscaled)
            {
                width = width * scaleFactor * 2;
                lineColor.a = 0.6f;
            }


            //Diffraction
            if (child.Value.type == NoiseSource.HitType.DIFFRACTION_V 
                || child.Value.type == NoiseSource.HitType.DIFFRACTION_H)
            {
                //lineDrawer.DrawLineInGameView(start, end, width, Color.blue, lineContainer);
                lineDrawer.DrawLineInGameView(start, end, width, lineColor, lineContainer);
            }
            //Reflection
            if (child.Value.type == NoiseSource.HitType.REFLECTION)
            {
                //lineDrawer.DrawLineInGameView(start, end, width, Color.yellow, lineContainer);
                lineDrawer.DrawLineInGameView(start, end, width, lineColor, lineContainer);
            }
            //Final hit
            if (child.Value.type == NoiseSource.HitType.DIRECT)
            {
                //lineDrawer.DrawLineInGameView(start, end + Vector3.down, width, Color.green, lineContainer);
                lineDrawer.DrawLineInGameView(start, end + Vector3.down, width, lineColor, lineContainer);
            }
        }
    }
}
