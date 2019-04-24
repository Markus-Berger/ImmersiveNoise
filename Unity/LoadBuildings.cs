using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using Mapbox.Unity.Utilities;
using Mapbox.Unity.Map;
using Mapbox.Utils;

public class LoadBuildings : MonoBehaviour {
    
    public AbstractMap map;
    public Vector3 modelRotation;
    public bool skirts = true;
    public bool firstUpdate = true;
    private List<Vector3> geoReferences = new List<Vector3>();
    private List<Vector3> meshReferences = new List<Vector3>();

	// Use this for initialization
	void Start () {
        Object[] buildings;
        buildings = Resources.LoadAll("Buildings", typeof(GameObject));
        
        foreach (Object building in buildings) {
            //Load metadata from XML
            string bldgNr = building.name.Replace("(clone)", "");
            TextAsset textAsset = (TextAsset)Resources.Load("Buildings/" + bldgNr, typeof(TextAsset));
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(textAsset.text);
            XmlNode root = xmldoc.DocumentElement;

            //Should probably be done with XPath
            XmlNode xmlBuilding = xmldoc.GetElementsByTagName("bldg:Building")[0];
            XmlNode buildingReference = xmldoc.GetElementsByTagName("gml:AbstractFeature")[0];
            XmlNode measuredHeight = xmldoc.GetElementsByTagName("bldg:measuredHeight")[0];
            string coordString = buildingReference.FirstChild.FirstChild.InnerText;

            //Get geographic coordinates, double precision
            string[] referenceCoords = coordString.Split(' ');
            Vector2d referenceD = new Vector2d(0, 0);
            double referenceHeight = 0;
            referenceD.x = double.Parse(referenceCoords[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            referenceD.y = double.Parse(referenceCoords[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            referenceHeight = double.Parse(referenceCoords[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
           
            /*if(bldgNr == "Building3486#1" || bldgNr == "Building3486#2")
            {
                Debug.Log("Double: " + referenceD.x.ToString("F12") + " " + referenceD.y.ToString("F12") + " " + referenceHeight.ToString("F12") + " ; String: " + referenceCoords[1] + " " + referenceCoords[0] + " " + referenceCoords[2]);
            }*/

            //From geocoordinates into unity coordinates
            Vector2d worldPos = Conversions.GeoToWorldPosition(referenceD, map.CenterMercator , map.WorldRelativeScale);
            Vector3 referencePoint = new Vector3((float)worldPos.x, (float)referenceHeight, (float)worldPos.y);

            /*if (bldgNr == "Building3486#1" || bldgNr == "Building3486#2")
            {
                Debug.Log("Geopos: " + referenceD.x.ToString("F12") + " " + referenceD.y.ToString("F12")+ "; Mapbox: " + worldPos.x.ToString("F12") + " " + worldPos.y.ToString("F12"));
            }*/

            /*
            if (bldgNr == "Building3486" || bldgNr == "Building3486#3")
            {
                Debug.Log("Double: " + worldPos.x.ToString("F12") + " " + worldPos.y.ToString("F12")+ "; Float: " + referencePoint.x.ToString("F12") + " " + referencePoint.z.ToString("F12"));
            }
            */

            //Instantiate game object at zero
            GameObject bldg = (GameObject) Instantiate(building, gameObject.transform);
            Building bldgScript = bldg.AddComponent<Building>();

            //Find reference point on mesh, then move the points together
            Vector3 meshReference = bldgScript.initialize(modelRotation);
            Vector3 offset = referencePoint - meshReference;
            meshReferences.Add(meshReference);
            geoReferences.Add(referencePoint);
            bldgScript.place(offset);
        }
    }

    void Update()
    {
        if (firstUpdate)
        {
            //Once all buildings are set, raise up the terrain towards them
            //Raycast downwards from each building, then take the mean distance
            float meanDistance = 0.0f;
            int buildings = 0;
            foreach (Transform child in transform)
            {
                //Add every valid distance
                float dist = child.GetComponent<Building>().getGround();
                if (dist >= 0.0f)
                {
                    meanDistance += dist;
                    buildings++;
                }
            }
            meanDistance /= buildings; 
            map.transform.Translate(new Vector3(0, meanDistance, 0));

            //Now, stretch the individual building meshes down onto the terrain
            foreach (Transform child in transform)
            {
                Building bldg = child.GetComponent<Building>();
                if (skirts)
                {
                    bldg.setGround();
                }
            }

            //Only do all this once
            firstUpdate = false;
        }
        
    }


    void OnDrawGizmos()
    {
        /*
        foreach (Vector3 point in geoReferences)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point, 1);
        }
        
        foreach (Vector3 point in meshReferences)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(point, 1);
        }*/
    }
}
