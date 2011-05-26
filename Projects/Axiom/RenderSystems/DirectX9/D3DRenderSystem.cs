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

#endregion LGPL License

#region SVN Version Information

// <file>
//     <license see="http://axiom3d.net/wiki/index.php/license.txt"/>
//     <id value="$Id: D3DRenderSystem.cs 1661 2009-06-11 09:40:16Z borrillis $"/>
// </file>

#endregion SVN Version Information

#region Namespace Declarations

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Axiom.Graphics.Collections;
using Axiom.Media;
using Axiom.RenderSystems.DirectX9.HLSL;
using SlimDX;
using SlimDX.Direct3D9;
using FogMode = Axiom.Graphics.FogMode;
using LightType = Axiom.Graphics.LightType;
using StencilOperation = Axiom.Graphics.StencilOperation;
using Capabilities = Axiom.Graphics.Capabilities;
using Axiom.Collections;
using Axiom.Configuration;
using Axiom.Core;
using Axiom.Core.Collections;
using Axiom.Math;
using Axiom.Graphics;

using DX = SlimDX;
using D3D = SlimDX.Direct3D9;
using Light = Axiom.Core.Light;
using Plane = Axiom.Math.Plane;
using Texture = Axiom.Core.Texture;
using TextureTransform = SlimDX.Direct3D9.TextureTransform;
using Vector3 = Axiom.Math.Vector3;
using Vector4 = Axiom.Math.Vector4;
using VertexDeclaration = Axiom.Graphics.VertexDeclaration;
using Viewport = Axiom.Core.Viewport;

#endregion Namespace Declarations

namespace Axiom.RenderSystems.DirectX9
{
	/// <summary>
	/// DirectX9 Render System implementation.
	/// </summary>
	public partial class D3DRenderSystem : RenderSystem
	{
        // Not implemented methods / fields: 
        // static ResourceManager
        // static DeviceManager
        // notifyOnDeviceLost
        // notifyOnDeviceReset

        [OgreVersion(1, 7, 2790)]
        private HLSLProgramFactory hlslProgramFactory;

	    private D3D9ResourceManager _resourceManager;

        public static D3D9ResourceManager ResourceManager
        {
            get
            {
                return _D3D9RenderSystem._resourceManager;
            }
        }

	    private D3D9DeviceManager _deviceManager;

	    private readonly RenderWindowList _renderWindows = new RenderWindowList();

	    //private Dictionary<D3D.Device, int> _currentLights;
	    private int _currentLights;

		/// <summary>
		///    Reference to the Direct3D
		/// </summary>
		internal D3D.Direct3D manager;

	    internal Driver _activeDriver;

	    private D3DHardwareBufferManager hardwareBufferManager;

		/// <summary>
		/// The one used to create the device.
		/// </summary>
		private D3DRenderWindow _primaryWindow;

		/// <summary>
		///		Should we use the W buffer? (16 bit color only).
		/// </summary>
		protected bool useWBuffer;

		/// <summary>
		///    Number of streams used last frame, used to unbind any buffers not used during the current operation.
		/// </summary>
		protected int _lastVertexSourceCount;

		// stores texture stage info locally for convenience
		internal D3DTextureStageDesc[] texStageDesc = new D3DTextureStageDesc[ Config.MaxTextureLayers ];

		protected int primCount;
		protected int renderCount = 0;

		const int MAX_LIGHTS = 8;
		protected Axiom.Core.Light[] lights = new Axiom.Core.Light[ MAX_LIGHTS ];

		protected D3DGpuProgramManager gpuProgramMgr;


		//---------------------------------------------------------------------
		private bool _basicStatesInitialized;

		//---------------------------------------------------------------------

		List<D3DRenderWindow> _secondaryWindows = new List<D3DRenderWindow>();

		protected Dictionary<D3D.Format, D3D.Format> depthStencilCache = new Dictionary<D3D.Format, D3D.Format>();

		private bool _useNVPerfHUD;
		private bool _vSync;
		private D3D.MultisampleType _fsaaType = D3D.MultisampleType.None;
		private int _fsaaQuality = 0;

		public struct ZBufferFormat
		{
			public ZBufferFormat( D3D.Format f, D3D.MultisampleType m )
			{
				this.format = f;
				this.multisample = m;
			}

			public D3D.Format format;
			public D3D.MultisampleType multisample;
		}
		protected Dictionary<ZBufferFormat, D3D.Surface> zBufferCache = new Dictionary<ZBufferFormat, D3D.Surface>();

		/// <summary>
		///		Temp D3D vector to avoid constant allocations.
		/// </summary>
		private DX.Vector4 tempVec = new DX.Vector4();

		public D3DRenderSystem()
		{
			LogManager.Instance.Write( "[D3D] : Direct3D9 Rendering Subsystem created." );

            // update singleton access pointer.
		    _D3D9RenderSystem = this;

			if ( manager == null || manager.Disposed )
			{
				manager = new D3D.Direct3D();
			}

		    _resourceManager = new D3D9ResourceManager();
		    _deviceManager = new D3D9DeviceManager();

			InitConfigOptions();

			// init the texture stage descriptions
			for ( int i = 0; i < Config.MaxTextureLayers; i++ )
			{
				texStageDesc[ i ].autoTexCoordType = TexCoordCalcMethod.None;
				texStageDesc[ i ].coordIndex = 0;
				texStageDesc[ i ].texType = D3DTextureType.Normal;
				texStageDesc[ i ].tex = null;
				texStageDesc[ i ].vertexTex = null;
			}
		}

	    private static D3DRenderSystem _D3D9RenderSystem;

        public static D3D.Direct3D Direct3D9
        {
            get
            {
                var pDirect3D9 = _D3D9RenderSystem.manager;

		        if (pDirect3D9 == null)
		        {
		            throw new AxiomException("Direct3D9 interface is NULL !!!");
		        }

		        return pDirect3D9;
            }
        }

        #region ActiveD3D9Device

        [OgreVersion(1, 7, 2790)]
        public static Device ActiveD3D9Device
        {
            get
            {
                var activeDevice = _D3D9RenderSystem._deviceManager.ActiveDevice;
                var d3D9Device = activeDevice.D3DDevice;

                if ( d3D9Device == null )
                {
                    throw new AxiomException( "Current d3d9 device is NULL !!!" );
                }

                return d3D9Device;
            }
        }

        #endregion

        #region GetSamplerId

        [OgreVersion(1, 7, 2790)]
        protected int GetSamplerId(int unit)
        {
            return unit + (texStageDesc[ unit ].vertexTex == null ? 0 : (int)VertexTextureSampler.Sampler0);
        }

        #endregion

        #region Implementation of RenderSystem


        /*
        // use default unlike Ogre as we determine this niceley via reflection
        public override string Name
        {
            get
            {
                return "Direct3D9 Rendering Subsystem;
            }
        }
         */

        [OgreVersion(1, 7, 2790)]
		public override ColorEx AmbientLight
		{
			set
			{
				SetRenderState( RenderState.Ambient, D3DHelper.ToColor( value ) );
			}
		}

	    [OgreVersion(1, 7, 2790)]
		public override bool LightingEnabled
		{
			set
			{
				SetRenderState( RenderState.Lighting, value );
			}
		}

        [OgreVersion(1, 7, 2790)]
		public override bool NormalizeNormals
		{
			set
			{
				SetRenderState( RenderState.NormalizeNormals, value );
			}
		}

        [OgreVersion(1, 7, 2790)]
        public override ShadeOptions ShadingType
        {
            set
            {
                SetRenderState(RenderState.ShadeMode, (int)D3DHelper.ConvertEnum(value));
            }
        }

        [OgreVersion(1, 7, 2790)]
		public override bool StencilCheckEnabled
		{
            set
			{
				SetRenderState( D3D.RenderState.StencilEnable, value );
			}
		}

		private bool _deviceLost;

		public bool IsDeviceLost
		{
			get
			{
				return _deviceLost;
			}
			set
			{
				if ( value )
				{
					LogManager.Instance.Write( "!!! Direct3D Device Lost!" );
					_deviceLost = true;
					// will have lost basic states
					_basicStatesInitialized = false;

					//TODO fireEvent("DeviceLost");
				}
				else
				{
					throw new AxiomException( "DeviceLost can only be set to true." );
				}
			}
		}
        #region GetErrorDescription

        [OgreVersion(1, 7, 2790)]
        public override string GetErrorDescription(int errorNumber)
        {
            return string.Format( "D3D9 error {0}", errorNumber );
        }

        #endregion

        #region SetVertexBufferBinding

        public override VertexBufferBinding VertexBufferBinding
        {
            set
            {
                SetVertexBufferBinding(value, 1, true, false);
            }
        }

        /// <summary>
        /// Extended setter for <see cref="VertexBufferBinding" />
		/// </summary>
        [OgreVersion(1, 7, 2790)]
        public void SetVertexBufferBinding(VertexBufferBinding binding,
            int numberOfInstances, bool useGlobalInstancingVertexBufferIsAvailable, bool indexesUsed)
		{
		    if ( useGlobalInstancingVertexBufferIsAvailable )
		    {
		        numberOfInstances *= GlobalNumberOfInstances;
		    }

		    var globalInstanceVertexBuffer = GlobalInstanceVertexBuffer;
		    var globalVertexDeclaration = GlobalInstanceVertexBufferVertexDeclaration;
		    var hasInstanceData = useGlobalInstancingVertexBufferIsAvailable &&
		                          globalInstanceVertexBuffer != null && globalVertexDeclaration != null
		        || binding.HasInstanceData;


		    // TODO: attempt to detect duplicates
		    var binds = binding.Bindings;
		    var source = -1;
		    foreach ( var i in binds )
		    {
		        source++;
		        var d3D9buf = (D3DHardwareVertexBuffer)i.Value;
		        //D3D9HardwareVertexBuffer* d3d9buf = 
		        //	static_cast<D3D9HardwareVertexBuffer*>(i->second.get());

		        // Unbind gap sources
		        for ( ; source < i.Key; ++source )
		        {
		            ActiveD3D9Device.SetStreamSource( source, null, 0, 0 );
		        }

                ActiveD3D9Device.SetStreamSource(source, d3D9buf.D3DVertexBuffer, 0, d3D9buf.VertexSize);

		        // SetStreamSourceFreq
		        if ( hasInstanceData )
		        {
		            if ( d3D9buf.IsInstanceData )
		            {
                        ActiveD3D9Device.SetStreamSourceFrequency(source, d3D9buf.InstanceDataStepRate, StreamSource.InstanceData);
		            }
		            else
		            {
		                if ( !indexesUsed )
		                {
		                    throw new AxiomException( "Instance data used without index data." );
		                }
                        ActiveD3D9Device.SetStreamSourceFrequency(source, numberOfInstances, StreamSource.InstanceData);
		            }
		        }
		        else
		        {
                    // SlimDX workaround see http://www.gamedev.net/topic/564376-solved-slimdx---instancing-problem/
                    ActiveD3D9Device.ResetStreamSourceFrequency(source);
		            //device.SetStreamSourceFrequency( source, 1, StreamSource.IndexedData );
		        }

		    }

		    if ( useGlobalInstancingVertexBufferIsAvailable )
		    {
		        // bind global instance buffer if exist
		        if ( globalInstanceVertexBuffer != null )
		        {
		            if ( !indexesUsed )
		            {
		                throw new AxiomException( "Instance data used without index data." );
		            }

		            var d3D9buf = (D3DHardwareVertexBuffer)globalInstanceVertexBuffer;
                    ActiveD3D9Device.SetStreamSource(source, d3D9buf.D3DVertexBuffer, 0, d3D9buf.VertexSize);

                    ActiveD3D9Device.SetStreamSourceFrequency(source, d3D9buf.InstanceDataStepRate, StreamSource.InstanceData);
		        }

		    }

		    // Unbind any unused sources
		    for ( var unused = source; unused < _lastVertexSourceCount; ++unused )
		    {

                ActiveD3D9Device.SetStreamSource(unused, null, 0, 0);
                ActiveD3D9Device.SetStreamSourceFrequency(source, 1, StreamSource.IndexedData);
		    }
		    _lastVertexSourceCount = source;
		}

