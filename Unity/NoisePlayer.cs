using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoisePlayer : MonoBehaviour {

    List<AudioSource> audioPlayers;
    bool hasInitialized = false;
    private NoiseController currController;

    // Use this for initialization
    void Start () {
        audioPlayers = new List<AudioSource>();
        hasInitialized = true;
        currController = transform.parent.GetComponent<NoiseController>();
    }
	
	// Update is called once per frame
	void Update () {
        //OVRInput.Update();
        foreach (AudioSource audio in audioPlayers) {
            //Start sound when the upper part of the touchpad is pressed
            if (OVRInput.Get(OVRInput.Button.PrimaryTouchpad) && OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad).y > 0.5f && !audio.isPlaying && !currController.downscaled)
            {
                //Delay by speed of sound
                audio.PlayDelayed(speedOfSound(audio.transform.position, currController.listener.transform.position));
            }
        }
    }

    //Updates the acoustics and starts playing them
    public void play(List<ImageSource> images, GameObject audioSourceObject)
    {
        if (!hasInitialized) { return; }
        //Iterate through copy of schedule to remove everything from schedule
        foreach (AudioSource obj in audioPlayers)
        {
            obj.Stop();
            Destroy(obj.gameObject);
        }
        audioPlayers.Clear();
        foreach (ImageSource image in images)
        {
            GameObject source = (GameObject) Instantiate(audioSourceObject);
            source.transform.parent = transform; // Make it a child of the noise source
            source.transform.position = image.source;
            AudioSource audio = source.GetComponent<AudioSource>();
            image.removeDistances();
            audio.volume = calibration(currController.aWeighting(image));
            audioPlayers.Add(audio);
        }
    }

    float speedOfSound(Vector3 source, Vector3 receiver)
    {
        float speed = 331.4f + 0.6f * currController.temperature;
        return Vector3.Distance(source, receiver) / speed;
    }

    //Turning the calculated dB value into a normalized unity volume level
    //Here we just turn the dezibel into a linear value as used by unity
    public float calibration(float dezibel)
    {
        //Conversion to pascal, divided by 2
        //Maps everything from 0 to a 100dB (extreme case for a road) to a range between 0 and 1
        return Mathf.Pow(10f, dezibel/20f) * 0.00002f / 2f;
    }

}
