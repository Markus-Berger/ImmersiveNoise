using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

//Stores all the information calculated by a noise propagation model
//Is turned into a unity audio source afterwards
public class ImageSource {
    public Vector3 source;
    public Dictionary<int, float> geometricDivergence; //Saved so it can be removed for audio playback, as there distance is handled by the falloff curve
    public Dictionary<int, float> soundLevel;

    //Remove the distances, so they can be handled by the 3D audio spatialization of Unity
    public void removeDistances()
    {
        foreach (int key in soundLevel.Keys.ToList())
        {
            soundLevel[key] = soundLevel[key] + geometricDivergence[key];
        }
        return;
    }
}