        #endregion

        #region ColorVertexElementType

        [OgreVersion(1, 7, 2790)]
        public override VertexElementType ColorVertexElementType
	    {
	        get
	        {
	            return VertexElementType.Color_ARGB;
	        }
	    }

        #endregion

        #region VertexDeclaration

        [OgreVersion(1, 7, 2790)]
        public override VertexDeclaration VertexDeclaration
        {
            set
            {
                SetVertexDeclaration( value, true );
            }
        }

        ///<summary>
        ///</summary>
        [OgreVersion(1, 7, 2790, "TODO: implement useGlobalInstancingVertexBufferIsAvailable")]
        public void SetVertexDeclaration(VertexDeclaration decl, bool useGlobalInstancingVertexBufferIsAvailable)
        {
            // TODO: Check for duplicate setting and avoid setting if dupe
            var d3DVertDecl = (D3DVertexDeclaration)decl;

            ActiveD3D9Device.VertexDeclaration = d3DVertDecl.D3DVertexDecl;
        }

        #endregion

        #region ClearFrameBuffer

        [OgreVersion(1, 7, 2790)]
	    public override void ClearFrameBuffer( FrameBufferType buffers, ColorEx color, Real depth, ushort stencil )
		{
			ClearFlags flags = 0;

			if ( ( buffers & FrameBufferType.Color ) > 0 )
			{
				flags |= ClearFlags.Target;
			}
			if ( ( buffers & FrameBufferType.Depth ) > 0 )
			{
				flags |= ClearFlags.ZBuffer;
			}
			// Only try to clear the stencil buffer if supported
			if ( ( buffers & FrameBufferType.Stencil ) > 0
				&& Capabilities.HasCapability( Graphics.Capabilities.StencilBuffer ) )
			{
				flags |= ClearFlags.Stencil;
			}

			// clear the device using the specified params
            ActiveD3D9Device.Clear(flags, color.ToARGB(), depth, stencil);
		}

        #endregion

        #region CreateHardwareOcclusionQuery

        /// <summary>
		///		Returns a Direct3D implementation of a hardware occlusion query.
		/// </summary>
        [OgreVersion(1, 7, 2790, "D3DHardwareOcclusionQuery ctor needs upgrade")]
		public override HardwareOcclusionQuery CreateHardwareOcclusionQuery()
		{
            var query = new D3DHardwareOcclusionQuery( ActiveD3D9Device );
		    hwOcclusionQueries.Add( query );
		    return query;
		}

        #endregion

        #region CreateRenderWindow

        [OgreVersion(1, 7, 2790)]
		public override RenderWindow CreateRenderWindow( string name, int width, int height, bool isFullScreen, NamedParameterList miscParams )
		{
            LogManager.Instance.Write("D3D9RenderSystem::createRenderWindow \"{0}\", {1}x{2} {3} ",
                                       name, width, height, isFullScreen ? "fullscreen" : "windowed");

		    LogManager.Instance.Write( "miscParams: {0}",
		                               miscParams.Aggregate( new StringBuilder(),
		                                                     ( s, kv ) =>
		                                                     s.AppendFormat( "{0} = {1};", kv.Key, kv.Value ).AppendLine()
		                                   ).ToString()
		        );

            // Make sure we don't already have a render target of the
            // same name as the one supplied
            if (renderTargets.ContainsKey(name))
            {
                throw new Exception(String.Format("A render target of the same name '{0}' already exists." +
                                     "You cannot create a new window with this name.", name));
            }

            var window = new D3DRenderWindow(_activeDriver, _primaryWindow != null ? ActiveD3D9Device : null);

            window.Create(name, width, height, isFullScreen, miscParams);

		    _resourceManager.LockDeviceAccess();

		    _deviceManager.LinkRenderWindow( window );

            _resourceManager.UnlockDeviceAccess();

		    _renderWindows.Add( window );

		    UpdateRenderSystemCapabilities( window );

            AttachRenderTarget( window );

			return window;
		}

        #endregion

        public override MultiRenderTarget CreateMultiRenderTarget( string name )
		{
			MultiRenderTarget retval = new D3DMultiRenderTarget( name );
			AttachRenderTarget( retval );
			return retval;
		}

        [OgreVersion(1, 7, 2790)]
		public override void Shutdown()
		{
            base.Shutdown();

            _activeDriver = null;


            if (_deviceManager != null)
            {
                _deviceManager.Dispose();
                _deviceManager = null;
            }

            if (_driverList != null)
            {
                _driverList.Dispose();
                _driverList = null;
            }

            _activeDriver = null;

            LogManager.Instance.Write( "D3D9 : Shutting down cleanly." );

            if (textureManager != null)
            {
                textureManager.Dispose();
                textureManager = null;
            }

            if (hardwareBufferManager != null)
            {
                hardwareBufferManager.Dispose();
                hardwareBufferManager = null;
            }

            if (gpuProgramMgr != null)
            {
                gpuProgramMgr.Dispose();
                gpuProgramMgr = null;
            }			
		}

        protected override void dispose(bool disposeManagedResources)
        {
            // this causes infinite recursions in axiom
            //Shutdown();

            // Deleting the HLSL program factory
            if ( hlslProgramFactory != null )
            {
                // Remove from manager safely
                if ( HighLevelGpuProgramManager.Instance != null )
                    HighLevelGpuProgramManager.Instance.RemoveFactory( hlslProgramFactory );
                hlslProgramFactory.Dispose();
                hlslProgramFactory = null;
            }

            if ( manager != null )
            {
                manager.Dispose();
                manager = null;
            }

            if ( _resourceManager != null )
            {
                _resourceManager.Dispose();
                _resourceManager = null;
            }

            LogManager.Instance.Write( "D3D9 : {0} destroyed.", Name );

            _D3D9RenderSystem = null;

            base.dispose( disposeManagedResources );
        }

	    #region Reinitialize

        [OgreVersion(1, 7, 2790)]
        public override void Reinitialize()
        {
            LogManager.Instance.Write( "D3D9 : Reinitialising" );
            Shutdown();
            Initialize( true, "Axiom Window" );
        }

        #endregion

        #region CreateRenderSystemCapabilities

        [OgreVersion(1, 7, 2790)]
	    public override RenderSystemCapabilities CreateRenderSystemCapabilities()
	    {
	        return realCapabilities;
	    }

        #endregion

        #region PolygonMode

        [OgreVersion(1, 7, 2790)]
		public override PolygonMode PolygonMode
		{
			set
			{
                SetRenderState(RenderState.FillMode, (int)D3DHelper.ConvertEnum(value));
			}
		}

        #endregion

        #region SetAlphaRejectSettings

        [OgreVersion(1, 7, 2790)]
		public override void SetAlphaRejectSettings( CompareFunction func, byte value, bool alphaToCoverage)
		{
			var a2C = false;

			if ( func != CompareFunction.AlwaysPass )
			{
				SetRenderState( RenderState.AlphaTestEnable, true );
				a2C = alphaToCoverage;
			}
			else
			{
				SetRenderState( RenderState.AlphaTestEnable, false );
			}

            // Set always just be sure
            SetRenderState(RenderState.AlphaFunc, (int)D3DHelper.ConvertEnum(func));
            SetRenderState(RenderState.AlphaRef, value);

			// Alpha to coverage
			if ( Capabilities.HasCapability( Graphics.Capabilities.AlphaToCoverage ) )
			{
				// Vendor-specific hacks on renderstate, gotta love 'em
                if (Capabilities.Vendor == GPUVendor.Nvidia)
				{
					if ( a2C )
					{
						SetRenderState( RenderState.AdaptiveTessY, ( 'A' | ( 'T' ) << 8 | ( 'O' ) << 16 | ( 'C' ) << 24 ) );
					}
					else
					{
						SetRenderState( RenderState.AdaptiveTessY, (int)Format.Unknown );
					}
				}
                else if (Capabilities.Vendor == GPUVendor.Ati)
				{
					if ( a2C )
					{
						SetRenderState( RenderState.AdaptiveTessY, ( 'A' | ( '2' ) << 8 | ( 'M' ) << 16 | ( '1' ) << 24 ) );
					}
					else
					{
						// discovered this through trial and error, seems to work
						SetRenderState( RenderState.AdaptiveTessY, ( 'A' | ( '2' ) << 8 | ( 'M' ) << 16 | ( '0' ) << 24 ) );
					}
				}
				// no hacks available for any other vendors?
				//lasta2c = a2c;
			}
		}

        #endregion

        [OgreVersion(1, 7, 2790, "Implement this")]
	    public override DepthBuffer CreateDepthBufferFor( RenderTarget renderTarget )
	    {
	        throw new NotImplementedException();
	    }

        #region SetColorBufferWriteEnabled

        [OgreVersion(1, 7, 2790)]
		public override void SetColorBufferWriteEnabled( bool red, bool green, bool blue, bool alpha )
		{
			ColorWriteEnable val = 0;

			if ( red )
				val |= ColorWriteEnable.Red;
			if ( green )
				val |= ColorWriteEnable.Green;
			if ( blue )
				val |= ColorWriteEnable.Blue;
			if ( alpha )
				val |= ColorWriteEnable.Alpha;
			
	        SetRenderState( RenderState.ColorWriteEnable, (int)val );
		}

        #endregion

        #region SetFog

