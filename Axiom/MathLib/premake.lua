package.name = "MathLib"
package.language = "c#"
package.kind = "dll"
package.buildflags = { "unsafe" }
package.defines = { "TRACE" } 
package.config["Debug"].defines = { "DEBUG" }
package.target = "Axiom.MathLib"
package.links = { "System" }
package.files = { matchfiles("*.cs"), matchfiles("Collections/*.cs") }