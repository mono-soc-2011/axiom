project.name = "Axiom"

dopackage("MathLib")
dopackage("Engine")
dopackage("ParticleFX")
dopackage("DynamicsSystem_ODE")
dopackage("Plugin_CgProgramManager")
dopackage("Plugin_GuiElements")
dopackage("RenderSystem_OpenGL")
dopackage("Plugin_OctreeSceneManager")

if(OS == "windows") then
	dopackage("RenderSystem_DirectX9")
	dopackage("PlatformManager_Win32")
end

if(OS == "linux") then
	dopackage("PlatformManager_Unix")
end