        public override void SetFog( FogMode mode, ColorEx color, Real density,
            Real start, Real end)
		{
            RenderState fogType, fogTypeNot;
            if ((_deviceManager.ActiveDevice.D3D9DeviceCaps.RasterCaps & RasterCaps.FogTable) != 0)
            {
                fogType = RenderState.FogTableMode;
                fogTypeNot = RenderState.FogVertexMode;
            }
            else
            {
                fogType = RenderState.FogVertexMode;
                fogTypeNot = RenderState.FogTableMode;
            }

			if ( mode == FogMode.None )
			{
                // just disable
				SetRenderState( RenderState.FogTableMode, (int)D3D.FogMode.None );
				SetRenderState( RenderState.FogEnable, false );
			}
			else
			{
                // Allow fog
                SetRenderState(RenderState.FogEnable, true);
                SetRenderState(fogTypeNot, (int)FogMode.None);
                SetRenderState(fogType, (int)D3DHelper.ConvertEnum(mode));

				SetRenderState( RenderState.FogColor, D3DHelper.ToColor( color ).ToArgb() );
				SetFloatRenderState( RenderState.FogStart, (float)start );
                SetFloatRenderState(RenderState.FogEnd, (float)end);
                SetFloatRenderState(RenderState.FogDensity, (float)density);
			}
		}

        #endregion

        #region MinimumDepthInputValue

        [OgreVersion(1, 7, 2790)]
		public override Real MinimumDepthInputValue
		{
			get
			{
				// Range [0.0f, 1.0f]
				return 0.0f;
			}
		}

        #endregion

        #region MaximumDepthInputValue

        [OgreVersion(1, 7, 2790)]
		public override Real MaximumDepthInputValue
		{
			get
			{
				// Range [0.0f, 1.0f]
				// D3D inverts even identity view matrixes so maximum INPUT is -1.0f
				return -1.0f;
			}
		}

        #endregion

        #region RegisterThread

        [OgreVersion(1, 7, 2790)]
        public override void RegisterThread()
        {
            // nothing to do - D3D9 shares rendering context already
        }

        #endregion

        #region UnregisterThread

        [OgreVersion(1, 7, 2790)]
        public override void UnregisterThread()
        {
            // nothing to do - D3D9 shares rendering context already
        }

        #endregion

        #region PreExtraThreadsStarted

        [OgreVersion(1, 7, 2790)]
        public override void PreExtraThreadsStarted()
        {
            // nothing to do - D3D9 shares rendering context already
        }

        #endregion

        #region PostExtraThreadsStarted

        [OgreVersion(1, 7, 2790)]
        public override void PostExtraThreadsStarted()
        {
            // nothing to do - D3D9 shares rendering context already
        }

        #endregion

        #region BeginFrame

        [OgreVersion(1, 7, 2790)]
	    public override void BeginFrame()
		{
            if (activeViewport == null)
                throw new AxiomException( "BeingFrame cannot run without an active viewport." );

			// begin the D3D scene for the current viewport
			ActiveD3D9Device.BeginScene();

		    _lastVertexSourceCount = 0;

            // Clear left overs of previous viewport.
            // I.E: Viewport A can use 3 different textures and light states
            // When trying to render viewport B these settings should be cleared, otherwise 
            // graphical artifacts might occur.
            _deviceManager.ActiveDevice.ClearDeviceStreams();
		}

        #endregion

        /*
        // This effectivley aint used in 1.7 ...
        [OgreVersion(1, 7, 2790)]
        class D3D9RenderContext : RenderSystemContext
        {
            public RenderTarget target;
        }
         */

        #region PauseFrame

        [OgreVersion(1, 7, 2790)]
        public override RenderSystemContext PauseFrame()
        {
            //Stop rendering
            EndFrame();
            //return new D3D9RenderContext { target = activeRenderTarget };
            return null;
        }

        #endregion

        #region ResumeFrame

        [OgreVersion(1, 7, 2790)]
        public override void ResumeFrame(RenderSystemContext context)
        {
            //Resume rendering
            BeginFrame();

            //var d3dContext = (D3D9RenderContext)context;
        }

        #endregion

        #region EndFrame

        [OgreVersion(1, 7, 2790, "TODO: destroyInactiveRenderDevices")]
		public override void EndFrame()
		{
			// end the D3D scene
            ActiveD3D9Device.EndScene();
		}

        #endregion

        #region Viewport

        [OgreVersion(1, 7, 2790)]
        public override Viewport Viewport
        {
            set
            {
                if (value == null)
                {
                    activeViewport = null;
                    activeRenderTarget = null;
                }
                else if (activeViewport != value || value.IsUpdated)
                {
                    // store this viewport and it's target
                    activeViewport = value;

                    var target = value.Target;
                    activeRenderTarget = target;

                    // set the culling mode, to make adjustments required for viewports
                    // that may need inverted vertex winding or texture flipping
                    CullingMode = cullingMode;

                    var d3Dvp = new D3D.Viewport();
                    // set viewport dimensions
                    d3Dvp.X = value.ActualLeft;
                    d3Dvp.Y = value.ActualTop;
                    d3Dvp.Width = value.ActualWidth;
                    d3Dvp.Height = value.ActualHeight;

                    if (target.RequiresTextureFlipping)
                    {
                        // Convert "top-left" to "bottom-left"
                        d3Dvp.Y = activeRenderTarget.Height - d3Dvp.Height - d3Dvp.Y;
                    }

                    // Z-values from 0.0 to 1.0
                    // TODO: standardize with OpenGL
                    d3Dvp.MinZ = 0.0f;
                    d3Dvp.MaxZ = 1.0f;

                    // set the current D3D viewport
                    ActiveD3D9Device.Viewport = d3Dvp;

                    // Set sRGB write mode
                    SetRenderState( RenderState.SrgbWriteEnable, target.IsHardwareGammaEnabled );

                    // clear the updated flag
                    value.ClearUpdatedFlag();                    
                }
            }
        }

        #endregion

        private static readonly Format[] _preferredStencilFormats = {
			Format.D24SingleS8,
			Format.D24S8,
			Format.D24X4S4,
			Format.D24X8,
			Format.D15S1,
			Format.D16,
			Format.D32
		};

        #region GetDepthStencilFormatFor

        [OgreVersion(1, 7, 2790, "Needs review")]
		private Format GetDepthStencilFormatFor( Format fmt )
		{
			Format dsfmt;
			// Check if result is cached
			if ( depthStencilCache.TryGetValue( fmt, out dsfmt ) )
				return dsfmt;

			// If not, probe with CheckDepthStencilMatch
			dsfmt = Format.Unknown;

			// Get description of primary render target
			var surface = _primaryWindow.RenderSurface;
			var srfDesc = surface.Description;

			// Probe all depth stencil formats
			// Break on first one that matches
			foreach ( var df in _preferredStencilFormats )
			{
				// Verify that the depth format exists
				if ( !manager.CheckDeviceFormat( _activeDriver.AdapterNumber, DeviceType.Hardware, srfDesc.Format, Usage.DepthStencil, ResourceType.Surface, df ) )
					continue;
				// Verify that the depth format is compatible
				if ( manager.CheckDepthStencilMatch( _activeDriver.AdapterNumber, DeviceType.Hardware, srfDesc.Format, fmt, df ) )
				{
					dsfmt = df;
					break;
				}
			}
			// Cache result
			depthStencilCache[ fmt ] = dsfmt;
			return dsfmt;
		}

        #endregion

        #region CheckTextureFilteringSupported

        [OgreVersion(1, 7, 2790, "Not implemented yet")]
        private bool CheckTextureFilteringSupported(TextureType ttype, PixelFormat format, int usage)
	    {
		    // Gets D3D format

            var d3Dpf = D3DHelper.ConvertEnum(format);
		    if (d3Dpf == Format.Unknown)
			    return false;

            throw new NotImplementedException();
	    }

        #endregion

        #region DetermineFSAASettings

        [OgreVersion(1, 7, 2790, "Not implemented yet")]
        internal void DetermineFSAASettings(Device d3d9Device,
            int fsaa, string fsaaHint, Format d3DPixelFormat,
            bool fullScreen, out MultisampleType outMultisampleType, out int outMultisampleQuality)
        {
            outMultisampleType = MultisampleType.None;
            outMultisampleQuality = 0;

            var ok = false;
		    var qualityHint = fsaaHint.Contains("Quality");
		    var origFSAA = fsaa;

		    var driverList = Direct3DDrivers;
            var deviceDriver = _activeDriver;
            var device = _deviceManager.GetDeviceFromD3D9Device(d3d9Device);

            foreach (var currDriver in driverList)
            {
                if ( currDriver.AdapterNumber != device.AdapterNumber )
                    continue;
                deviceDriver = currDriver;
                break;
            }

		    var tryCSAA = false;
		    // NVIDIA, prefer CSAA if available for 8+
		    // it would be tempting to use getCapabilities()->getVendor() == GPU_NVIDIA but
		    // if this is the first window, caps will not be initialised yet
		    if (deviceDriver.AdapterIdentifier.VendorId == 0x10DE && 
			    fsaa >= 8)
		    {
			    tryCSAA	 = true;
		    }
            
		    while (!ok)
		    {
			    // Deal with special cases
			    if (tryCSAA)
			    {
				    // see http://developer.nvidia.com/object/coverage-sampled-aa.html
				    switch(fsaa)
				    {
				    case 8:
					    if (qualityHint)
					    {
					        outMultisampleType = MultisampleType.EightSamples;
						    outMultisampleQuality = 0;
					    }
					    else
					    {
                            outMultisampleType = MultisampleType.FourSamples;
						    outMultisampleQuality = 2;
					    }
					    break;
				    case 16:
					    if (qualityHint)
					    {
                            outMultisampleType = MultisampleType.EightSamples;
						    outMultisampleQuality = 2;
					    }
					    else
					    {
                            outMultisampleType = MultisampleType.FourSamples;
						    outMultisampleQuality = 4;
					    }
					    break;
				    }
			    }
			    else // !CSAA
			    {
                    outMultisampleType = (MultisampleType)fsaa;
				    outMultisampleQuality = 0;
			    }

		        int outQuality;
		        var hr = manager.CheckDeviceMultisampleType(
		            deviceDriver.AdapterNumber, DeviceType.Hardware, d3DPixelFormat,
		            fullScreen, outMultisampleType, out outQuality );

                if (hr && (!tryCSAA || outQuality > outMultisampleQuality))
                {
                    ok = true;
                }
			    else
			    {
				    // downgrade
				    if (tryCSAA && fsaa == 8)
				    {
					    // for CSAA, we'll try downgrading with quality mode at all samples.
					    // then try without quality, then drop CSAA
					    if (qualityHint)
					    {
						    // drop quality first
						    qualityHint = false;
					    }
					    else
					    {
						    // drop CSAA entirely 
						    tryCSAA = false;
					    }
					    // return to original requested samples
					    fsaa = origFSAA;
				    }
				    else
				    {
					    // drop samples
					    --fsaa;

					    if (fsaa == 1)
					    {
						    // ran out of options, no FSAA
						    fsaa = 0;
						    ok = true;
					    }
				    }
			    }

		    } // while !ok
        }

        #endregion

        #region DisplayMonitorCount

        [OgreVersion(1, 7, 2790)]
        public override int DisplayMonitorCount
        {
            get
            {
                return manager.AdapterCount;
            }
        }

        #endregion
		
