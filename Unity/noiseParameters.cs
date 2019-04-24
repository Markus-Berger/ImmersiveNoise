using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Source: http://publications.jrc.ec.europa.eu/repository/bitstream/JRC72550/cnossos-eu%20jrc%20reference%20report_final_on%20line%20version_10%20august%202012.pdf

public class noiseParameters : MonoBehaviour {

    //Vehicle categories
    public enum VehicleType { LIGHT, MEDIUM, HEAVY, TWOWHEELER }; //Light is everything below 3,5t

    //Standard sound emission per octave band for different vehicle categories for 70km/h
    private struct coefficients
    {
        public float AR; //A rolling noise
        public float BR; //B rolling noise
        public float AP; //A propulsion noise
        public float BP; //B propulsion noise
        public float a; // Spectral correction at reference speed
        public float b; // Speed effect for actual speed
    };

    private Dictionary<int, coefficients> octaveBandsLIGHT = new Dictionary<int, coefficients>();
    private Dictionary<int, coefficients> octaveBandsMEDIUM = new Dictionary<int, coefficients>();
    private Dictionary<int, coefficients> octaveBandsHEAVY = new Dictionary<int, coefficients>();
    private Dictionary<int, coefficients> octaveBandsTWOWHEELER = new Dictionary<int, coefficients>();

    //Values only for this vehicle
    public int vehiclesPerHour = 100;
    public float segmentLength = 1; //Road segment length in meter
    public float soundPower = 20; //Sound power per meter segment
    public float averageSpeed = 50; //Average speed in km/h
    public VehicleType category = VehicleType.LIGHT;

	// Initialize dictionary values
	void Start () {
        octaveBandsLIGHT.Add(63, new coefficients {AR = 79.9f, BR = 30.0f, AP = 94.5f, BP = -1.3f, a = 0.0f, b = 0.0f});
        octaveBandsLIGHT.Add(125, new coefficients { AR = 85.7f, BR = 41.5f, AP = 89.2f, BP = 7.2f, a = 0.0f, b = 0.0f});
        octaveBandsLIGHT.Add(250, new coefficients { AR = 84.5f, BR = 38.9f, AP = 88.0f, BP = 7.7f, a = 0.0f, b = 0.0f});
        octaveBandsLIGHT.Add(500, new coefficients { AR = 90.2f, BR = 25.7f, AP = 85.9f, BP = 8.0f, a = 2.6f, b = -3.1f});
        octaveBandsLIGHT.Add(1000, new coefficients { AR = 97.3f, BR = 32.5f, AP = 84.2f, BP = 8.0f, a = 2.9f, b = -6.4f});
        octaveBandsLIGHT.Add(2000, new coefficients { AR = 93.9f, BR = 37.2f, AP = 86.9f, BP = 8.0f, a = 1.5f, b = -14.0f});
        octaveBandsLIGHT.Add(4000, new coefficients { AR = 84.1f, BR = 39.0f, AP = 83.3f, BP = 8.0f, a = 2.3f, b = -22.4f});
        octaveBandsLIGHT.Add(8000, new coefficients { AR = 74.3f, BR = 40.0f, AP = 76.1f, BP = 8.0f, a = 9.2f, b = -11.4f});
        //TODO: Add other categories
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    private float rollingNoise(coefficients input, float temperature)
    {
        //No studded tires, standard road conditions, see cnossos-eu p.34
        float temperatureCoefficient;

        //Simplified temperature correction:
        if (category == VehicleType.LIGHT)
        {
            temperatureCoefficient = 0.08f * (20.0f - temperature);
        }
        else if (category == VehicleType.MEDIUM || category == VehicleType.HEAVY)
        {
            temperatureCoefficient = 0.04f * (20.0f - temperature);
        }
        else
        {
            temperatureCoefficient = 0;
        }

        return input.AR + input.BR * Mathf.Log10(averageSpeed/70) + temperatureCoefficient;
    }

    private float propulsionNoise(coefficients input)
    {
        //TODO: Propulsion noise coefficients for road gradient and acceleration etc.

        //Road surface correction: (Standard values)
        float surfaceCoefficient = input.a + input.b * Mathf.Log10(averageSpeed / 70);

        return input.AP + input.BP * (averageSpeed - 70)/(70) + surfaceCoefficient;
    }

    //Calculates sound emission over all octave bands
    public Dictionary<int, float> soundEmission(float temperature)
    {
        Dictionary<int, float> emissionOutput = new Dictionary<int, float>(); ;

        Dictionary<int, coefficients> standardValues;
        if (category == VehicleType.LIGHT)
        {
            standardValues = octaveBandsLIGHT;
        }
        else if (category == VehicleType.MEDIUM)
        {
            standardValues = octaveBandsMEDIUM;
        }
        else if (category == VehicleType.HEAVY)
        {
            standardValues = octaveBandsHEAVY;
        }
        else
        {
            standardValues = octaveBandsTWOWHEELER;
        }
        //Iterate over all octave bands
        foreach(KeyValuePair<int, coefficients> entry in standardValues)
        {
            float soundEmission;
            if (category == VehicleType.TWOWHEELER)
            {
                //Motorcycles only have propulsion noise
                soundEmission = propulsionNoise(entry.Value);
            }
            else
            {
                //Rolling and propulsion noise
                soundEmission = 10 * Mathf.Log10(Mathf.Pow(10.0f, (rollingNoise(entry.Value, temperature) / 10.0f)) + Mathf.Pow(10.0f, (propulsionNoise(entry.Value) / 10.0f)));
            }
            emissionOutput.Add(entry.Key, soundEmission);
        }

        return emissionOutput;
    }
}
