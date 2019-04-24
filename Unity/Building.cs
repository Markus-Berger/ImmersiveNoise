using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour {

    private List<GameObject> buildingParts;
    public Vector3 roofReference;
    public Vector3 floorReference;


    //Rotates the object, and returns the mesh reference
    public Vector3 initialize(Vector3 rotation)
    {
        // Initialize components
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        //Initialize variables
        buildingParts = new List<GameObject>();

        //Find mesh reference
        //Check if its only a parent, or if there are children
        if (transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                addPart(child.gameObject, rotation);
            }
        }
        else
        {
            addPart(gameObject, rotation);
        }

        //Iterate through all meshes
        Vector3 referencePoint = new Vector3(0, float.MinValue, 0);
        Vector3 floorReference = new Vector3(0, float.MaxValue, 0);
        int posNr = 0;
        foreach (GameObject building in buildingParts)
        {
            building.layer = LayerMask.NameToLayer("Buildings");
            //List of all mesh filters of the game object
            MeshFilter mf = building.GetComponent<MeshFilter>();
            List<Vector3> vertices = new List<Vector3>(mf.mesh.vertices);
            foreach (Vector3 vert in vertices)
            {
                Vector3 pt = building.transform.TransformPoint(vert);
                //Store highest point
                if (pt.y > referencePoint.y)
                {
                    referencePoint.y = pt.y;
                }

                //Accumulate north and east values
                referencePoint.x += pt.x;
                referencePoint.z += pt.z;
                posNr++;

                //Save low point
                if (pt.y < floorReference.y)
                {
                    floorReference = pt;
                }
            }
        }
        //Find reference point
        referencePoint.x = referencePoint.x / posNr;
        referencePoint.z = referencePoint.z / posNr;
        roofReference = referencePoint;

        return referencePoint;
    }

    //Moves building and references to correct world position
    public void place(Vector3 position)
    {
        transform.Translate(position, Space.World);
        roofReference = position + roofReference;
        floorReference = position + floorReference;
    }
         
    //Adds building (parts) that possess a mesh to the list, and rotates them correctly
    public void addPart(GameObject part, Vector3 rotation)
    {
        part.transform.eulerAngles = rotation;
        buildingParts.Add(part);
    }

    public List<GameObject> getParts()
    {
        return buildingParts;
    }

    //Finds distance from floor to ground
    public float getGround()
    {
        int terrain_mask = LayerMask.GetMask("Terrain");
        RaycastHit terrainHit;
        if (Physics.Raycast(floorReference, Vector3.down, out terrainHit, 100.0f, terrain_mask))
        {
            return terrainHit.distance;
        }
        else
        {
            return -1.0f;
        }
    }

    //Creates skirts to the ground, then creates a collider
    public void setGround()
    {
        foreach(GameObject building in buildingParts)
        {
            MeshFilter mf = building.GetComponent<MeshFilter>();
            Vector3[] verts = mf.mesh.vertices;

            //Go through floor-level vertices
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 pt = building.transform.TransformPoint(verts[i]);
                if (Mathf.Abs(pt.y - floorReference.y) < 0.5f)
                {
                    int terrain_mask = LayerMask.GetMask("Terrain");
                    RaycastHit terrainHit;
                    if (Physics.Raycast(pt, Vector3.down, out terrainHit, 100.0f, terrain_mask))
                    {
                        verts[i] = verts[i] + (new Vector3(0, 0, -1) * terrainHit.distance);
                    }
                }
            }

            //Apply new mesh
            mf.mesh.vertices = verts;
            mf.mesh.RecalculateBounds();
            mf.mesh.RecalculateNormals();

            //Add collider
            building.AddComponent<MeshCollider>();
        }
    }

    // Update is called once per frame
    void Update () {
		
	}
}