        [OgreVersion(1, 7, 2790, "TODO: RT System")]
		public override void Render( RenderOperation op )
		{
            // Exit immediately if there is nothing to render
            // This caused a problem on FireGL 8800
			if ( op.vertexData.vertexCount == 0 )
			{
				return;
			}

		    base.Render( op );

            // To think about: possibly remove setVertexDeclaration and 
            // setVertexBufferBinding from RenderSystem since the sequence is
            // a bit too D3D9-specific?
			VertexDeclaration = op.vertexData.vertexDeclaration;
            // TODO: the false parameter has to be carried inside op as var
            SetVertexBufferBinding(op.vertexData.vertexBufferBinding, op.numberOfInstances, false, op.useIndices);

            // Determine rendering operation
            var primType = PrimitiveType.TriangleList;
			var lprimCount = op.vertexData.vertexCount;
			var cnt = op.useIndices && primType != PrimitiveType.PointList ? op.indexData.indexCount : op.vertexData.vertexCount;

			switch ( op.operationType )
			{
				case OperationType.TriangleList:
					primType = PrimitiveType.TriangleList;
					lprimCount = cnt / 3;
					break;
				case OperationType.TriangleStrip:
					primType = PrimitiveType.TriangleStrip;
					lprimCount = cnt - 2;
					break;
				case OperationType.TriangleFan:
					primType = PrimitiveType.TriangleFan;
					lprimCount = cnt - 2;
					break;
				case OperationType.PointList:
					primType = PrimitiveType.PointList;
					lprimCount = cnt;
					break;
				case OperationType.LineList:
					primType = PrimitiveType.LineList;
					lprimCount = cnt / 2;
					break;
				case OperationType.LineStrip:
					primType = PrimitiveType.LineStrip;
					lprimCount = cnt - 1;
					break;
			} // switch(primType)

            if (lprimCount == 0)
                return;


            if (op.useIndices)
            {
                var d3DIdxBuf = (D3DHardwareIndexBuffer)op.indexData.indexBuffer;
                ActiveD3D9Device.Indices = d3DIdxBuf.D3DIndexBuffer;
                do
                {
                    // Update derived depth bias
                    if (derivedDepthBias && currentPassIterationNum > 0)
                    {
                        SetDepthBias(derivedDepthBiasBase +
                            derivedDepthBiasMultiplier * currentPassIterationNum,
                            derivedDepthBiasSlopeScale);
                    }

                    // draw the indexed primitives
                    ActiveD3D9Device.DrawIndexedPrimitives(primType, 
                        op.vertexData.vertexStart, 
                        0, 
                        op.vertexData.vertexCount, 
                        op.indexData.indexStart, 
                        lprimCount);
                } while (UpdatePassIterationRenderState());
            }
            else
            {
                // nfz: gpu_iterate
                do
                {
                    // Update derived depth bias
                    if (derivedDepthBias && currentPassIterationNum > 0)
                    {
                        SetDepthBias(derivedDepthBiasBase +
                            derivedDepthBiasMultiplier * currentPassIterationNum,
                            derivedDepthBiasSlopeScale);
                    }
                    // Unindexed, a little simpler!
                    ActiveD3D9Device.DrawPrimitives(primType, op.vertexData.vertexStart, lprimCount);
                } while (UpdatePassIterationRenderState());
            }
		}

		
        [OgreVersion(1, 7, 2790)]
        public override void SetPointParameters(Real size, bool attenuationEnabled,
            Real constant, Real linear, Real quadratic, Real minSize, Real maxSize)
		{
			if ( attenuationEnabled )
			{
				//scaling required
				SetRenderState( RenderState.PointScaleEnable, true );
				SetFloatRenderState( RenderState.PointScaleA, constant );
                SetFloatRenderState(RenderState.PointScaleB, linear);
                SetFloatRenderState(RenderState.PointScaleC, quadratic);
			}
			else
			{
				//no scaling required
				SetRenderState( RenderState.PointScaleEnable, false );
			}

            SetFloatRenderState( RenderState.PointSize, size );
            SetFloatRenderState( RenderState.PointSizeMin, minSize );
			if ( maxSize == 0.0f )
			{
				maxSize = Capabilities.MaxPointSize;
			}
            SetFloatRenderState( RenderState.PointSizeMax, maxSize );
		}


        [OgreVersion(1, 7, 2790, "Hardware gamma not implemented, yet")]
		public override void SetTexture( int stage, bool enabled, Texture texture )
		{
			var dxTexture = (D3DTexture)texture;

			if ( enabled && dxTexture != null )
			{
				// note used
				dxTexture.Touch();

			    var ptex = dxTexture.DXTexture;
				if ( texStageDesc[ stage ].tex != ptex )
				{
					ActiveD3D9Device.SetTexture( stage, ptex );

					// set stage description
					texStageDesc[ stage ].tex = ptex;
					texStageDesc[ stage ].texType = D3DHelper.ConvertEnum( dxTexture.TextureType );
				}
                if (dxTexture.HardwareGammaEnabled)
                {
                    SetSamplerState( GetSamplerId( stage ), SamplerState.SrgbTexture, true );
                }
                else
                {
                    SetSamplerState( GetSamplerId( stage ), SamplerState.SrgbTexture, false );
                }
			}
			else
			{
				if ( texStageDesc[ stage ].tex != null )
				{
                    ActiveD3D9Device.SetTexture(stage, null);
				}

				
				SetTextureStageState( stage, TextureStage.ColorOperation, TextureOperation.Disable );

                // set stage description to defaults
				texStageDesc[ stage ].tex = null;
				texStageDesc[ stage ].autoTexCoordType = TexCoordCalcMethod.None;
				texStageDesc[ stage ].coordIndex = 0;
				texStageDesc[ stage ].texType = D3DTextureType.Normal;
			}
		}

        private void SetSamplerState(int sampler, SamplerState type, bool value)
        {
            SetSamplerState( sampler, type, value ? 1 : 0 );
        }
        private void SetSamplerState(int sampler, SamplerState type, int value)
        {
            var oldVal = ActiveD3D9Device.GetSamplerState( sampler, type );
            if (oldVal == value)
                return;

            ActiveD3D9Device.SetSamplerState(sampler, type, value);
        }


	    [OgreVersion(1, 7, 2790)]
		public override void SetTextureLayerAnisotropy( int stage, int maxAnisotropy )
		{

            if (maxAnisotropy > _deviceManager.ActiveDevice.D3D9DeviceCaps.MaxAnisotropy)
                maxAnisotropy = _deviceManager.ActiveDevice.D3D9DeviceCaps.MaxAnisotropy;

			if ( ActiveD3D9Device.GetSamplerState( stage, SamplerState.MaxAnisotropy ) != maxAnisotropy )
			{
				SetSamplerState( stage, SamplerState.MaxAnisotropy, maxAnisotropy );
			}
		}

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureCoordCalculation( int stage, TexCoordCalcMethod method, Frustum frustum )
		{
			// save this for texture matrix calcs later
			texStageDesc[ stage ].autoTexCoordType = method;
			texStageDesc[ stage ].frustum = frustum;

            SetTextureStageState( stage, TextureStage.TexCoordIndex,
                                  D3DHelper.ConvertEnum( method, _deviceManager.ActiveDevice.D3D9DeviceCaps ) |
                                  texStageDesc[ stage ].coordIndex );
		}

        [OgreVersion(1, 7, 2790, "Ogre silently ignores binding GS; Axiom will throw.")]
		public override void BindGpuProgram( GpuProgram program )
		{
			switch ( program.Type )
			{
				case GpuProgramType.Vertex:
					ActiveD3D9Device.VertexShader = ( (D3DVertexProgram)program ).VertexShader;
					break;

				case GpuProgramType.Fragment:
                    ActiveD3D9Device.PixelShader = ((D3DFragmentProgram)program).PixelShader;
					break;

                case GpuProgramType.Geometry:
			        throw new AxiomException( "Geometry shaders not supported with D3D9" );
			}

            // Make sure texcoord index is equal to stage value, As SDK Doc suggests:
		    // "When rendering using vertex shaders, each stage's texture coordinate index must be set to its default value."
		    // This solves such an errors when working with the Debug runtime -
		    // "Direct3D9: (ERROR) :Stage 1 - Texture coordinate index in the stage must be equal to the stage index when programmable vertex pipeline is used".
		    for (var nStage=0; nStage < 8; ++nStage)
                SetTextureStageState(nStage, TextureStage.TexCoordIndex, nStage);

			base.BindGpuProgram( program );
		}

        [OgreVersion(1, 7, 2790, "Partially outdated, need GpuProgramParameters updates")]
        public override void BindGpuProgramParameters(GpuProgramType gptype, GpuProgramParameters parms, GpuProgramParameters.GpuParamVariability variability)
		{
            // special case pass iteration
            if (variability == GpuProgramParameters.GpuParamVariability.PassIterationNumber)
            {
                BindGpuProgramPassIterationParameters(gptype);
                return;
            }

            if ((variability & GpuProgramParameters.GpuParamVariability.Global) != 0)
            {
                // D3D9 doesn't support shared constant buffers, so use copy routine
                parms.CopySharedParams();
            }

            switch ( gptype )
			{
				case GpuProgramType.Vertex:
                    activeVertexGpuProgramParameters = parms;
					if ( parms.HasIntConstants )
					{
						for ( int index = 0; index < parms.IntConstantCount; index++ )
						{
							var entry = parms.GetIntConstant( index );

							if ( entry.isSet )
							{
								ActiveD3D9Device.SetVertexShaderConstant( index, entry.val, 0, 1 );
							}
						}
					}

					if ( parms.HasFloatConstants )
					{
						for ( int index = 0; index < parms.FloatConstantCount; index++ )
						{
							var entry = parms.GetFloatConstant( index );

							if ( entry.isSet )
							{
                                ActiveD3D9Device.SetVertexShaderConstant(index, entry.val, 0, 1);
							}
						}
					}

					break;

				case GpuProgramType.Fragment:
			        activeFragmentGpuProgramParameters = parms;
					if ( parms.HasIntConstants )
					{
						for ( int index = 0; index < parms.IntConstantCount; index++ )
						{
							GpuProgramParameters.IntConstantEntry entry = parms.GetIntConstant( index );

							if ( entry.isSet )
							{
                                ActiveD3D9Device.SetPixelShaderConstant(index, entry.val, 0, 1);
							}
						}
					}

					if ( parms.HasFloatConstants )
					{
						for ( int index = 0; index < parms.FloatConstantCount; index++ )
						{
							GpuProgramParameters.FloatConstantEntry entry = parms.GetFloatConstant( index );

							if ( entry.isSet )
							{
                                ActiveD3D9Device.SetPixelShaderConstant(index, entry.val, 0, 1);
							}
						}
					}
					break;
			}
		}

        [OgreVersion(1, 7, 2790, "Not implemented, yet")]
        public override void BindGpuProgramPassIterationParameters(GpuProgramType gptype)
        {
            var physicalIndex = 0;
            var logicalIndex = 0;
            throw new NotImplementedException();
        }

