﻿#region LGPL License
/*
Axiom Graphics Engine Library
Copyright © 2003-2011 Axiom Project Team

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
#endregion LGPL License

#region SVN Version Information
// <file>
//     <license see="http://axiom3d.net/wiki/index.php/license.txt"/>
//     <id value="$Id$"/>
// </file>
#endregion SVN Version Information

#region Namespace Declarations

using System;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Math;
using Axiom.Scripting.Compiler.AST;
using Axiom.Utilities;

#endregion Namespace Declarations

namespace Axiom.Scripting.Compiler
{
	/// <summary>
	/// Manages threaded compilation of scripts. This script loader forwards
	/// scripts compilations to a specific compiler instance.
	/// </summary>
	public partial class ScriptCompilerManager : Singleton<ScriptCompilerManager>, IScriptLoader
	{
		#region Fields and Properties

		private List<string> _scriptPatterns = new List<string>();

		private ScriptCompiler _compiler;

		private List<ScriptTranslatorManager> _translatorManagers = new List<ScriptTranslatorManager>();
		private ScriptTranslatorManager _builtinTranslatorManager;

		public IList<ScriptTranslatorManager> TranslatorManagers
		{
			get
			{
				return _translatorManagers;
			}
		}

		#endregion Fields and Properties

		#region Construction and Destruction

		public ScriptCompilerManager()
			: base()
		{
#if AXIOM_USENEWCOMPILERS
			this._scriptPatterns.Add( "*.program" );
			this._scriptPatterns.Add( "*.material" );
			this._scriptPatterns.Add( "*.particle" );
			this._scriptPatterns.Add( "*.compositor" );
#endif
			this._scriptPatterns.Add( "*.os" );

			ResourceGroupManager.Instance.RegisterScriptLoader( this );

			this._compiler = new ScriptCompiler();

			this._builtinTranslatorManager = new BuiltinScriptTranslatorManager();
			this._translatorManagers.Add( this._builtinTranslatorManager );
		}
		#endregion Construction and Destruction

		#region Methods

		/// <summary>
		/// Retrieves a ScriptTranslator from the supported managers
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public ScriptCompiler.Translator GetTranslator( AbstractNode node )
		{
			ScriptCompiler.Translator translator = null;

			// Start looking from the back
			if ( _translatorManagers.Count > 0 )
			{
				for ( int i = _translatorManagers.Count - 1; i >= 0; i-- )
				{
					translator = _translatorManagers[ i ].GetTranslator( node );
					if ( translator != null )
						break;
				}
			}

			return translator;
		}

		#endregion Methods

		#region IScriptLoader Implementation

		/// <summary>
		/// A list of patterns loaded by this compiler manager
		/// </summary>
		public List<string> ScriptPatterns
		{
			get
			{
				return _scriptPatterns;
			}
		}

		public void ParseScript( System.IO.Stream stream, string groupName, string fileName )
		{
			// Set the listener on the compiler before we continue
			_unsetCompilerEvents(); // Double tap
			_setCompilerEvents();

			System.IO.StreamReader rdr = new System.IO.StreamReader( stream );
			String script = rdr.ReadToEnd();
			_compiler.Compile( script, fileName, groupName );

			// Unset events in order to avoid that compiler's events will be called twice next time
			_unsetCompilerEvents();
		}

		/// <summary>
		/// Set events of this manager's compiler
		/// </summary>
		private void _setCompilerEvents()
		{
			Contract.RequiresNotNull( _compiler, "_compiler" );

			if ( this.OnImportFile != null )
				_compiler.OnImportFile += this.OnImportFile;

			if ( this.OnPreConversion != null )
				_compiler.OnPreConversion += this.OnPreConversion;

			if ( this.OnPostConversion != null )
				_compiler.OnPostConversion += this.OnPostConversion;

			if ( this.OnCompileError != null )
				_compiler.OnCompileError += this.OnCompileError;

			if ( this.OnCompilerEvent != null )
				_compiler.OnCompilerEvent += this.OnCompilerEvent;
		}

		/// <summary>
		/// Unset events of this manager's compiler
		/// </summary>
		private void _unsetCompilerEvents()
		{
			Contract.RequiresNotNull( _compiler, "_compiler" );

			if ( this.OnImportFile != null )
				_compiler.OnImportFile -= this.OnImportFile;

			if ( this.OnPreConversion != null )
				_compiler.OnPreConversion -= this.OnPreConversion;

			if ( this.OnPostConversion != null )
				_compiler.OnPostConversion -= this.OnPostConversion;

			if ( this.OnCompileError != null )
				_compiler.OnCompileError -= this.OnCompileError;

			if ( this.OnCompilerEvent != null )
				_compiler.OnCompilerEvent -= this.OnCompilerEvent;
		}

		public Real LoadingOrder
		{
			get
			{
				// Load relatively early, before most script loaders run
				return 90.0f;
			}
		}

		#endregion IScriptLoader Implementation

	}

	/// <summary>
	/// The ScriptTranslatorManager manages the lifetime and access to
	/// script translators. You register these managers with the
	/// ScriptCompilerManager tied to specific object types.
	/// Each manager may manage multiple types.
	/// </summary>
	public abstract class ScriptTranslatorManager
	{
		protected List<ScriptCompiler.Translator> _translators = new List<ScriptCompiler.Translator>();

		/// <summary>
		/// Returns the number of translators being managed
		/// </summary>
		public int TranslatorsCount
		{
			get
			{
				return _translators.Count;
			}
		}

		/// <summary>
		/// Returns a manager for the given object abstract node, or null if it is not supported
		/// </summary>
		/// <param name="node"></param>
		/// <returns>Returns a manager for the given object abstract node, or null if it is not supported</returns>
		public ScriptCompiler.Translator GetTranslator( AbstractNode node )
		{
			if ( node is ObjectAbstractNode )
			{
				ObjectAbstractNode obj = (ObjectAbstractNode)node;
				ObjectAbstractNode parent = obj.Parent != null ? (ObjectAbstractNode)obj.Parent : null;
				Keywords parentId = parent != null ? (Keywords)parent.Id : Keywords.ID_ZERO;

				foreach ( ScriptCompiler.Translator currentTranslator in _translators )
				{
					if ( currentTranslator.CheckFor( (Keywords)obj.Id, parentId ) )
						return currentTranslator;
				}
			}

			return null;
		}
	}

	/// <summary>
	/// This class manages the builtin translators
	/// </summary>
	public class BuiltinScriptTranslatorManager : ScriptTranslatorManager
	{
		public BuiltinScriptTranslatorManager()
			: base()
		{
			_translators.Add( new ScriptCompiler.MaterialTranslator() );
			_translators.Add( new ScriptCompiler.TechniqueTranslator() );
			_translators.Add( new ScriptCompiler.PassTranslator() );
			_translators.Add( new ScriptCompiler.TextureUnitTranslator() );

			//TODO uncomment following file when ExternalTextureSourceManager is being implemented
			//_translators.Add( new ScriptCompiler.TextureSourceTranslator() );
			_translators.Add( new ScriptCompiler.GpuProgramTranslator() );
			//TODO add here SharedParamsTranslator

			/**************************************************************************
			* Particle System section
			*************************************************************************/
			_translators.Add( new ScriptCompiler.ParticleSystemTranslator() );
			_translators.Add( new ScriptCompiler.ParticleEmitterTranslator() );
			_translators.Add( new ScriptCompiler.ParticleAffectorTranslator() );

			/**************************************************************************
			* Compositor section
			*************************************************************************/
			_translators.Add( new ScriptCompiler.CompositorTranslator() );
			_translators.Add( new ScriptCompiler.CompositionTechniqueTranslator() );
			_translators.Add( new ScriptCompiler.CompositionTargetPassTranslator() );
			_translators.Add( new ScriptCompiler.CompositionPassTranslator() );
			_translators.Add( new ScriptCompiler.CompositionPassClearTranslator() );
			_translators.Add( new ScriptCompiler.CompositionPassStencilTranslator() );
		}
	}
}