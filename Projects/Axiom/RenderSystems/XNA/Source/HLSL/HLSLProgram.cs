#region LGPL License
/*
Axiom Graphics Engine Library
Copyright � 2003-2011 Axiom Project Team

The overall design, and a majority of the core engine and rendering code 
contained within this library is a derivative of the open source Object Oriented 
Graphics Engine OGRE, which can be found at http://ogre.sourceforge.net.  
Many thanks to the OGRE team for maintaining such a high quality project.

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*/
#endregion

#region SVN Version Information
// <file>
//     <license see="http://axiom3d.net/wiki/index.php/license.txt"/>
//     <id value="$Id$"/>
// </file>
#endregion SVN Version Information

#region Namespace Declarations

using System;
using System.Diagnostics;

using Axiom.Core;
using Axiom.Graphics;
using Axiom.RenderSystems.Xna.Content;

using ResourceHandle = System.UInt64;

using XNA = Microsoft.Xna.Framework;
using XFG = Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

#endregion Namespace Declarations

namespace Axiom.RenderSystems.Xna.HLSL
{
	/// <summary>
	/// Summary description for HLSLProgram.
	/// </summary>
	public class HLSLProgram : HighLevelGpuProgram
	{
		/// <summary>
		///     Shader profile to target for the compile (i.e. vs1.1, etc).
		/// </summary>
		protected string target;
		/// <summary>
		///     Entry point to compile from the program.
		/// </summary>
		protected string entry;
		/// <summary>
		/// preprocessor defines used to compile the program.
		/// </summary>
		protected string preprocessorDefines;

		public HLSLProgram( ResourceManager parent, string name, ResourceHandle handle, string group, bool isManual, IManualResourceLoader loader )
			: base( parent, name, handle, group, isManual, loader )
		{
			preprocessorDefines = string.Empty;
		}

		/// <summary>
		///     Creates a low level implementation based on the results of the
		///     high level shader compilation.
		/// </summary>
		protected override void CreateLowLevelImpl()
		{
            assemblerProgram = GpuProgramManager.Instance.CreateProgramFromString(Name, Group, "", type, target);
            assemblerProgram.IsSkeletalAnimationIncluded = this.IsSkeletalAnimationIncluded;
        }

		public override GpuProgramParameters CreateParameters()
		{
			GpuProgramParameters parms = base.CreateParameters();

			return parms;
		}

		/// <summary>
		///     Compiles the high level shader source to low level microcode.
		/// </summary>
		protected override void LoadFromSource()
		{
		}

		protected override void LoadHighLevelImpl()
		{
			isHighLevelLoaded = true;
		}

		/// <summary>
		///     Derives parameter names from the constant table.
		/// </summary>
		/// <param name="parms"></param>
		protected override void PopulateParameterNames( GpuProgramParameters parms )
		{
		}

		/// <summary>
		///     Unloads data that is no longer needed.
		/// </summary>
		protected override void UnloadImpl()
		{
		}

		public override bool IsSupported
		{
            get
            {
                return false;
            }
		}

		public override int SamplerCount
		{
			get
			{
				switch ( target )
				{
					case "ps_1_1":
					case "ps_1_2":
					case "ps_1_3":
						return 4;
					case "ps_1_4":
						return 6;
					case "ps_2_0":
					case "ps_2_x":
					case "ps_3_0":
					case "ps_3_x":
						return 16;
					default:
						throw new AxiomException( "Attempted to query sample count for unknown shader profile({0}).", target );
				}

				// return 0;
			}
		}

		#region IConfigurable Members

		/// <summary>
		///     Sets a param for this HLSL program.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="val"></param>
		/// <returns></returns>
		public override bool SetParam( string name, string val )
		{
			bool handled = true;

			switch ( name )
			{
				case "entry_point":
					entry = val;
					break;

				case "target":
					target = val.Split( ' ' )[ 0 ];
					break;

				case "preprocessor_defines":
					preprocessorDefines = val;
					break;

                case "includes_skeletal_animation":
                    this.IsSkeletalAnimationIncluded = true;
                    break;

				default:
					LogManager.Instance.Write( "HLSLProgram: Unrecognized parameter '{0}'", name );
					handled = false;
					break;
			}

			return handled;
		}

		#endregion IConfigurable Members
	}
}