        [OgreVersion(1, 7, 2790)]
		public override void UnbindGpuProgram( GpuProgramType type )
		{
			switch ( type )
			{
				case GpuProgramType.Vertex:
			        activeVertexGpuProgramParameters = null;
					ActiveD3D9Device.VertexShader = null;
					break;

				case GpuProgramType.Fragment:
			        activeFragmentGpuProgramParameters = null;
                    ActiveD3D9Device.PixelShader = null;
					break;
			}

			base.UnbindGpuProgram( type );
		}

        [OgreVersion(1, 7, 2790)]
		public override void SetVertexTexture( int stage, Texture texture )
		{
			if ( texture == null )
			{
				if ( texStageDesc[ stage ].vertexTex != null )
				{
					var result = ActiveD3D9Device.SetTexture( ( (int)VertexTextureSampler.Sampler0 ) + stage, null );
					if ( result.IsFailure )
					{
						throw new AxiomException( "Unable to disable vertex texture '{0}' in D3D9.", stage );
					}
				}
				texStageDesc[ stage ].vertexTex = null;
			}
			else
			{
				var dt = (D3DTexture)texture;
                // note used
				dt.Touch();

				var ptex = dt.DXTexture;

				if ( texStageDesc[ stage ].vertexTex != ptex )
				{
                    var result = ActiveD3D9Device.SetTexture(((int)VertexTextureSampler.Sampler0) + stage, ptex);
					if ( result.IsFailure )
					{
                        throw new AxiomException("Unable to set vertex texture '{0}' in D3D9.", dt.Name);
					}
				}
				texStageDesc[ stage ].vertexTex = ptex;
			}
		}

		#endregion Implementation of RenderSystem

        #region DisableTextureUnit

        [OgreVersion(1, 7, 2790)]
        public override void DisableTextureUnit(int texUnit)
        {
            base.DisableTextureUnit( texUnit );
            // also disable vertex texture unit
            SetVertexTexture( texUnit, null );
        }

        #endregion

        #region UseLights

        [OgreVersion(1, 7, 2790, "sharing _currentLights rather than using Dictionary")]
		public override void UseLights( LightList lightList, int limit )
		{
			var i = 0;

			for ( ; i < limit && i < lightList.Count; i++ )
			{
				SetD3DLight( i, lightList[ i ] );
			}

            for (; i < _currentLights; i++)
			{
				SetD3DLight( i, null );
			}

            _currentLights = Utility.Min(limit, lightList.Count);
		}

        #endregion

        #region SetSceneBlending

        [OgreVersion(1, 7, 2790)]
        public override void SetSceneBlending(SceneBlendFactor src, SceneBlendFactor dest, SceneBlendOperation op = SceneBlendOperation.Add)
		{
			// set the render states after converting the incoming values to D3D.Blend
			if ( src == SceneBlendFactor.One && dest == SceneBlendFactor.Zero )
			{
				SetRenderState( RenderState.AlphaBlendEnable, false );
			}
			else
			{
				SetRenderState( RenderState.AlphaBlendEnable, true );
				SetRenderState( RenderState.SeparateAlphaBlendEnable, false );
				SetRenderState( RenderState.SourceBlend, (int)D3DHelper.ConvertEnum( src ) );
				SetRenderState( RenderState.DestinationBlend, (int)D3DHelper.ConvertEnum( dest ) );
			}

            SetRenderState( RenderState.BlendOperation, (int)D3DHelper.ConvertEnum( op ) );
            SetRenderState( RenderState.BlendOperationAlpha, (int)D3DHelper.ConvertEnum( op ) );
		}

        #endregion

        #region SetSeparateSceneBlending

        [OgreVersion(1, 7, 2790)]
        public override void SetSeparateSceneBlending( SceneBlendFactor sourceFactor, SceneBlendFactor destFactor, SceneBlendFactor sourceFactorAlpha,
            SceneBlendFactor destFactorAlpha, SceneBlendOperation op = SceneBlendOperation.Add, SceneBlendOperation alphaOp = SceneBlendOperation.Add )
		{
			if ( sourceFactor == SceneBlendFactor.One && destFactor == SceneBlendFactor.Zero &&
				 sourceFactorAlpha == SceneBlendFactor.One && destFactorAlpha == SceneBlendFactor.Zero )
			{
				SetRenderState( RenderState.AlphaBlendEnable, false );
			}
			else
			{
				SetRenderState( RenderState.AlphaBlendEnable, true );
				SetRenderState( RenderState.SeparateAlphaBlendEnable, true );
				SetRenderState( RenderState.SourceBlend, (int)D3DHelper.ConvertEnum( sourceFactor ) );
				SetRenderState( RenderState.DestinationBlend, (int)D3DHelper.ConvertEnum( destFactor ) );
				SetRenderState( RenderState.SourceBlendAlpha, (int)D3DHelper.ConvertEnum( sourceFactorAlpha ) );
				SetRenderState( RenderState.DestinationBlendAlpha, (int)D3DHelper.ConvertEnum( destFactorAlpha ) );
			}

            SetRenderState(RenderState.BlendOperation, (int)D3DHelper.ConvertEnum(op));
            SetRenderState(RenderState.BlendOperationAlpha, (int)D3DHelper.ConvertEnum(alphaOp));
		}

        #endregion

        #region CullingMode

        [OgreVersion(1, 7, 2790)]
		public override CullingMode CullingMode
		{
			set
			{
				cullingMode = value;

				var flip = activeRenderTarget.RequiresTextureFlipping ^ invertVertexWinding;

			    SetRenderState( RenderState.CullMode, (int)D3DHelper.ConvertEnum( value, flip ) );
			}
		}

        #endregion

        #region SetDepthBias

        [OgreVersion(1, 7, 2790)]
        public override void SetDepthBias(float constantBias, float slopeScaleBias = 0.0f)
        {
            // Negate bias since D3D is backward
            // D3D also expresses the constant bias as an absolute value, rather than 
            // relative to minimum depth unit, so scale to fit
            constantBias = -constantBias/250000.0f;
            SetRenderState(RenderState.DepthBias, FLOAT2DWORD(constantBias));

            if ((_deviceManager.ActiveDevice.D3D9DeviceCaps.RasterCaps & RasterCaps.SlopeScaleDepthBias) != 0)
            {
                // Negate bias since D3D is backward
                slopeScaleBias = -slopeScaleBias;
                SetRenderState(RenderState.SlopeScaleDepthBias, FLOAT2DWORD(slopeScaleBias));
            }
        }

        #endregion

        #region ValidateConfigOptions

        [OgreVersion(1, 7, 2790)]
	    public override string ValidateConfigOptions()
        {
            var mOptions = configOptions;
            ConfigOption it;

            // check if video mode is selected
            if (!mOptions.TryGetValue("Video Mode", out it))
                return "A video mode must be selected.";

            var foundDriver = false;
		    if (mOptions.TryGetValue( "Rendering Device", out it ))
		    {
		        var name = it.Value;
		        foundDriver = Direct3DDrivers.Any( d => d.DriverDescription == name );
		    }

            if (!foundDriver)
            {
                // Just pick the first driver
                SetConfigOption( "Rendering Device", _driverList.First().DriverDescription );
                return "Your DirectX driver name has changed since the last time you ran OGRE; " +
                       "the 'Rendering Device' has been changed.";
            }

            it = mOptions["VSync"];
		    vSync = it.Value == "Yes";

		    return "";
	    }

        #endregion

        #region DepthBufferCheckEnabled

        [OgreVersion(1, 7, 2790)]
        public override bool DepthBufferCheckEnabled
		{
			set
			{
				if ( value )
				{
					// use w-buffer if available
                    if (useWBuffer && (_deviceManager.ActiveDevice.D3D9DeviceCaps.RasterCaps & RasterCaps.WBuffer) == RasterCaps.WBuffer)
                        SetRenderState( RenderState.ZEnable, (int)ZBufferType.UseWBuffer );
                    else
                        SetRenderState( RenderState.ZEnable, (int)ZBufferType.UseZBuffer );
				}
				else
					SetRenderState( RenderState.ZEnable, (int)ZBufferType.DontUseZBuffer );
			}
		}

        #endregion

        #region DepthBufferFunction

        [OgreVersion(1, 7, 2790)]
        public override CompareFunction DepthBufferFunction
		{
			set
			{
				SetRenderState( RenderState.ZFunc, (int)D3DHelper.ConvertEnum( value ) );
			}
		}

        #endregion

        #region DepthBufferWriteEnabled

        [OgreVersion(1, 7, 2790)]
        public override bool DepthBufferWriteEnabled
		{
			set
			{
				SetRenderState( RenderState.ZWriteEnable, value );
			}
		}

        #endregion

        #region HorizontalTexelOffset

        [OgreVersion(1, 7, 2790)]
		public override Real HorizontalTexelOffset
		{
			get
			{
				// D3D considers the origin to be in the center of a pixel
				return -0.5f;
			}
		}

        #endregion

        #region VerticalTexelOffset

        [OgreVersion(1, 7, 2790)]
		public override Real VerticalTexelOffset
		{
			get
			{
				// D3D considers the origin to be in the center of a pixel
				return -0.5f;
			}
		}

        #endregion

        #region Private methods

        /// <summary>
		///		Sets up a light in D3D.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="light"></param>
		private void SetD3DLight( int index, Axiom.Core.Light light )
		{
			if ( light == null )
			{
				ActiveD3D9Device.EnableLight( index, false );
			}
			else
			{
                var nlight = new D3D.Light();

				switch ( light.Type )
				{
					case LightType.Point:
						nlight.Type = D3D.LightType.Point;
						break;

					case LightType.Directional:
						nlight.Type = D3D.LightType.Directional;
						break;

					case LightType.Spotlight:
						nlight.Type = D3D.LightType.Spot;
						nlight.Falloff = light.SpotlightFalloff;
						nlight.Theta = Utility.DegreesToRadians( light.SpotlightInnerAngle );
						nlight.Phi = Utility.DegreesToRadians( light.SpotlightOuterAngle );
						break;
				} // switch

				// light colors
				nlight.Diffuse = D3DHelper.ToColor( light.Diffuse );

				nlight.Specular = D3DHelper.ToColor( light.Specular );

				Vector3 vec;

				if ( light.Type != LightType.Directional )
				{
					vec = light.DerivedPosition;
					nlight.Position = new DX.Vector3( vec.x, vec.y, vec.z );
				}

				if ( light.Type != LightType.Point )
				{
					vec = light.DerivedDirection;
					nlight.Direction = new DX.Vector3( vec.x, vec.y, vec.z );
				}

				// atenuation settings
				nlight.Range = light.AttenuationRange;
				nlight.Attenuation0 = light.AttenuationConstant;
				nlight.Attenuation1 = light.AttenuationLinear;
			    nlight.Attenuation2 = light.AttenuationQuadratic;

				ActiveD3D9Device.SetLight( index, nlight );
                ActiveD3D9Device.EnableLight( index, true );
			} // if
		}

