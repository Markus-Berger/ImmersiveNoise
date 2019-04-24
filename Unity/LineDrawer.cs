using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct LineDrawer
{
    public List<LineRenderer> lines;

    //Draws lines through the provided vertices
    public void DrawLineInGameView(Vector3 start, Vector3 end, float lineSize, Color color, GameObject container)
    {
        GameObject lineObj = new GameObject("LineObj");
        lineObj.transform.parent = container.transform;
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        lines.Add(line);
        line.material = new Material(Shader.Find("Hidden/Internal-Colored"));
        
        //Set color
        line.startColor = color;
        line.endColor = color;

        //Set width
        line.startWidth = lineSize;
        line.endWidth = lineSize;

        //Set line count which is 2
        line.positionCount = 2;

        //Set the postion of both two lines
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    public void Erase()
    {
        foreach(LineRenderer line in lines)
        {
            Object.Destroy(line.gameObject);
        }
        lines.Clear();
    }
}