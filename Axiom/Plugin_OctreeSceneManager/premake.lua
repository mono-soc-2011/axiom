package = newpackage()
package.name = "Plugin_OctreeSceneManager"
package.language = "c#"
package.kind = "dll"
package.target = "Axiom.SceneManagers.Octree"
package.buildflags = { "unsafe" }
package.defines = { "TRACE" } 
package.config["Debug"].defines = { "DEBUG" }
package.libpaths = { "../Solution Items" }
package.links = { "System", "System.Data", "System.Xml", "Engine", "MathLib" }
package.files = { matchfiles("*.cs") }