        [OgreVersion(1, 7, 2790)]
        [AxiomHelper(0, 8, "Using Axiom options, change handler see below at _configOptionChanged")]
		public override void SetConfigOption( string name, string value )
		{
			if ( ConfigOptions.ContainsKey( name ) )
				ConfigOptions[ name ].Value = value;
		}

        [AxiomHelper(0, 8, "Needs update to 1.7")]
		private void _configOptionChanged( string name, string value )
		{
			LogManager.Instance.Write( "D3D9 : RenderSystem Option: {0} = {1}", name, value );

			bool viewModeChanged = false;

			// Find option
			ConfigOption opt = ConfigOptions[ name ];

			// Refresh other options if D3DDriver changed
			if ( name == "Rendering Device" )
				_refreshD3DSettings();

			if ( name == "Full Screen" )
			{
				// Video mode is applicable
				opt = ConfigOptions[ "Video Mode" ];
				if ( opt.Value == "" )
				{
					opt.Value = "800 x 600 @ 32-bit colour";
					viewModeChanged = true;
				}
			}

			if ( name == "Anti aliasing" )
			{
				if ( value == "None" )
				{
					_setFSAA( D3D.MultisampleType.None, 0 );
				}
				else
				{
					D3D.MultisampleType fsaa = D3D.MultisampleType.None;
					int level = 0;

					if ( value.StartsWith( "NonMaskable" ) )
					{
						fsaa = D3D.MultisampleType.NonMaskable;
						level = Int32.Parse( value.Substring( value.LastIndexOf( " " ) ) );
						level -= 1;
					}
					else if ( value.StartsWith( "Level" ) )
					{
						fsaa = (D3D.MultisampleType)Int32.Parse( value.Substring( value.LastIndexOf( " " ) ) );
					}

					_setFSAA( fsaa, level );
				}
			}

			if ( name == "VSync" )
			{
				_vSync = ( value == "Yes" );
			}

			if ( name == "Allow NVPerfHUD" )
			{
				_useNVPerfHUD = ( value == "Yes" );
			}

			if ( viewModeChanged || name == "Video Mode" )
			{
				_refreshFSAAOptions();
			}
		}

		private void _setFSAA( D3D.MultisampleType fsaa, int level )
		{
            // this is a temporarily hack!!
            // gotta upgrade this to 1.7
            try
            {
                if (ActiveD3D9Device != null)
                    return;
            } catch(AxiomException)
		    {
				_fsaaType = fsaa;
				_fsaaQuality = level;
			}
		}

		/// <summary>
		///		Called in constructor to init configuration.
		/// </summary>
		private void InitConfigOptions()
		{
			ConfigOption optDevice = new ConfigOption( "Rendering Device", "", false );

			ConfigOption optVideoMode = new ConfigOption( "Video Mode", "800 x 600 @ 32-bit color", false );

			ConfigOption optFullScreen = new ConfigOption( "Full Screen", "No", false );

			ConfigOption optVSync = new ConfigOption( "VSync", "No", false );

			ConfigOption optAA = new ConfigOption( "Anti aliasing", "None", false );

			ConfigOption optFPUMode = new ConfigOption( "Floating-point mode", "Fastest", false );

			ConfigOption optNVPerfHUD = new ConfigOption( "Allow NVPerfHUD", "No", false );

			DriverCollection driverList = D3DHelper.GetDriverInfo( manager );
			foreach ( Driver driver in driverList )
			{
                optDevice.PossibleValues.Add(driver.AdapterNumber, driver.DriverDescription);
			}
            optDevice.Value = driverList[0].DriverDescription;

			optFullScreen.PossibleValues.Add( 0, "Yes" );
			optFullScreen.PossibleValues.Add( 1, "No" );

			optVSync.PossibleValues.Add( 0, "Yes" );
			optVSync.PossibleValues.Add( 1, "No" );

			optAA.PossibleValues.Add( 0, "None" );

			optFPUMode.PossibleValues.Clear();
			optFPUMode.PossibleValues.Add( 0, "Fastest" );
			optFPUMode.PossibleValues.Add( 1, "Consistent" );

			optNVPerfHUD.PossibleValues.Add( 0, "Yes" );
			optNVPerfHUD.PossibleValues.Add( 1, "No" );

			optFPUMode.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optAA.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optVSync.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optFullScreen.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optVideoMode.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optDevice.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );
			optNVPerfHUD.ConfigValueChanged += new ConfigOption.ValueChanged( _configOptionChanged );

			ConfigOptions.Add( optDevice );
			ConfigOptions.Add( optVideoMode );
			ConfigOptions.Add( optFullScreen );
			ConfigOptions.Add( optVSync );
			ConfigOptions.Add( optAA );
			ConfigOptions.Add( optFPUMode );
			ConfigOptions.Add( optNVPerfHUD );

			_refreshD3DSettings();
		}

		private void _refreshD3DSettings()
		{
			DriverCollection drivers = D3DHelper.GetDriverInfo( manager );

			ConfigOption optDevice = ConfigOptions[ "Rendering Device" ];
			Driver driver = drivers[ optDevice.Value ];
			if ( driver != null )
			{
				// Get Current Selection
				ConfigOption optVideoMode = ConfigOptions[ "Video Mode" ];
				string curMode = optVideoMode.Value;

				// Clear previous Modes
				optVideoMode.PossibleValues.Clear();

				// Get Video Modes for current device;
				foreach ( var videoMode in driver.VideoModeList )
				{
					optVideoMode.PossibleValues.Add( optVideoMode.PossibleValues.Count, videoMode.ToString() );
				}

				// Reset video mode to default if previous doesn't avail in new possible values

				if ( optVideoMode.PossibleValues.Values.Contains( curMode ) == false )
				{
					optVideoMode.Value = "800 x 600 @ 32-bit color";
				}

				// Also refresh FSAA options
				_refreshFSAAOptions();
			}
		}

		private void _refreshFSAAOptions()
		{
			// Reset FSAA Options
			ConfigOption optFSAA = ConfigOptions[ "Anti aliasing" ];
			string curFSAA = optFSAA.Value;
			optFSAA.PossibleValues.Clear();
			optFSAA.PossibleValues.Add( 0, "None" );

			ConfigOption optFullScreen = ConfigOptions[ "Full Screen" ];
			bool windowed = optFullScreen.Value != "Yes";

			DriverCollection drivers = D3DHelper.GetDriverInfo( manager );
			ConfigOption optDevice = ConfigOptions[ "Rendering Device" ];
			Driver driver = drivers[ optDevice.Value ];
			if ( driver != null )
			{
				ConfigOption optVideoMode = ConfigOptions[ "Video Mode" ];
				var videoMode = driver.VideoModeList[ optVideoMode.Value ];
				if ( videoMode != null )
				{
					int numLevels = 0;
					SlimDX.Result result;

					// get non maskable levels supported for this VMODE
					manager.CheckDeviceMultisampleType( driver.AdapterNumber, D3D.DeviceType.Hardware, videoMode.Format, windowed, D3D.MultisampleType.NonMaskable, out numLevels, out result );
					for ( int n = 0; n < numLevels; n++ )
					{
						optFSAA.PossibleValues.Add( optFSAA.PossibleValues.Count, String.Format( "NonMaskable {0}", n ) );
					}

					// get maskable levels supported for this VMODE
					for ( int n = 2; n < 17; n++ )
					{
						if ( manager.CheckDeviceMultisampleType( driver.AdapterNumber, D3D.DeviceType.Hardware, videoMode.Format, windowed, (D3D.MultisampleType)n ) )
						{
							optFSAA.PossibleValues.Add( optFSAA.PossibleValues.Count, String.Format( "Level {0}", n ) );
						}
					}
				}
			}

			// Reset FSAA to none if previous doesn't avail in new possible values
			if ( optFSAA.PossibleValues.Values.Contains( curFSAA ) == false )
			{
				optFSAA.Value = "None";
			}
		}



		#endregion Private methods

        #region SetDepthBufferParams

        [OgreVersion(1, 7, 2790)]
		public override void SetDepthBufferParams( bool depthTest, bool depthWrite, CompareFunction depthFunction )
		{
            DepthBufferCheckEnabled = depthTest;
			DepthBufferWriteEnabled = depthWrite;
			DepthBufferFunction = depthFunction;
		}

        [OgreVersion(1, 7, 2790)]
		public override void SetStencilBufferParams( CompareFunction function = CompareFunction.AlwaysPass, 
            int refValue = 0, int mask = -1, 
            StencilOperation stencilFailOp = StencilOperation.Keep, StencilOperation depthFailOp = StencilOperation.Keep, 
            StencilOperation passOp = StencilOperation.Keep, bool twoSidedOperation = false )
		{
		    bool flip;

			// 2 sided operation?
			if ( twoSidedOperation )
			{
                if (!currentCapabilities.HasCapability(Graphics.Capabilities.TwoSidedStencil))
				{
					throw new AxiomException( "2-sided stencils are not supported on this hardware!" );
				}

				SetRenderState( RenderState.TwoSidedStencilMode, true );

                // NB: We should always treat CCW as front face for consistent with default
                // culling mode. Therefore, we must take care with two-sided stencil settings.
                flip = (invertVertexWinding && activeRenderTarget.RequiresTextureFlipping) ||
                    (!invertVertexWinding && !activeRenderTarget.RequiresTextureFlipping);

			    SetRenderState( RenderState.CcwStencilFail, (int)D3DHelper.ConvertEnum( stencilFailOp, !flip ) );
			    SetRenderState( RenderState.CcwStencilZFail, (int)D3DHelper.ConvertEnum( depthFailOp, !flip ) );
			    SetRenderState( RenderState.CcwStencilPass, (int)D3DHelper.ConvertEnum( passOp, !flip ) );
			}
			else
			{
				SetRenderState( RenderState.TwoSidedStencilMode, false );
			    flip = false;
			}

			// configure standard version of the stencil operations
			SetRenderState( RenderState.StencilFunc, (int)D3DHelper.ConvertEnum( function ) );
			SetRenderState( RenderState.StencilRef, refValue );
			SetRenderState( RenderState.StencilMask, mask );
		    SetRenderState( RenderState.StencilFail, (int)D3DHelper.ConvertEnum( stencilFailOp, flip ) );
		    SetRenderState( RenderState.StencilZFail, (int)D3DHelper.ConvertEnum( depthFailOp, flip ) );
		    SetRenderState( RenderState.StencilPass, (int)D3DHelper.ConvertEnum( passOp, flip ) );
		}

        #endregion

        #region SetSurfaceParams

