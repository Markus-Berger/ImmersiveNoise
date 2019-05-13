# ImmersiveNoise

Code repository for [Combining VR visualization and sonification for immersive exploration of urban noise standards](https://www.mdpi.com/2414-4088/3/2/34).

Please read the paper for detailed explanation about the technology and its intended applications. Below you will find instructions on how to run the code on your own system. Most of the code is intended to run with the Unity engine, and will not work outside a Unity environment.

_This is research code. It is not ready or intended for production environments._

## Usage
### Dependencies

In order to run the preprocessing, you need the c2x.jar from [this repository](https://github.com/900k/CityGML2X/tree/master/CityGML2X_CLI).

For Unity, you need to install the Mapbox plugin and get a developer key.

For GearVR and Oculus Go support, include the [Oculus integration for Unity](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) and the [ArkTeleporter](https://developer.oculus.com/blog/teleport-curves-with-the-gear-vr-controller/) script

### Preprocessing

First, all the CityGML-Buildings need to be split into individual .xml files. For this, run `BuildingParser.py`. 

Example command:

```
py BuildingsParser.py -i InputCityGML -o outputFolder -p +init=EPSG:3857
```

Then, run cg2unity.bat in the output folder as so:

```
cg2unity --input outputFolder --lod LOD2
```

This will create one .dae file (COLLADA 3D model) for each CityGML building .xml. Load both .xml and .dae files into your Unity resource folder.

### Unity

#### Buildings and Terrain
Include all the Unity scripts in your Unity project. Make sure the CityGML building .dae files are in the resources folder. Attach the `AbstractMap.cs` script from the Mapbox extension to an empty game object. Configure your map. Add a kinematic rigidbody to this object too.

Then, place the `LoadBuildings.cs` script in the scene. Reference the AbstractMap object in it. The LoadBuildings script loads the buildings from the resources folder. If you check skirts, then the floor geometry will be stretched to the terrain. If you activate firstUpdate, then the map will be raised or lowered so that the buildings properly sit on the terrain (in case there are inaccuracies in your CityGML, the Mapbox terrain or in the conversion process). Test if map and buildings are placed correctly.

#### VR support

No custom scripts were used to support VR. To get to the same state as the paper, you need to configure the Oculus Integration and the ArkTeleporter yourself. In the end, you should be able to teleport on the terrain. Teleporting onto buildings is not always guaranteed to work, because Unities collision meshes don't always work well with input generated from CityGML.

#### Noise Modeling

Once movement and resource loading work, you can add the noise modeling component. Add the `NoiseController.cs` to an empty gameObject that you place at the world zero. Add another empty gameObject that you call `RasterContainer`. It will contain the raster of observer points. 

Create a prefab with the clip you want to have played by your noise sources, set its rolloff curve to logarithmic, at a max distance of 800, and activate spatialization. Reference that prefab in the NoiseController under "Audio Source Object". Reference the Raster Container under "Raster Container". Set the "Listener" variable to your oculus tracking space. Set "Building Loader" to the object that has `LoadBuildings.cs` attached. Set the "Map" variable to your AbstractMap object. Set your OVRCameraRig as the "Player". Configure the other variables. (More detailed explanations of this last step will come with the code refactor. In the meantime, the presets should work. If there are any issues, please feel free to ask.)

In order to add noise sources to the scene, add child game objects to the noise controller and position them where you want them. Attach the `NoiseParameter.cs` script to every noise source. It tells the noise modeling what kind of vehicle the source represents. At the moment, only the values for light cars are included.


## Citation
If you want to use this project or reference it in a scientific publication, please use the following reference:


>Berger, M.; Bill, R. Combining VR Visualization and Sonification for Immersive Exploration of Urban Noise Standards. _Multimodal Technologies Interact._  **2019**, _3_, 34.


## TODO

- More detailed documentation
- Code refactoring & comments

