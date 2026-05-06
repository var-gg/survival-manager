●First,
Immediately after installing this asset, 
the character will not be displayed because there is no shader.
Install "jp.lilxyzw.liltoon-1.x.x-installer" in the "Shader_installer" folder.
“lilToon_Shader” will be added to the Package.

The physics simulation "MagicaCloth 2" has been set up.
It will work if you install a separate asset.

If you are using "MgicaCloth2",
unzip the "Setup_MagicaCloth2" package data.
The settings will be applied to the model data.

https://assetstore.unity.com/packages/tools/physics/magica-cloth-2-242307

The standard preparation for using this asset is as follows:
1. Run "Shader_installer > jp.lilxyzw.liltoon-1.x.x-installer" in this asset.
2. Install the separate asset "MagicaCloth2".
3. Extract the "Setup_MagicaCloth2" package in this asset.


=== [Notes] ====

● If the lilToon shader installer does not work properly,
download the latest shaders from Github and place the entire folder
in the "Packages" folder of your project.

https://github.com/lilxyzw/lilToon/releases

● If the bone constraints for hair become unstable during playback, 
store the entire hair group as a child of the skeleton's head bone, 
and delete the constraint components set to the root of each hair bone.

● The MagicaCloth settings use the Pre-Build function so that 
they function correctly even when the character starts playing in the middle of the animation. 
If you want to change the settings, turn off the "Pre-Build" function, 
make the changes, save the Pre-Build data again, and set it again.

●If you think you have enough capacity for the processing load of Physics by MagicaCloth, 
you can make simple setting changes such as making the reduction more fine-grained or 
turning on self-collision to achieve higher quality behavior with fewer errors.

● The texture resolution of each piece of armor is set to 2048, 
but the data is created at 4096, so the resolution can be increased to that value.

● The material shader uses "lilToon", but roughness maps and MaskMaps for HDRP are available 
in "Extra Texture" to support Standard materials.

●You can create facial expressions on a character's face using blend shapes,
but when applying a beard to a male face and creating facial expressions,
you will also need to manipulate the beard's blend shapes at the same time.

●The character's texture has been created with the assumption that 
it will be used in a realistic scene environment,
but if you are using a Stylized (Toon) scene environment,
we recommend lowering the metalness and matte cap strength in the material settings.

●You can change some of the colors of each outfit.
In the material inspector,
adjust the parameters in "Color" > "Main Color" > "Color Adjust".
Please note that this is a simple function and is not perfect.