        [OgreVersion(1, 7, 2790)]
        public override void SetSurfaceParams(ColorEx ambient, ColorEx diffuse, ColorEx specular,
            ColorEx emissive, Real shininess, TrackVertexColor tracking = TrackVertexColor.None)
		{
			// TODO: Cache color values to prune unneccessary setting

			// create a new material based on the supplied params
			var mat = new D3D.Material();
			mat.Diffuse = D3DHelper.ToColor( diffuse );
			mat.Ambient = D3DHelper.ToColor( ambient );
			mat.Specular = D3DHelper.ToColor( specular );
			mat.Emissive = D3DHelper.ToColor( emissive );
			mat.Power = shininess;

			// set the current material
			ActiveD3D9Device.Material = mat;

			if ( tracking != TrackVertexColor.None )
			{
				SetRenderState( RenderState.ColorVertex, true );
				SetRenderState( RenderState.AmbientMaterialSource, (int)( ( ( tracking & TrackVertexColor.Ambient ) != 0 ) ? ColorSource.Color1 : ColorSource.Material ) );
				SetRenderState( RenderState.DiffuseMaterialSource, (int)( ( ( tracking & TrackVertexColor.Diffuse ) != 0 ) ? ColorSource.Color1 : ColorSource.Material ) );
				SetRenderState( RenderState.SpecularMaterialSource, (int)( ( ( tracking & TrackVertexColor.Specular ) != 0 ) ? ColorSource.Color1 : ColorSource.Material ) );
				SetRenderState( RenderState.EmissiveMaterialSource, (int)( ( ( tracking & TrackVertexColor.Emissive ) != 0 ) ? ColorSource.Color1 : ColorSource.Material ) );
			}
			else
			{
				SetRenderState( RenderState.ColorVertex, false );
			}
		}

        #endregion

        #region RenderTarget

        [OgreVersion(1, 7, 2790)]
        public override RenderTarget RenderTarget
        {
            set
            {
                activeRenderTarget = value;
                if (activeRenderTarget != null)
                {
                    // If this is called without going through RenderWindow::update, then 
                    // the device will not have been set. Calling it twice is safe, the 
                    // implementation ensures nothing happens if the same device is set twice
                    if (_renderWindows.Cast<RenderTarget>().Contains(value))
                    {
                        var window = (D3DRenderWindow)value;
                        _deviceManager.ActiveRenderTargetDevice = window.Device;
                        // also make sure we validate the device; if this never went 
                        // through update() it won't be set
                        window.ValidateDevice();
                    }

                    // Retrieve render surfaces (up to OGRE_MAX_MULTIPLE_RENDER_TARGETS)
			        var pBack = (Surface[])value["DDBACKBUFFER"];
			        if (pBack[0] == null)
				        return;

                    var depthBuffer = (D3D9DepthBuffer)value.DepthBuffer;

			        if( value.DepthBufferPool != Graphics.PoolId.NoDepth &&
				        (depthBuffer == null || depthBuffer.DeviceCreator != ActiveD3D9Device ) )
			        {
				        //Depth is automatically managed and there is no depth buffer attached to this RT
				        //or the Current D3D device doesn't match the one this Depth buffer was created
			            SetDepthBufferFor( value );
				        
				        //Retrieve depth buffer again
				        depthBuffer = (D3D9DepthBuffer)value.DepthBuffer;
			        }

			        if ((depthBuffer != null) && ( depthBuffer.DeviceCreator != ActiveD3D9Device ))
			        {
			            throw new AxiomException("Can't use a depth buffer from a different device!");
			        }

			        var depthSurface = depthBuffer != null ? depthBuffer.DepthBufferSurface : null;

                    // Bind render targets
			        var count = currentCapabilities.MultiRenderTargetCount;
			        for (var x=0; x<count; ++x)
			        {
				        var hr = ActiveD3D9Device.SetRenderTarget(x, pBack[x]);
				        if (hr.IsFailure)
				        {
				            throw new AxiomException( "Failed to setRenderTarget : {0}", hr.Description );
				        }
			        }
			        ActiveD3D9Device.DepthStencilSurface = depthSurface;
                }
            }
        }

        #endregion

        #region PointSpritesEnabled

        [OgreVersion(1, 7, 2790)]
		public override bool PointSpritesEnabled
		{
			set
			{
				SetRenderState( RenderState.PointSpriteEnable, value );
			}
		}

        #endregion

        internal D3D9RenderWindowList renderWindows = new D3D9RenderWindowList();

        #region Direct3DDrivers

        private D3D9DriverList _driverList;

        [OgreVersion(1, 7, 2790, "Replaces ResourceCreationDevice")]
        public static IEnumerable<Device> ResourceCreationDevices
        {
            get
            {
                var creationPolicy = ResourceManager.CreationPolicy;

                if ( creationPolicy == D3D9ResourceManager.ResourceCreationPolicy.CreateOnActiveDevice )
                {
                    yield return ActiveD3D9Device;
                }
                else if ( creationPolicy == D3D9ResourceManager.ResourceCreationPolicy.CreateOnAllDevices )
                {
                    foreach ( var dev in _D3D9RenderSystem._deviceManager.Select( x => x.D3DDevice ) )
                        yield return dev;
                }
                else
                {
                    throw new AxiomException( "Invalid resource creation policy !!!" );
                }
            }
        }

	    public D3D9DriverList Direct3DDrivers
	    {
	        get
	        {
	            return _driverList ?? ( _driverList = new D3D9DriverList() );
	        }
	    }

        #endregion

        #region SetRenderState

        /// <summary>
        /// Sets the given renderstate to a new value
        /// </summary>
        /// <param name="state">The state to set</param>
        /// <param name="val">The value to set</param>
        [OgreVersion(1, 7, 2790, "returns HRESULT in Ogre")]
        [AxiomHelper(0, 8, "convenience overload")]
		private void SetRenderState( RenderState state, bool val )
		{
            var oldVal = ActiveD3D9Device.GetRenderState<bool>(state);
			if ( oldVal != val )
                ActiveD3D9Device.SetRenderState(state, val);
		}

        /// <summary>
        /// Sets the given renderstate to a new value
        /// </summary>
        /// <param name="state">The state to set</param>
        /// <param name="val">The value to set</param>
        [OgreVersion(1, 7, 2790, "returns HRESULT in Ogre")]
        private void SetRenderState( RenderState state, int val )
		{
            var oldVal = ActiveD3D9Device.GetRenderState<int>(state);
			if ( oldVal != val )
                ActiveD3D9Device.SetRenderState(state, val);
		}


        /// <summary>
        /// Sets the given renderstate to a new value
        /// </summary>
        /// <param name="state">The state to set</param>
        /// <param name="val">The value to set</param>
        [OgreVersion(1, 7, 2790, "returns HRESULT in Ogre")]
        [AxiomHelper(0, 8, "convenience overload")]
        private void SetFloatRenderState( RenderState state, float val )
		{
            var oldVal = ActiveD3D9Device.GetRenderState<float>(state);
			if ( oldVal != val )
                ActiveD3D9Device.SetRenderState(state, val);
		}


        /// <summary>
        /// Sets the given renderstate to a new value
        /// </summary>
        /// <param name="state">The state to set</param>
        /// <param name="val">The value to set</param>
        [OgreVersion(1, 7, 2790, "returns HRESULT in Ogre")]
        [AxiomHelper(0, 8, "convenience overload")]
        private void SetRenderState( RenderState state, System.Drawing.Color val )
		{
            var oldVal = System.Drawing.Color.FromArgb(ActiveD3D9Device.GetRenderState<int>(state));
			if ( oldVal != val )
                ActiveD3D9Device.SetRenderState(state, val.ToArgb());
		}

        /// <summary>
        /// Sets the given renderstate to a new value
        /// </summary>
        /// <param name="state">The state to set</param>
        /// <param name="val">The value to set</param>
        [OgreVersion(1, 7, 2790, "returns HRESULT in Ogre")]
        [AxiomHelper(0, 8, "convenience overload")]
        private void SetRenderState(RenderState state, Color4 val)
        {
            var oldVal = ActiveD3D9Device.GetRenderState<Color4>(state);
            if (oldVal != val)
                ActiveD3D9Device.SetRenderState(state, val.ToArgb());
        }


        #endregion

        #region SetTextureAddressingMode

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureAddressingMode( int stage, UVWAddressing uvw )
		{
			// set the device sampler states accordingly
            SetSamplerState( GetSamplerId( stage ), SamplerState.AddressU, (int)D3DHelper.ConvertEnum( uvw.U ) );
            SetSamplerState( GetSamplerId( stage ), SamplerState.AddressV, (int)D3DHelper.ConvertEnum( uvw.V ) );
            SetSamplerState( GetSamplerId( stage ), SamplerState.AddressW, (int)D3DHelper.ConvertEnum( uvw.W ) );
		}

        #endregion

        #region SetTextureBorderColor

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureBorderColor( int stage, ColorEx borderColor )
        {
            SetSamplerState( GetSamplerId( stage ), SamplerState.BorderColor, D3DHelper.ToColor( borderColor ).ToArgb() );
        }

        #endregion

        #region SetTextureMipmapBias

        [OgreVersion(1, 7, 2790)]
        public override void SetTextureMipmapBias(int unit, float bias)
        {
            if (currentCapabilities.HasCapability(Graphics.Capabilities.MipmapLODBias))
            {
                // ugh - have to pass float data through DWORD with no conversion
                unsafe
                {
                    var b = &bias;
                    var dw = (int*)b;
                    SetSamplerState( GetSamplerId( unit ), SamplerState.MipMapLodBias, *dw );
                }
            }
        }

        #endregion

        private unsafe int FLOAT2DWORD(float f)
        {
            return *(int*)&f;
        }

