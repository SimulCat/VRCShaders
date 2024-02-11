# Shaders

This is a repository for sharing and testing wave and particle simulation shaders used across the science demo projects I have on VRChat;
It is a WIP so use at your own risk. 

I am only getting started in writing Shader code, please go easy and constructive feedback is welcome.

Some:
    Use cut-down shaders to create wave interference patterns in VRC Worlds that are aimed at being able to run on the quest.
Others involve: 
    Custom Render Textures not able to tbe used on Quest AFAIK but able to handle more complex patterns and higher resolution simulations on PC.


Using the Custom Render Texture Demo:
To use the code, you need latest VRChat SDK and UdonSharp, otherwise the shaders can be used by manually configuring properties.

Shaders are in Folder /CRT Shaders
Demo prefab and assets /CRT Demos

Some notes on the Custom Render Texture versions:
    A CRT to my mind is a cross between a regular shader and a compute shader.
    Essentially it is a compute shader where the texture output can be linked directly to the game engine 
    graphics as a render texture similar to a camera render texture.
    Like a compute shader, it can be run independently of the graphics frame cadence. You can decide what 
    to do on each pass, and even what zone of the texture needs updating.
    
    For wave simulations it has the essential feature of being double-buffered so the output of one cycle 
    is available as input for the next cycle.

    My current version is intended for dual use. 
        First, the output can be used directly with a standard shader it generates an image texture able 
        to be used with a standard fragment shader running on a material.
        
        This currently produces static output unless the CRT shader is run continuously.

        Second, it can be set to output the raw phase data so that custom shaders can display the output.

    To use:
        (1) Set up the render texture: 
        
            Create a custom render texture, "Create->Custom Render Texture".
            Make a material and set its shader to the Custom Render Texture shader.
            Add the material to the render texture shader.
            Configure the render texture properties on the material.

        (2) Set up a display panel (e.g. Quad) and assign it a new material.
            (a) If choosing a standard shader, go to the custom render texture material's properties and choose property "Generate Raw Output" to 0.
                The render texture will then output an image.
            (b) If choosing a CRT display shader, go to the custom render texture properties and choose property "Generate Raw Output" to 1.
                The render texture will then output phase and amplitude data to be processed by the display shader.