	    #region SetTextureBlendMode

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureBlendMode( int stage, LayerBlendModeEx bm )
        {
            TextureStage tss;
            ColorEx manualD3D;
			// choose type of blend.
		    if( bm.blendType == LayerBlendType.Color)
			    tss = TextureStage.ColorOperation;
		    else if( bm.blendType == LayerBlendType.Alpha )
			    tss = TextureStage.AlphaOperation;
		    else
		        throw new AxiomException("Invalid blend type");

            // set manual factor if required by operation
		    if (bm.operation == LayerBlendOperationEx.BlendManual)
		    {
		        SetRenderState( RenderState.TextureFactor, new Color4( bm.blendFactor, 0.0f, 0.0f, 0.0f ) );
		    }

		    // set operation  
		    SetTextureStageState( stage, tss, D3DHelper.ConvertEnum(bm.operation, _deviceManager.ActiveDevice.D3D9DeviceCaps) );

            // choose source 1
		    if( bm.blendType == LayerBlendType.Color)
		    {
			    tss = TextureStage.ColorArg1;
                manualD3D = bm.colorArg1;
		        manualBlendColors[ stage, 0 ] = manualD3D;
		    }
		    else if( bm.blendType == LayerBlendType.Alpha )
		    {
			    tss = TextureStage.AlphaArg1;
		        manualD3D = manualBlendColors[ stage, 0 ];
                manualD3D.a = bm.alphaArg1;
		    }
		    else
		    {
		        throw new AxiomException("Invalid blend type");
		    }

            // Set manual factor if required
		    if (bm.source1 == LayerBlendSource.Manual)
		    {
			    if (currentCapabilities.HasCapability(Graphics.Capabilities.PerStageConstant))
			    {
				    // Per-stage state
				    SetTextureStageState(stage, TextureStage.Constant, manualD3D.ToARGB());
			    }
			    else
			    {
				    // Global state
				    SetRenderState( RenderState.TextureFactor, manualD3D.ToARGB() );
			    }
		    }
		    // set source 1
            SetTextureStageState(stage, tss, D3DHelper.ConvertEnum(bm.source1, currentCapabilities.HasCapability(Graphics.Capabilities.PerStageConstant)));

            // choose source 2
            if (bm.blendType == LayerBlendType.Color)
            {
                tss = TextureStage.ColorArg2;
                manualD3D = bm.colorArg2;
                manualBlendColors[stage, 1] = manualD3D;
            }
            else if (bm.blendType == LayerBlendType.Alpha)
            {
                tss = TextureStage.AlphaArg2;
                manualD3D = manualBlendColors[ stage, 1 ];
                manualD3D.a = bm.alphaArg2;
            }
            // Set manual factor if required
		    if (bm.source2 == LayerBlendSource.Manual)
		    {
			    if (currentCapabilities.HasCapability(Graphics.Capabilities.PerStageConstant))
			    {
				    // Per-stage state
				    SetTextureStageState(stage, TextureStage.Constant, manualD3D.ToARGB());
			    }
			    else
			    {
				    SetRenderState( RenderState.TextureFactor, manualD3D.ToARGB() );
			    }
		    }

            // Now set source 2
		    SetTextureStageState( stage, tss,D3DHelper.ConvertEnum(bm.source2, currentCapabilities.HasCapability(Graphics.Capabilities.PerStageConstant)) );
		    
            // Set interpolation factor if lerping
		    if (bm.operation == LayerBlendOperationEx.BlendDiffuseColor && 
			    (_deviceManager.ActiveDevice.D3D9DeviceCaps.TextureOperationCaps & TextureOperationCaps.Lerp) != 0)
		    {
			    // choose source 0 (lerp factor)
			    if( bm.blendType == LayerBlendType.Color)
			    {
				    tss = TextureStage.ColorArg0;
			    }
			    else if( bm.blendType == LayerBlendType.Alpha )
			    {
				    tss = TextureStage.AlphaArg0;
			    }
			    SetTextureStageState(stage, tss, TextureArgument.Diffuse);
		    }
		}

        #endregion

        #region SetTextureStageState

        private void SetTextureStageState( int stage, TextureStage type, int value )
	    {
            // can only set fixed-function texture stage state
            if (stage < 8)
            {
                var oldVal = ActiveD3D9Device.GetTextureStageState( stage, type );
                if (oldVal == value)
                    return;
                
                ActiveD3D9Device.SetTextureStageState(stage, type, value);
            }
	    }

        private void SetTextureStageState(int stage, TextureStage type, TextureArgument value)
	    {
            SetTextureStageState(stage, type, (int)value);
	    }

        private void SetTextureStageState(int stage, TextureStage type, TextureOperation value)
	    {
            SetTextureStageState(stage, type, (int)value);
	    }

	    #endregion

        #region SetTextureCoordSet

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureCoordSet( int stage, int index )
		{
            // if vertex shader is being used, stage and index must match
            if (vertexProgramBound)
                index = stage;

            // Record settings
			texStageDesc[ stage ].coordIndex = index;
            SetTextureStageState( stage, TextureStage.TexCoordIndex,
                                  ( D3DHelper.ConvertEnum( texStageDesc[ stage ].autoTexCoordType,
                                                           _deviceManager.ActiveDevice.D3D9DeviceCaps ) | index ) );
		}

        #endregion

        #region SetTextureUnitFiltering

        [OgreVersion(1, 7, 2790)]
		public override void SetTextureUnitFiltering( int stage, FilterType type, FilterOptions filter )
		{
			var texType = texStageDesc[ stage ].texType;
            var texFilter = D3DHelper.ConvertEnum( type, filter, _deviceManager.ActiveDevice.D3D9DeviceCaps, texType );

            SetSamplerState(GetSamplerId(stage), D3DHelper.ConvertEnum(type), (int)texFilter);
		}

        #endregion

        #region SetClipPlanesImpl

        protected override void SetClipPlanesImpl(Math.Collections.PlaneList planes)
        {
            for (var i = 0; i < planes.Count; i++)
            {
                var p = planes[ i ];
                var plane = new DX.Plane(p.Normal.x, p.Normal.y, p.Normal.z, p.D);

                if (vertexProgramBound)
                {
                    // programmable clips in clip space (ugh)
                    // must transform worldspace planes by view/proj
                    throw new NotImplementedException();
                }

                ActiveD3D9Device.SetClipPlane(i, plane);
            }
            var bits = ( 1ul << ( planes.Count + 1 ) ) - 1;
            SetRenderState(RenderState.ClipPlaneEnable, (int)bits);
        }

        #endregion

        #region SetClipPlane

        /// <summary>
        /// </summary>
        [OgreVersion(1, 7, 2790, "D3D Rendersystem utility func")]
        public void SetClipPlane(ushort index, Real a, Real b, Real c, Real d)
        {
            ActiveD3D9Device.SetClipPlane( index, new SlimDX.Plane( a, b, c, d ) );
        }

        #endregion

        #region EnableClipPlane

        /// <summary>
        /// </summary>
        [OgreVersion(1, 7, 2790, "D3D Rendersystem utility func")]
        public void EnableClipPlane (ushort index, bool enable)
        {
            var prev = ActiveD3D9Device.GetRenderState<int>( RenderState.ClipPlaneEnable );
            SetRenderState( RenderState.ClipPlaneEnable, enable ? ( prev | ( 1 << index ) ) : ( prev & ~( 1 << index ) ) );
	    }

        #endregion

        #region SetScissorTest

        [OgreVersion(1, 7, 2790)]
		public override void SetScissorTest( bool enable, int left, int top, int right, int bottom )
		{
			if ( enable )
			{
                SetRenderState(RenderState.ScissorTestEnable, true);
				ActiveD3D9Device.ScissorRect = new System.Drawing.Rectangle( left, top, right - left, bottom - top );
			}
			else
			{
				SetRenderState( RenderState.ScissorTestEnable, false );
			}
		}

        #endregion

        private void _cleanupDepthStencils()
		{
			foreach ( D3D.Surface surface in zBufferCache.Values )
			{
				/// Release buffer
				surface.Dispose();
			}
			zBufferCache.Clear();
		}

		public void RestoreLostDevice()
		{
			// Release all non-managed resources

			// Cleanup depth stencils
			_cleanupDepthStencils();

			// Set all texture units to nothing
			DisableTextureUnitsFrom( 0 );

			// Unbind any vertex streams
			for ( int i = 0; i < _lastVertexSourceCount; ++i )
			{
				ActiveD3D9Device.SetStreamSource( i, null, 0, 0 );
			}
			_lastVertexSourceCount = 0;

			// Release all automatic temporary buffers and free unused
			// temporary buffers, so we doesn't need to recreate them,
			// and they will reallocate on demand. This saves a lot of
			// release/recreate of non-managed vertex buffers which
			// wasn't need at all.
			hardwareBufferManager.ReleaseBufferCopies( true );

			// We have to deal with non-managed textures and vertex buffers
			// GPU programs don't have to be restored
			( (D3DTextureManager)textureManager ).ReleaseDefaultPoolResources();
			( (D3DHardwareBufferManager)hardwareBufferManager ).ReleaseDefaultPoolResources();

			// release additional swap chains (secondary windows)
			foreach ( var sw in _secondaryWindows )
			{
				sw.DisposeD3DResources();
			}

			// Reset the device, using the primary window presentation params
			try
			{
				SlimDX.Result result = ActiveD3D9Device.Reset( _primaryWindow.PresentationParameters );
				if ( result.Code == D3D.ResultCode.DeviceLost.Code )
					return;
			}
			catch ( SlimDX.SlimDXException dlx )
			{
				LogManager.Instance.Write( "[Error] Received error while trying to restore the device." );
				LogManager.Instance.Write( LogManager.BuildExceptionString( dlx ) );
				return;
			}
			catch ( Exception ex )
			{
				throw new AxiomException( "Cannot reset device!", ex );
			}

			// will have lost basic states
			_basicStatesInitialized = false;
			vertexProgramBound = false;
			fragmentProgramBound = false;

			// recreate additional swap chains
			foreach ( D3DRenderWindow sw in _secondaryWindows )
			{
				//sw.CreateD3DResources();
			}

			// Recreate all non-managed resources
			( (D3DTextureManager)textureManager ).RecreateDefaultPoolResources();
			( (D3DHardwareBufferManager)hardwareBufferManager ).RecreateDefaultPoolResources();

			LogManager.Instance.Write( "!!! Direct3D Device successfully restored." );

			_deviceLost = false;

			//device.SetRenderState( D3D.RenderState.Clipping, true );

			//TODO fireEvent("DeviceRestored");
		}

        /// <summary>
        /// This function is meant to add Depth Buffers to the pool that aren't released when the DepthBuffer
        /// is deleted. This is specially useful to put the Depth Buffer created along with the window's
        /// back buffer into the pool. All depth buffers introduced with this method go to POOL_DEFAULT
        /// </summary>
        public DepthBuffer AddManualDepthBuffer(Device depthSurfaceDevice, Surface depthSurface)
	    {
	        //If this depth buffer was already added, return that one
		
            foreach( var itor in depthBufferPool[PoolId.Default] )
            {
                if( ((D3D9DepthBuffer)itor).DepthBufferSurface == depthSurface )
				return itor;
            }

		    //Nope, get the info about this depth buffer and create a new container fot it
            var dsDesc = depthSurface.Description;
		
		    var newDepthBuffer = new D3D9DepthBuffer( PoolId.Default, this,
												depthSurfaceDevice, depthSurface,
												dsDesc.Format, dsDesc.Width, dsDesc.Height,
												dsDesc.MultisampleType, dsDesc.MultisampleQuality, true );

		    //Add the 'main' depth buffer to the pool
            depthBufferPool[newDepthBuffer.PoolId].Add(newDepthBuffer);

		    return newDepthBuffer;
	    }
	}

    /// <summary>
	///		Structure holding texture unit settings for every stage
	/// </summary>
	internal struct D3DTextureStageDesc
	{
		/// the type of the texture
		public D3DTextureType texType;
		/// which texCoordIndex to use
		public int coordIndex;
		/// type of auto tex. calc. used
		public TexCoordCalcMethod autoTexCoordType;
		/// Frustum, used if the above is projection
		public Frustum frustum;
		/// texture
		public BaseTexture tex;
		/// vertex texture
		public BaseTexture vertexTex;
	}

	/// <summary>
	///	D3D texture types
	/// </summary>
	public enum D3DTextureType
	{
		Normal,
		Cube,
		Volume,
		None
	}
}