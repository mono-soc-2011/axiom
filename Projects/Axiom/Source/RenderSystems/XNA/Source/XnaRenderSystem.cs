#region LGPL License
/*
Axiom Graphics Engine Library
Copyright (C) 2003-2007  Axiom Project Team

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
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <id value="$Id$"/>
// </file>
#endregion SVN Version Information

#region Namespace Declarations

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
#if !(XBOX || XBOX360 || SILVERLIGHT )
using System.Windows.Forms;
#endif
using Axiom.Core;
using Axiom.Configuration;
using Axiom.Graphics;
using Axiom.Math;
using Axiom.Overlays;
using XNA = Microsoft.Xna.Framework;
using XFG = Microsoft.Xna.Framework.Graphics;
using Axiom.Collections;

#endregion Namespace Declarations

namespace Axiom.RenderSystems.Xna
{
    public class XnaRenderSystem : RenderSystem, IServiceProvider
    {
        public static readonly Matrix4 ProjectionClipSpace2DToImageSpacePerspective = new Matrix4(
           0.5f, 0, 0, -0.5f,
           0, -0.5f, 0, -0.5f,
           0, 0, 0, 1f,
           0, 0, 0, 1f );

        public static readonly Matrix4 ProjectionClipSpace2DToImageSpaceOrtho = new Matrix4(
            -0.5f, 0, 0, -0.5f,
            0, 0.5f, 0, -0.5f,
            0, 0, 0, 1f,
            0, 0, 0, 1f );

        /// <summary>
        ///    Reference to the Xna device.
        /// </summary>
        private XFG.GraphicsDevice _device;
        private Driver _activeDriver;

        private XFG.GraphicsDeviceCapabilities _capabilities;
        /// Saved last view matrix
        protected Matrix4 _viewMatrix = Matrix4.Identity;
        bool _isFirstFrame = true;
        private int _primCount;
        private int _renderCount = 0;
        /// <summary>
        /// The one used to crfeate the device.
        /// </summary>
        private XnaRenderWindow _primaryWindow;

        List<XnaRenderWindow> _secondaryWindows = new List<XnaRenderWindow>();

        // stores texture stage info locally for convenience
        internal XnaTextureStageDescription[] texStageDesc = new XnaTextureStageDescription[ Config.MaxTextureLayers ];
        protected XnaGpuProgramManager gpuProgramMgr;
        int numLastStreams = 0;

        /// <summary>
        ///    Number of streams used last frame, used to unbind any buffers not used during the current operation.
        /// </summary>
        private int _lastVertexSourceCount;

        // Fixed Function Emulation
#if !(XBOX || XBOX360 || SILVERLIGHT)
        FixedFunctionEmulation.ShaderManager _shaderManager = new FixedFunctionEmulation.ShaderManager();
        FixedFunctionEmulation.HLSLShaderGenerator _hlslShaderGenerator = new FixedFunctionEmulation.HLSLShaderGenerator();
        FixedFunctionEmulation.FixedFunctionState _fixedFunctionState = new FixedFunctionEmulation.FixedFunctionState();
        FixedFunctionEmulation.HLSLFixedFunctionProgram _fixedFunctionProgram;//= new Axiom.RenderSystems.Xna.FixedFunctionEmulation.HLSLFixedFunctionProgram();
        FixedFunctionEmulation.FixedFunctionPrograms.FixedFunctionProgramsParameters _ffProgramParameters = new FixedFunctionEmulation.FixedFunctionPrograms.FixedFunctionProgramsParameters();
#endif


        protected int primCount;
        // protected int renderCount = 0;

        #region Construction and Destruction

        public XnaRenderSystem()
        {
            _initConfigOptions();
            // init the texture stage descriptions
            for ( int i = 0; i < Config.MaxTextureLayers; i++ )
            {
                texStageDesc[ i ].autoTexCoordType = TexCoordCalcMethod.None;
                texStageDesc[ i ].coordIndex = 0;
                texStageDesc[ i ].texType = TextureType.OneD;
                texStageDesc[ i ].tex = null;
            }
#if !(XBOX || XBOX360 || SILVERLIGHT)
            _shaderManager.RegisterGenerator( _hlslShaderGenerator );
#endif
        }

        #endregion Construction and Destruction

        #region Helper Methods

        /// <summary>
        /// 
        /// </summary>
        private void _setVertexBufferBinding(VertexBufferBinding binding)
        {
            Dictionary<short, HardwareVertexBuffer> bindings = binding.Bindings;

            // TODO: Optimize to remove enumeration if possible, although with so few iterations it may never make a difference
            foreach (short stream in bindings.Keys)
            {
                XnaHardwareVertexBuffer buffer = (XnaHardwareVertexBuffer)bindings[stream];

                _device.Vertices[stream].SetSource(buffer.XnaVertexBuffer, 0, buffer.VertexSize);

                _lastVertexSourceCount++;
            }

            // Unbind any unused sources
            for (int i = binding.BindingCount; i < _lastVertexSourceCount; i++)
            {
                _device.Vertices[i].SetSource(null, 0, 0);
            }

            _lastVertexSourceCount = binding.BindingCount;
        }

        /// <summary>
        ///		Helper method for setting the current vertex declaration.
        /// </summary>
        private void _setVertexDeclaration(VertexDeclaration decl)
        {
            // TODO: Check for duplicate setting and avoid setting if dupe
            XnaVertexDeclaration vertDecl = (XnaVertexDeclaration)decl;

            _device.VertexDeclaration = vertDecl.XnaVertexDecl;
        }

        private void _initConfigOptions()
        {
            ConfigOption optDevice = new ConfigOption( "Rendering Device", "", false );
            ConfigOption optVideoMode = new ConfigOption( "Video Mode", "800 x 600 @ 32-bit colour", false );
            ConfigOption optFullScreen = new ConfigOption( "Full Screen", "No", false );
            ConfigOption optVSync = new ConfigOption( "VSync", "No", false );
            ConfigOption optAA = new ConfigOption( "Anti aliasing", "None", false );
            ConfigOption optFPUMode = new ConfigOption( "Floating-point mode", "Fastest", false );

            optDevice.PossibleValues.Clear();
            DriverCollection driver = XnaHelper.GetDriverInfo();

            foreach ( VideoMode mode in driver[0].VideoModes )
            {
                string query = string.Format( "{0} x {1} @ {2}-bit colour", mode.Width, mode.Height, mode.ColorDepth.ToString() );
                // add a new row to the display settings table
                optVideoMode.PossibleValues.Add( query );
            }

            optFullScreen.PossibleValues.Add( "Yes" );
            optFullScreen.PossibleValues.Add( "No" );

            optVSync.PossibleValues.Add( "Yes" );
            optVSync.PossibleValues.Add( "No" );

            optAA.PossibleValues.Add( "None" );

            optFPUMode.PossibleValues.Clear();
            optFPUMode.PossibleValues.Add( "Fastest" );
            optFPUMode.PossibleValues.Add( "Consistent" );

            ConfigOptions.Add( optDevice );
            ConfigOptions.Add( optVideoMode );
            ConfigOptions.Add( optFullScreen );
            ConfigOptions.Add( optVSync );
            ConfigOptions.Add( optAA );
            ConfigOptions.Add( optFPUMode );
        }

#if !(XBOX || XBOX360 || SILVERLIGHT)
        /// <summary>
        ///		Creates a default form to use for a rendering target.
        /// </summary>
        /// <remarks>
        ///		This is used internally whenever <see cref="Initialize"/> is called and autoCreateWindow is set to true.
        /// </remarks>
        /// <param name="windowTitle">Title of the window.</param>
        /// <param name="top">Top position of the window.</param>
        /// <param name="left">Left position of the window.</param>
        /// <param name="width">Width of the window.</param>
        /// <param name="height">Height of the window</param>
        /// <param name="fullScreen">Prepare the form for fullscreen mode?</param>
        /// <returns>A form suitable for using as a rendering target.</returns>
        private DefaultForm _createDefaultForm( string windowTitle, int top, int left, int width, int height, bool fullScreen )
        {
            DefaultForm form = new DefaultForm();

            form.ClientSize = new System.Drawing.Size( width, height );
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.StartPosition = FormStartPosition.CenterScreen;

            if ( fullScreen )
            {
                form.Top = 0;
                form.Left = 0;
                form.FormBorderStyle = FormBorderStyle.None;
                form.WindowState = FormWindowState.Maximized;
                form.TopMost = true;
                form.TopLevel = true;
            }
            else
            {
                form.Top = top;
                form.Left = left;
                form.FormBorderStyle = FormBorderStyle.FixedSingle;
                form.WindowState = FormWindowState.Normal;
                form.Text = windowTitle;
            }

            return form;
        }
#endif


        /// <summary>
        ///		Helper method to go through and interrogate hardware capabilities.
        /// </summary>
        private void _checkHardwareCapabilities( XFG.GraphicsDevice device )
        {
            // get the number of possible texture units
            HardwareCapabilities.TextureUnitCount = _capabilities.MaxSimultaneousTextures;

            // max active lights
            HardwareCapabilities.MaxLights = 8; // d3dHardwareCapabilities.MaxActiveLights;

            XFG.DepthStencilBuffer surface = device.DepthStencilBuffer;
            //XNA.TextureInformation surfaceDesc = surface.Description;
            //surface.Dispose();

            if ( surface.Format == XFG.DepthFormat.Depth24Stencil8 || surface.Format == XFG.DepthFormat.Depth24 )
            {
                HardwareCapabilities.SetCapability( Capabilities.StencilBuffer );
                // always 8 here
                HardwareCapabilities.StencilBufferBits = 8;
            }

            // some cards, oddly enough, do not support this
            if ( _capabilities.DeclarationTypeCapabilities.SupportsByte4 )
            {
                HardwareCapabilities.SetCapability( Capabilities.VertexFormatUByte4 );
            }

            // Anisotropy?
            if ( _capabilities.MaxAnisotropy > 1 )
            {
                HardwareCapabilities.SetCapability( Capabilities.AnisotropicFiltering );
            }

            // Hardware mipmapping?
            if ( _capabilities.DriverCapabilities.CanAutoGenerateMipMap )
            {
                HardwareCapabilities.SetCapability( Capabilities.HardwareMipMaps );
            }

            // blending between stages is definately supported
            HardwareCapabilities.SetCapability( Capabilities.TextureBlending );
            HardwareCapabilities.SetCapability( Capabilities.MultiTexturing );

            // Dot3 bump mapping?
            //if ( _capabilities.TextureCapabilities.SupportsDotProduct3 )
            //{
            //    HardwareCapabilities.SetCap( Capabilities.Dot3 );
            //}

            // Cube mapping?
            if ( _capabilities.TextureCapabilities.SupportsCubeMap )
            {
                HardwareCapabilities.SetCapability( Capabilities.CubeMapping );
            }

            // Texture Compression
            // We always support compression, Xna will decompress if device does not support
            HardwareCapabilities.SetCapability( Capabilities.TextureCompression );
            HardwareCapabilities.SetCapability( Capabilities.TextureCompressionDXT );

            // Xna uses vertex buffers for everything
            HardwareCapabilities.SetCapability( Capabilities.VertexBuffer );

            // Scissor test
            if ( _capabilities.RasterCapabilities.SupportsScissorTest )
            {
                HardwareCapabilities.SetCapability( Capabilities.ScissorTest );
            }

            // 2 sided stencil
            if ( _capabilities.StencilCapabilities.SupportsTwoSided )
            {
                HardwareCapabilities.SetCapability( Capabilities.TwoSidedStencil );
            }

            // stencil wrap
            if ( _capabilities.StencilCapabilities.SupportsIncrement && _capabilities.StencilCapabilities.SupportsDecrement )
            {
                HardwareCapabilities.SetCapability( Capabilities.StencilWrap );
            }

            // Hardware Occlusion, new!
            XFG.OcclusionQuery oQuery = new Microsoft.Xna.Framework.Graphics.OcclusionQuery( device );
            if ( oQuery.IsSupported )
                caps.SetCap( Capabilities.HardwareOcculusion );
            oQuery.Dispose();


            if ( _capabilities.MaxUserClipPlanes > 0 )
            {
                HardwareCapabilities.SetCapability( Capabilities.UserClipPlanes );
            }

            _checkVertexProgramCapabilities();

            _checkFragmentProgramCapabilities();

            // Infinite projection?
            // We have no capability for this, so we have to base this on our
            // experience and reports from users
            // Non-vertex program capable hardware does not appear to support it
            if ( HardwareCapabilities.HasCapability( Capabilities.VertexPrograms ) )
            {
                // GeForce4 Ti (and presumably GeForce3) does not
                // render infinite projection properly, even though it does in GL
                // So exclude all cards prior to the FX range from doing infinite

                XFG.GraphicsAdapter details = XFG.GraphicsAdapter.Adapters[0];

                // not nVidia or GeForceFX and above
                if ( details.VendorId != 0x10DE || details.DeviceId >= 0x0301 )
                    {
                    HardwareCapabilities.SetCapability( Capabilities.InfiniteFarPlane );
                    }
                    }

            // write hardware capabilities to registered log listeners
            HardwareCapabilities.Log();
            }

        private void _checkFragmentProgramCapabilities()
            {
            int fpMajor = _capabilities.PixelShaderVersion.Major;
            int fpMinor = _capabilities.PixelShaderVersion.Minor;

            switch ( fpMajor )
            {
                case 1:
                    HardwareCapabilities.MaxFragmentProgramVersion = string.Format( "ps_1_{0}", fpMinor );

                    HardwareCapabilities.FragmentProgramConstantIntCount = 0;
                    // 8 4d float values, entered as floats but stored as fixed
                    HardwareCapabilities.FragmentProgramConstantFloatCount = 8;
                    break;

                case 2:
                    if ( fpMinor > 0 )
                    {
                        HardwareCapabilities.MaxFragmentProgramVersion = "ps_2_x";
                        //16 integer params allowed
                        HardwareCapabilities.FragmentProgramConstantIntCount = 16 * 4;
                        // 4d float params
                        HardwareCapabilities.FragmentProgramConstantFloatCount = 224;
                    }
                    else
                    {
                        HardwareCapabilities.MaxFragmentProgramVersion = "ps_2_0";
                        // no integer params allowed
                        HardwareCapabilities.FragmentProgramConstantIntCount = 0;
                        // 4d float params
                        HardwareCapabilities.FragmentProgramConstantFloatCount = 32;
                    }

                    break;

                case 3:
                    if ( fpMinor > 0 )
                    {
                        HardwareCapabilities.MaxFragmentProgramVersion = "ps_3_x";
                    }
                    else
                    {
                        HardwareCapabilities.MaxFragmentProgramVersion = "ps_3_0";
                    }

                    // 16 integer params allowed
                    HardwareCapabilities.FragmentProgramConstantIntCount = 16;
                    HardwareCapabilities.FragmentProgramConstantFloatCount = 224;
                    break;

                default:
                    // doh, SOL
                    HardwareCapabilities.MaxFragmentProgramVersion = "";
                    break;
            }

            // Fragment Program syntax code checks
            if ( fpMajor >= 1 )
            {
                HardwareCapabilities.SetCapability( Capabilities.FragmentPrograms );
                _gpuProgramMgr.PushSyntaxCode( "ps_1_1" );

                if ( fpMajor > 1 || fpMinor >= 2 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "ps_1_2" );
                }
                if ( fpMajor > 1 || fpMinor >= 3 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "ps_1_3" );
                }
                if ( fpMajor > 1 || fpMinor >= 4 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "ps_1_4" );
                }
            }

            if ( fpMajor >= 2 )
            {
                _gpuProgramMgr.PushSyntaxCode( "ps_2_0" );

                if ( fpMinor > 0 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "ps_2_x" );
                }
            }

            if ( fpMajor >= 3 )
            {
                _gpuProgramMgr.PushSyntaxCode( "ps_3_0" );

                if ( fpMinor > 0 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "ps_3_x" );
                }
            }
        }

        private void _checkVertexProgramCapabilities()
            {
            int vpMajor = _capabilities.VertexShaderVersion.Major;
            int vpMinor = _capabilities.VertexShaderVersion.Minor;

            // check vertex program HardwareCapabilities
            switch ( vpMajor )
                {
                case 1:
                    HardwareCapabilities.MaxVertexProgramVersion = "vs_1_1";
                    // 4d float vectors
                    HardwareCapabilities.VertexProgramConstantFloatCount = _capabilities.MaxVertexShaderConstants;
                    // no int params supports
                    HardwareCapabilities.VertexProgramConstantIntCount = 0;
                    break;
                case 2:
                    if ( vpMinor > 0 )
                    {
                        HardwareCapabilities.MaxVertexProgramVersion = "vs_2_x";
                    }
                    else
                    {
                        HardwareCapabilities.MaxVertexProgramVersion = "vs_2_0";
                    }

                    // 16 ints
                    HardwareCapabilities.VertexProgramConstantIntCount = 16 * 4;
                    // 4d float vectors
                    HardwareCapabilities.VertexProgramConstantFloatCount = _capabilities.MaxVertexShaderConstants;

                    break;
                case 3:
                    HardwareCapabilities.MaxVertexProgramVersion = "vs_3_0";

                    // 16 ints
                    HardwareCapabilities.VertexProgramConstantIntCount = 16 * 4;
                    // 4d float vectors
                    HardwareCapabilities.VertexProgramConstantFloatCount = _capabilities.MaxVertexShaderConstants;

                    break;
                default:
                    // not gonna happen
                    HardwareCapabilities.MaxVertexProgramVersion = "";
                        break;
                    }

            // check for supported vertex program syntax codes
            if ( vpMajor >= 1 )
            {
                HardwareCapabilities.SetCapability( Capabilities.VertexPrograms );
                _gpuProgramMgr.PushSyntaxCode( "vs_1_1" );
            }
            if ( vpMajor >= 2 )
            {
                if ( vpMajor > 2 || vpMinor > 0 )
                {
                    _gpuProgramMgr.PushSyntaxCode( "vs_2_x" );
                }
                _gpuProgramMgr.PushSyntaxCode( "vs_2_0" );
            }
            if ( vpMajor >= 3 )
            {
                _gpuProgramMgr.PushSyntaxCode( "vs_3_0" );
            }
                }

        private XNA.Matrix _makeXnaMatrix( Axiom.Math.Matrix4 matrix )
        {
            XNA.Matrix xna = new XNA.Matrix();

            xna.M11 = matrix.m00;
            xna.M12 = matrix.m01;
            xna.M13 = matrix.m02;
            xna.M14 = matrix.m03;
            xna.M21 = matrix.m10;
            xna.M22 = matrix.m11;
            xna.M23 = matrix.m12;
            xna.M24 = matrix.m13;
            xna.M31 = matrix.m20;
            xna.M32 = matrix.m21;
            xna.M33 = matrix.m22;
            xna.M34 = matrix.m23;
            xna.M41 = matrix.m30;
            xna.M42 = matrix.m31;
            xna.M43 = matrix.m32;
            xna.M44 = matrix.m33;

            return xna;
        }

        #endregion Private Helper Functions

        #region Axiom.Core.RenderSystem Implementation

        #region Properties

        private ColorEx _ambientLight = ColorEx.White;
        public override ColorEx AmbientLight
        {
            get
            {
                return _ambientLight;
            }
            set
            {
                _ambientLight = value;
#if !(XBOX || XBOX360 || SILVERLIGHT)
                _ffProgramParameters.LightAmbient = value;
#endif
            }
        }

        public override CullingMode CullingMode
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                cullingMode = value;

                bool flip = activeRenderTarget.RequiresTextureFlipping ^ invertVertexWinding;
                _device.RenderState.CullMode = XnaHelper.Convert( value, flip );
            }
        }

        public override bool DepthWrite
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                _device.RenderState.DepthBufferWriteEnable = value;
            }
        }

        public override bool DepthCheck
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                _device.RenderState.DepthBufferEnable = value;
            }
        }

        public override CompareFunction DepthFunction
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                _device.RenderState.DepthBufferFunction = XnaHelper.Convert( value );
            }
        }

        public override int DepthBias
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                _device.RenderState.DepthBias = (float)value;
            }
        }

        public override float HorizontalTexelOffset
        {
            // Xna considers the origin to be in the center of a pixel?
            get
            {
                return -0.5f;
            }
        }

        private bool _lightingEnabled;
        public override bool LightingEnabled
        {
            get
            {
                return _lightingEnabled;
            }
            set
            {
                _lightingEnabled = value;
#if !(XBOX || XBOX360 || SILVERLIGHT)
                _ffProgramParameters.LightingEnabled = value;
#endif
            }
        }

        public override bool NormalizeNormals
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                throw new Exception( "The method or operation is not implemented." );
            }
        }

        private Matrix4 _projectionMatrix;
        public override Matrix4 ProjectionMatrix
        {
            get
            {
                return _projectionMatrix;
            }
            set
            {
                _projectionMatrix = value;

                /*XNA.Matrix mat = XnaHelper.Convert(value);

                if (activeRenderTarget.RequiresTextureFlipping)
                {
                    mat.M22 = -mat.M22;
                }*/



#if !(XBOX || XBOX360 || SILVERLIGHT)
                _ffProgramParameters.ProjectionMatrix = value;
#endif

            }
        }

        public override PolygonMode RasterizationMode
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                switch ( value )
                {
                    case PolygonMode.Points:
                        _device.RenderState.FillMode = XFG.FillMode.Point;
                        break;
                    case PolygonMode.Wireframe:
                        _device.RenderState.FillMode = XFG.FillMode.WireFrame;
                        break;
                    case PolygonMode.Solid:
                        _device.RenderState.FillMode = XFG.FillMode.Solid;
                        break;
                }
            }
        }

        public override Shading ShadingMode
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                //throw new Exception("The method or operation is not implemented.");

            }
        }

        public override bool StencilCheckEnabled
        {
            get
            {
                throw new Exception( "The method or operation is not implemented." );
            }
            set
            {
                _device.RenderState.StencilEnable = value;
            }
        }

        public override float VerticalTexelOffset
        {
            get
            {
                // Xna considers the origin to be in the center of a pixel ?
                return -0.5f;
            }
        }

        public override Matrix4 ViewMatrix
        {
            get
            {
                return _viewMatrix;
            }
            set
            {
                // flip the transform portion of the matrix for DX and its left-handed coord system
                // save latest view matrix
                _viewMatrix = value;
                _viewMatrix.m20 = -_viewMatrix.m20;
                _viewMatrix.m21 = -_viewMatrix.m21;
                _viewMatrix.m22 = -_viewMatrix.m22;
                _viewMatrix.m23 = -_viewMatrix.m23;

#if !(XBOX || XBOX360 || SILVERLIGHT)
                _ffProgramParameters.ViewMatrix = _viewMatrix;
#endif
            }
        }

        private Matrix4 _worldMatrix;
        public override Axiom.Math.Matrix4 WorldMatrix
        {
            get
            {
                return _worldMatrix;
            }
            set
            {
                _worldMatrix = value;
#if !(XBOX || XBOX360 || SILVERLIGHT)
                _ffProgramParameters.WorldMatrix = _worldMatrix;
#endif
            }
        }

        #endregion Properties

        #region Methods

        public override void ApplyObliqueDepthProjection( ref Axiom.Math.Matrix4 projMatrix, Axiom.Math.Plane plane, bool forGpuProgram )
        {
            // Thanks to Eric Lenyel for posting this calculation at www.terathon.com
            // Calculate the clip-space corner point opposite the clipping plane
            // as (sgn(clipPlane.x), sgn(clipPlane.y), 1, 1) and
            // transform it into camera space by multiplying it
            // by the inverse of the projection matrix
            /* generalised version
            Vector4 q = matrix.inverse() * 
                Vector4(Math::Sign(plane.normal.x), Math::Sign(plane.normal.y), 1.0f, 1.0f);
            */
            Axiom.Math.Vector4 q = new Axiom.Math.Vector4();
            q.x = System.Math.Sign( plane.Normal.x ) / projMatrix.m00;
            q.y = System.Math.Sign( plane.Normal.y ) / projMatrix.m11;
            q.z = 1.0f;

            // flip the next bit from Lengyel since we're right-handed
            if ( forGpuProgram )
            {
                q.w = ( 1.0f - projMatrix.m22 ) / projMatrix.m23;
            }
            else
            {
                q.w = ( 1.0f + projMatrix.m22 ) / projMatrix.m23;
            }

            // Calculate the scaled plane vector
            Axiom.Math.Vector4 clipPlane4d =
                new Axiom.Math.Vector4( plane.Normal.x, plane.Normal.y, plane.Normal.z, plane.D );

            Axiom.Math.Vector4 c = clipPlane4d * ( 1.0f / ( clipPlane4d.Dot( q ) ) );

            // Replace the third row of the projection matrix
            projMatrix.m20 = c.x;
            projMatrix.m21 = c.y;

            // flip the next bit from Lengyel since we're right-handed
            if ( forGpuProgram )
            {
                projMatrix.m22 = c.z;
            }
            else
            {
                projMatrix.m22 = -c.z;
            }

            projMatrix.m23 = c.w;
        }

        public override void BeginFrame()
        {
            // clear the device if need be
            if ( activeViewport.ClearEveryFrame )
            {
                ClearFrameBuffer( FrameBuffer.Color | FrameBuffer.Depth, activeViewport.BackgroundColor );
            }
            // set initial render states if this is the first frame. we only want to do 
            //	this once since renderstate changes are expensive
            if ( _isFirstFrame )
            {
                // enable alpha blending and specular materials
                _device.RenderState.AlphaBlendEnable = true;
                //_device.RenderState.SpecularEnable = true;
                _device.RenderState.DepthBufferEnable = true;
                _isFirstFrame = false;
            }
        }

        public override void BindGpuProgram( GpuProgram program )
        {
            switch ( program.Type )
            {
                case GpuProgramType.Vertex:
                    _device.VertexShader = ( (XnaVertexProgram)program ).VertexShader;
                    break;

                case GpuProgramType.Fragment:
                    _device.PixelShader = ( (XnaFragmentProgram)program ).PixelShader;
                    break;
            }
        }

        public override void BindGpuProgramParameters( GpuProgramType type, GpuProgramParameters parms )
        {
            switch ( type )
            {
                case GpuProgramType.Vertex:
                    if ( parms.HasIntConstants )
                    {
                        for ( int index = 0; index < parms.IntConstantCount; index++ )
                        {
                            GpuProgramParameters.IntConstantEntry entry = parms.GetIntConstant( index );

                            if ( entry.isSet )
                            {
                                _device.SetVertexShaderConstant( index, entry.val );
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
                                _device.SetVertexShaderConstant( index, entry.val );
                            }
                        }
                    }
                    break;
                case GpuProgramType.Fragment:
                    if ( parms.HasIntConstants )
                    {
                        for ( int index = 0; index < parms.IntConstantCount; index++ )
                        {
                            GpuProgramParameters.IntConstantEntry entry = parms.GetIntConstant( index );

                            if ( entry.isSet )
                            {
                                _device.SetPixelShaderConstant( index, entry.val );
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
                                _device.SetPixelShaderConstant( index, entry.val );
                            }
                        }
                    }
                    break;
            }
        }

        public override void ClearFrameBuffer( RenderTarget.FrameBuffer buffers, ColorEx color, float depth, int stencil )
        {
            XFG.ClearOptions flags = 0; //ClearFlags 

            if ( ( buffers & FrameBuffer.Color ) > 0 )
            {
                flags |= XFG.ClearOptions.Target;
            }
            if ( ( buffers & FrameBuffer.Depth ) > 0 )
            {
                flags |= XFG.ClearOptions.DepthBuffer;
            }
            // Only try to clear the stencil buffer if supported
            if ( ( buffers & FrameBuffer.Stencil ) > 0
                && caps.CheckCap( Capabilities.StencilBuffer ) )
            {

                flags |= XFG.ClearOptions.Stencil;
            }
            XFG.Color col = new XFG.Color( (byte)( color.r * 255.0f ), (byte)( color.g * 255.0f ), (byte)( color.b * 255.0f ), (byte)( color.a * 255.0f ) );

            // clear the device using the specified params
            _device.Clear( flags, col, depth, stencil );
        }

        public override int ConvertColor( ColorEx color )
        {
            return color.ToARGB();
        }

        public override RenderTexture CreateRenderTexture( string name, int width, int height )
        {
            XnaRenderTexture renderTexture = new XnaRenderTexture( name, width, height );
            AttachRenderTarget( renderTexture );
            return renderTexture;
        }

        public override RenderWindow CreateRenderWindow( string name, int width, int height, int colorDepth, bool isFullscreen, int left, int top, bool depthBuffer, bool vsync, IntPtr target )
        {
            if ( _device == null )
            {
                _device = InitDevice( isFullscreen, depthBuffer, width, height, colorDepth, target );

                /*if (!isFullscreen)
                {
                    _device = InitDevice(isFullscreen, depthBuffer, width, height, colorDepth, target);
                }
                else
                {
                    _device = InitDevice(isFullscreen, depthBuffer, width, height, colorDepth, IntPtr.Zero);
                }*/
            }

            RenderWindow window = new XnaRenderWindow();

            window.Handle = target;

            // create the window
            window.Create( name, width, height, colorDepth, isFullscreen, left, top, depthBuffer, target, _device );

            // add the new render target
            AttachRenderTarget( window );

            return window;
        }

        private XFG.GraphicsDevice InitDevice( bool isFullscreen, bool depthBuffer, int width, int height, int colorDepth, IntPtr target )
        {
            if ( _device != null )
            {
                return _device;
            }

            XFG.GraphicsDevice newDevice;

            // if this is the first window, get the device and do other initialization
            XFG.PresentationParameters presentParams = new XFG.PresentationParameters();
            presentParams.IsFullScreen = isFullscreen;
            presentParams.BackBufferCount = 1;
            presentParams.EnableAutoDepthStencil = depthBuffer;
            presentParams.BackBufferWidth = width;
            presentParams.BackBufferHeight = height;
            presentParams.MultiSampleType = XFG.MultiSampleType.None;
            presentParams.SwapEffect = XFG.SwapEffect.Copy;

            // TODO: Check vsync setting
            presentParams.PresentationInterval = XFG.PresentInterval.Immediate;

            // supports 16 and 32 bit color
            if ( colorDepth == 16 )
            {
                presentParams.BackBufferFormat = XFG.SurfaceFormat.Bgr565;
            }
            else
            {
                //could not create depth24stencil8 with "Color"
                presentParams.BackBufferFormat = XFG.SurfaceFormat.Bgr32;
            }

            if ( colorDepth > 16 )
            {
                if ( XFG.GraphicsAdapter.DefaultAdapter.CheckDepthStencilMatch( XFG.DeviceType.Hardware, presentParams.BackBufferFormat, presentParams.BackBufferFormat, XFG.DepthFormat.Depth24Stencil8 ) )
                {
                    presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth24Stencil8;
                }
                // check for 24 bit Z buffer with 8 bit stencil (optimal choice)
                if ( !XFG.GraphicsAdapter.DefaultAdapter.CheckDeviceFormat(
                     XFG.DeviceType.Hardware,
                     XFG.GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Format,
                     XFG.TextureUsage.None,
                     XFG.QueryUsages.None,
                     XFG.ResourceType.DepthStencilBuffer,
                     XFG.DepthFormat.Depth24Stencil8 ) )
                {
                    // doh, check for 32 bit Z buffer then

                    if ( !XFG.GraphicsAdapter.DefaultAdapter.CheckDeviceFormat(
                         XFG.DeviceType.Hardware,
                         XFG.GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Format,
                         XFG.TextureUsage.None,
                         XFG.QueryUsages.None,
                         XFG.ResourceType.DepthStencilBuffer,
                         XFG.DepthFormat.Depth32 ) )
                    {
                        // float doh, just use 16 bit Z buffer
                        presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth16;
                    }
                    else
                    {
                        // use 32 bit Z buffer
                        presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth32;
                    }
                }
                else
                {
                    // <flair>Woooooooooo!</flair>
                    if ( XFG.GraphicsAdapter.DefaultAdapter.CheckDepthStencilMatch(
                            XFG.DeviceType.Hardware,
                            XFG.GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Format,
                            presentParams.BackBufferFormat,
                            XFG.DepthFormat.Depth24Stencil8 ) )
                    {
                        presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth24Stencil8;
                    }
                    else
                    {
                        presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth24;
                    }
                }
            }
            else
            {
                // use 16 bit Z buffer if they arent using true color
                presentParams.AutoDepthStencilFormat = XFG.DepthFormat.Depth16;
            }

            // create the D3D Device, trying for the best vertex support first, and settling for less if necessary
            try
            {
                // hardware vertex processing
                newDevice = new XFG.GraphicsDevice( XFG.GraphicsAdapter.DefaultAdapter,
                                                    XFG.DeviceType.Hardware,
                                                    target,
                                                    presentParams
                                                );
            }
            catch ( Exception )
            {
                try
                {
                    // doh, how bout mixed vertex processing
                    newDevice = new XFG.GraphicsDevice(
                                                        XFG.GraphicsAdapter.DefaultAdapter,
                                                        XFG.DeviceType.Hardware,
                                                        target,
                                                        presentParams
                                                       );
                }
                catch ( XFG.DeviceNotSupportedException )
                {
                    // what the...ok, how bout software vertex procssing.  if this fails, then I don't even know how they are seeing
                    // anything at all since they obviously don't have a video card installed
                    newDevice = new XFG.GraphicsDevice(
                                                        XFG.GraphicsAdapter.DefaultAdapter,
                                                        XFG.DeviceType.Hardware,
                                                        target,
                                                        presentParams
                                                       );
                }
            }

            // CMH - end
            // save the device capabilites
            _capabilities = newDevice.GraphicsDeviceCapabilities;

            // by creating our texture manager, singleton TextureManager will hold our implementation
            textureMgr = new XnaTextureManager( newDevice );

            // by creating our Gpu program manager, singleton GpuProgramManager will hold our implementation
            gpuProgramMgr = new XnaGpuProgramManager( newDevice );

            // intializes the HardwareBufferManager singleton
            hardwareBufferManager = new XnaHardwareBufferManager( newDevice );

            _checkCapabilities( newDevice );

            return newDevice;
        }

        public override IHardwareOcclusionQuery CreateHardwareOcclusionQuery()
        {
            return new XnaHardwareOcclusionQuery( _device );
        }

        public override void EndFrame()
        {
            // end the scene
        }

        public override RenderWindow Initialize( bool autoCreateWindow, string windowTitle )
        {
            RenderWindow renderWindow = null;

            // register the HLSL program manager
            HighLevelGpuProgramManager.Instance.AddFactory( new HLSL.HLSLProgramFactory() );

            if ( autoCreateWindow )
            {
                int width = 800;
                int height = 600;
                int bpp = 32;
                bool fullScreen = false;

                ConfigOption optVM = ConfigOptions[ "Video Mode" ];
                string vm = optVM.Value;
                width = int.Parse( vm.Substring( 0, vm.IndexOf( "x" ) ) );
                height = int.Parse( vm.Substring( vm.IndexOf( "x" ) + 1, vm.IndexOf( "@" ) - ( vm.IndexOf( "x" ) + 1 ) ) );
                bpp = int.Parse( vm.Substring( vm.IndexOf( "@" ) + 1, vm.IndexOf( "-" ) - ( vm.IndexOf( "@" ) + 1 ) ) );

#if !(XBOX || XBOX360 || SILVERLIGHT ) //
                fullScreen = ( ConfigOptions[ "Full Screen" ].Value == "Yes" );
#endif

                IntPtr target = IntPtr.Zero;

                // create a default form window
#if !(XBOX || XBOX360 || SILVERLIGHT )
                DefaultForm newWindow = _createDefaultForm( windowTitle, 0, 0, width, height, fullScreen );
                target = newWindow.Handle;
#endif

                // create the render window
                renderWindow = CreateRenderWindow( "Main Window", width, height, bpp, fullScreen, 0, 0, true, false, target );

                // use W buffer when in 16 bit color mode
                //useWBuffer = (renderWindow.ColorDepth == 16);
#if !(XBOX || XBOX360 || SILVERLIGHT )
                newWindow.Target.Visible = false;

                newWindow.Show();

                // set the default form's renderwindow so it can access it internally
                newWindow.RenderWindow = renderWindow;
#endif
            }

            return renderWindow;
        }

        public override Axiom.Math.Matrix4 MakeOrthoMatrix( float fov, float aspectRatio, float near, float far, bool forGpuPrograms )
        {
            float thetaY = Utility.DegreesToRadians( fov / 2.0f );
            float tanThetaY = Utility.Tan( thetaY );
            float tanThetaX = tanThetaY * aspectRatio;

            float halfW = tanThetaX * near;
            float halfH = tanThetaY * near;

            float w = 1.0f / ( halfW );
            float h = 1.0f / ( halfH );
            float q = 0;

            if ( far != 0 )
            {
                q = 1.0f / ( far - near );
            }

            Matrix4 dest = Matrix4.Zero;
            dest.m00 = w;
            dest.m11 = h;
            dest.m22 = q;
            dest.m23 = -near / ( far - near );
            dest.m33 = 1;

            if ( forGpuPrograms )
            {
                dest.m22 = -dest.m22;
            }

            return dest;
        }

        public override Axiom.Math.Matrix4 MakeProjectionMatrix( float fov, float aspectRatio, float near, float far, bool forGpuProgram )
        {
            float theta = Utility.DegreesToRadians( fov * 0.5f );
            float h = 1 / Utility.Tan( theta );
            float w = h / aspectRatio;
            float q = 0;
            float qn = 0;

            if ( far == 0 )
            {
                q = 1 - Frustum.InfiniteFarPlaneAdjust;
                qn = near * ( Frustum.InfiniteFarPlaneAdjust - 1 );
            }
            else
            {
                q = far / ( far - near );
                qn = -q * near;
            }

            Matrix4 dest = Matrix4.Zero;

            dest.m00 = w;
            dest.m11 = h;

            if ( forGpuProgram )
            {
                dest.m22 = -q;
                dest.m32 = -1.0f;
            }
            else
            {
                dest.m22 = q;
                dest.m32 = 1.0f;
            }

            dest.m23 = qn;

            return dest;
        }

        public override Matrix4 ConvertProjectionMatrix( Matrix4 mat, bool forGpuProgram )
        {
            Matrix4 dest = new Matrix4( mat.m00, mat.m01, mat.m02, mat.m03,
                                       mat.m10, mat.m11, mat.m12, mat.m13,
                                       mat.m20, mat.m21, mat.m22, mat.m23,
                                       mat.m30, mat.m31, mat.m32, mat.m33 );

            // Convert depth range from [-1,+1] to [0,1]
            dest.m20 = ( dest.m20 + dest.m30 ) / 2.0f;
            dest.m21 = ( dest.m21 + dest.m31 ) / 2.0f;
            dest.m22 = ( dest.m22 + dest.m32 ) / 2.0f;
            dest.m23 = ( dest.m23 + dest.m33 ) / 2.0f;

            if ( !forGpuProgram )
            {
                // Convert right-handed to left-handed
                dest.m02 = -dest.m02;
                dest.m12 = -dest.m12;
                dest.m22 = -dest.m22;
                dest.m32 = -dest.m32;
            }

            return dest;
        }

        //XFG.BasicEffect ef;
        public override void Render( RenderOperation op )
        {
            // don't even bother if there are no vertices to render, causes problems on some cards (FireGL 8800)
            if ( op.vertexData.vertexCount == 0 )
            {
                return;
            }

            // class base implementation first
            base.Render( op );


            /*---------------shaders generator part------*/
#if !(XBOX || XBOX360 || SILVERLIGHT )

            bool needToUnmapVS = false;
            bool needToUnmapFS = false;

            if ( _device.VertexShader == null || _device.PixelShader == null )
            {
                FixedFunctionEmulation.VertexBufferDeclaration vbd = new FixedFunctionEmulation.VertexBufferDeclaration();
                List<FixedFunctionEmulation.VertexBufferElement> lvbe = new List<FixedFunctionEmulation.VertexBufferElement>( op.vertexData.vertexDeclaration.ElementCount );

                int textureLayer = 0;
                for ( int i = 0; i < op.vertexData.vertexDeclaration.ElementCount; i++ )
                {
                    FixedFunctionEmulation.VertexBufferElement element = new FixedFunctionEmulation.VertexBufferElement();

                    element.VertexElementIndex = (ushort)op.vertexData.vertexDeclaration[ i ].Index;
                    element.VertexElementSemantic = op.vertexData.vertexDeclaration[ i ].Semantic;
                    element.VertexElementType = op.vertexData.vertexDeclaration[ i ].Type;

                    //uncomment this to see the texture shadow
                    //the problem is that texture states are not set and texture are set  in vertexdeclaration
                    //
                    if (//op.vertexData.vertexDeclaration[i].Type == VertexElementType.Float1 &&
                        op.vertexData.vertexDeclaration[ i ].Semantic == VertexElementSemantic.TexCoords )
                    {
                        if ( !texStageDesc[ textureLayer ].Enabled )
                        {

                            texStageDesc[ textureLayer ].layerBlendMode = new LayerBlendModeEx();
                            texStageDesc[ textureLayer ].layerBlendMode.blendType = LayerBlendType.Color;
                            texStageDesc[ textureLayer ].layerBlendMode.operation = LayerBlendOperationEx.Modulate;
                            texStageDesc[ textureLayer ].layerBlendMode.source1 = LayerBlendSource.Texture;
                            texStageDesc[ textureLayer ].layerBlendMode.source2 = LayerBlendSource.Current;

                            texStageDesc[ textureLayer ].Enabled = true;
                            texStageDesc[ textureLayer ].autoTexCoordType = TexCoordCalcMethod.None;
                            texStageDesc[ textureLayer ].coordIndex = textureLayer;
                            switch ( op.vertexData.vertexDeclaration[ i ].Type )
                            {
                                case VertexElementType.Float1:
                                    texStageDesc[ textureLayer ].texType = TextureType.OneD;
                                    break;
                                case VertexElementType.Float2:
                                    texStageDesc[ textureLayer ].texType = TextureType.TwoD;
                                    break;
                                case VertexElementType.Float3:
                                    texStageDesc[ textureLayer ].texType = TextureType.ThreeD;
                                    break;
                            }
                            //texStageDesc[textureLayer].layerBlendMode = new LayerBlendModeEx();
                        }
                        textureLayer++;
                    }

                    lvbe.Add( element );
                }
                vbd.VertexBufferElements = lvbe;


                for ( int i = 0; i < Config.MaxTextureLayers; i++ )
                {
                    FixedFunctionEmulation.TextureLayerState tls = new FixedFunctionEmulation.TextureLayerState();

                    if ( texStageDesc[ i ].Enabled )
                    //if (texStageDesc[i].tex != null)
                    {
                        tls.TextureType = texStageDesc[ i ].texType;
                        tls.TexCoordCalcMethod = texStageDesc[ i ].autoTexCoordType;
                        tls.CoordIndex = texStageDesc[ i ].coordIndex;
                        tls.LayerBlendMode = texStageDesc[ i ].layerBlendMode;
                        //TextureLayerStateList
                        _fixedFunctionState.TextureLayerStates.Add( tls );
                    }
                }

                FixedFunctionEmulation.GeneralFixedFunctionState gff;
                gff = _fixedFunctionState.GeneralFixedFunctionState;

                gff.EnableLighting = _ffProgramParameters.LightingEnabled;
                gff.FogMode = _ffProgramParameters.FogMode;
                _fixedFunctionState.GeneralFixedFunctionState = gff;

                //lights
                foreach ( Light l in _ffProgramParameters.Lights )
                    _fixedFunctionState.Lights.Add( l.Type );


                _fixedFunctionProgram = (FixedFunctionEmulation.HLSLFixedFunctionProgram)_shaderManager.GetShaderPrograms( "hlsl", vbd, _fixedFunctionState );

                _fixedFunctionProgram.FragmentProgramUsage.Program.DefaultParameters.NamedParamCount.ToString();

                _fixedFunctionProgram.SetFixedFunctionProgramParameters( _ffProgramParameters );

                //Bind Vertex Program
                if ( _device.VertexShader == null )
                {
                    _device.VertexShader = ( (XnaVertexProgram)_fixedFunctionProgram.VertexProgramUsage.Program.BindingDelegate ).VertexShader;
                    BindGpuProgramParameters( GpuProgramType.Vertex, _fixedFunctionProgram.VertexProgramUsage.Params );
                    needToUnmapVS = true;

                }
                // Bind Fragment Program 
                if ( _device.PixelShader == null )
                {
                    _device.PixelShader = ( (XnaFragmentProgram)_fixedFunctionProgram.FragmentProgramUsage.Program.BindingDelegate ).PixelShader;
                    BindGpuProgramParameters( GpuProgramType.Fragment, _fixedFunctionProgram.FragmentProgramUsage.Params );
                    needToUnmapFS = true;
                }

                //clear parameters lists for next frame
                _fixedFunctionState.Lights.Clear();
                _fixedFunctionState.TextureLayerStates.Clear();
                //_fixedFunctionState.MaterialEnabled = false; 
                //_ffProgramParameters.FogMode = FogMode.None;


            }
            /*---------------------------------------------------------------------------------------------------------*/
#endif

            XnaVertexDeclaration vertDecl = (XnaVertexDeclaration)op.vertexData.vertexDeclaration;
            // set the vertex declaration and buffer binding
            _device.VertexDeclaration = vertDecl.XnaVertexDecl;
            _setVertexBufferBinding( op.vertexData.vertexBufferBinding );

            XFG.PrimitiveType primType = 0;

            switch ( op.operationType )
            {
                case OperationType.PointList:
                    primType = XFG.PrimitiveType.PointList;
                    primCount = op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount;
                    break;
                case OperationType.LineList:
                    primType = XFG.PrimitiveType.LineList;
                    primCount = ( op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount ) / 2;
                    break;
                case OperationType.LineStrip:
                    primType = XFG.PrimitiveType.LineStrip;
                    primCount = ( op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount ) - 1;
                    break;
                case OperationType.TriangleList:
                    primType = XFG.PrimitiveType.TriangleList;
                    primCount = ( op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount ) / 3;
                    break;
                case OperationType.TriangleStrip:
                    primType = XFG.PrimitiveType.TriangleStrip;
                    primCount = ( op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount ) - 2;
                    break;
                case OperationType.TriangleFan:
                    primType = XFG.PrimitiveType.TriangleFan;
                    primCount = ( op.useIndices ? op.indexData.indexCount : op.vertexData.vertexCount ) - 2;
                    break;
            } // switch(primType)

            // are we gonna use indices?
            if ( op.useIndices )
            {
                XnaHardwareIndexBuffer idxBuffer = (XnaHardwareIndexBuffer)op.indexData.indexBuffer;
                _device.Indices = idxBuffer.XnaIndexBuffer;
                _device.DrawIndexedPrimitives( primType, op.vertexData.vertexStart, 0, op.vertexData.vertexCount, op.indexData.indexStart, primCount );
            }
            else
            {
                // draw vertices without indices
                _device.DrawPrimitives( primType, op.vertexData.vertexStart, primCount );
            }

            //crap hack, set the sources back to null to allow accessing vertices and indices buffers
            _device.Vertices[ 0 ].SetSource( null, 0, 0 );
            _device.Vertices[ 1 ].SetSource( null, 0, 0 );
            _device.Vertices[ 2 ].SetSource( null, 0, 0 );
            _device.Indices = null;


#if !(XBOX || XBOX360 || SILVERLIGHT )
            /*---------------shaders generator part------*/
            if ( needToUnmapVS )
            {
                UnbindGpuProgram( GpuProgramType.Vertex );
            }

            if ( needToUnmapFS )
            {
                UnbindGpuProgram( GpuProgramType.Fragment );
            }
            /*--------------------------------------------*/
#endif
        }

        public override void SetAlphaRejectSettings( int stage, Axiom.Graphics.CompareFunction func, byte val )
        {
            _device.RenderState.AlphaTestEnable = ( func != Axiom.Graphics.CompareFunction.AlwaysPass );
            _device.RenderState.AlphaFunction = XnaHelper.Convert( func );
            _device.RenderState.ReferenceAlpha = val;
        }

        public override void SetColorBufferWriteEnabled( bool red, bool green, bool blue, bool alpha )
        {
            XFG.ColorWriteChannels val = 0;

            if ( red )
            {
                val |= XFG.ColorWriteChannels.Red;
            }
            if ( green )
            {
                val |= XFG.ColorWriteChannels.Green;
            }
            if ( blue )
            {
                val |= XFG.ColorWriteChannels.Blue;
            }
            if ( alpha )
            {
                val |= XFG.ColorWriteChannels.Alpha;
            }
            _device.RenderState.ColorWriteChannels = val;
        }

        public override void SetDepthBufferParams( bool depthTest, bool depthWrite, Axiom.Graphics.CompareFunction depthFunction )
        {
            _device.RenderState.DepthBufferEnable = depthTest;
            _device.RenderState.DepthBufferWriteEnable = depthWrite;
            _device.RenderState.DepthBufferFunction = XnaHelper.Convert( depthFunction );
        }

        public override void SetFog( Axiom.Graphics.FogMode mode, ColorEx color, float density, float start, float end )
        {
#if !(XBOX || XBOX360 || SILVERLIGHT )
            _ffProgramParameters.FogColor = color;
            _ffProgramParameters.FogDensity = density;
            _ffProgramParameters.FogEnd = end;
            _ffProgramParameters.FogStart = start;
            _ffProgramParameters.FogMode = mode;
#endif

            #region fog fixed function implementation
            // disable fog if set to none
            /*if ( mode == Axiom.Graphics.FogMode.None )
            {
                _device.RenderState.FogTableMode = Microsoft.Xna.Framework.Graphics.FogMode.None;
                  
                _device.RenderState.FogEnable = false;
            }
            else
            {
                // enable fog
                XFG.Color col = XnaHelper.Convert( color );
                _device.RenderState.FogEnable = true;
                _device.RenderState.FogVertexMode = XnaHelper.Convert(mode);
                _device.RenderState.FogTableMode= XnaHelper.Convert(mode);
                _device.RenderState.FogColor= col;
                _device.RenderState.FogStart= start;
                _device.RenderState.FogEnd= end;
                _device.RenderState.FogDensity= density;
                _device.RenderState.RangeFogEnable= true; 
            }*/
            #endregion

        }

        public override void SetSceneBlending( SceneBlendFactor src, SceneBlendFactor dest )
        {
            if ( src == SceneBlendFactor.One && dest == SceneBlendFactor.Zero )
            {
                _device.RenderState.AlphaBlendEnable = false;
            }
            else
            {
                _device.RenderState.AlphaBlendEnable = true;
                _device.RenderState.SeparateAlphaBlendEnabled = false;
                _device.RenderState.SourceBlend = XnaHelper.Convert( src );
                _device.RenderState.DestinationBlend = XnaHelper.Convert( dest );
            }
        }

        public override void SetScissorTest( bool enable, int left, int top, int right, int bottom )
        {
            if ( enable )
            {
                _device.ScissorRectangle = new XNA.Rectangle( left, top, right - left, bottom - top );
                _device.RenderState.ScissorTestEnable = true;
            }
            else
            {
                _device.RenderState.ScissorTestEnable = false;
            }
        }

        public override void SetStencilBufferParams( Axiom.Graphics.CompareFunction function, int refValue, int mask, Axiom.Graphics.StencilOperation stencilFailOp, Axiom.Graphics.StencilOperation depthFailOp, Axiom.Graphics.StencilOperation passOp, bool twoSidedOperation )
        {
            bool flip;
            // 2 sided operation?
            if ( twoSidedOperation )
            {
                if ( !caps.CheckCap( Capabilities.TwoSidedStencil ) )
                {
                    throw new AxiomException( "2-sided stencils are not supported on this hardware!" );
                }
                _device.RenderState.TwoSidedStencilMode = true;
                flip = ( invertVertexWinding && activeRenderTarget.RequiresTextureFlipping ) ||
                        ( !invertVertexWinding && !activeRenderTarget.RequiresTextureFlipping );

                _device.RenderState.StencilFail = XnaHelper.Convert( stencilFailOp, !flip );
                _device.RenderState.StencilDepthBufferFail = XnaHelper.Convert( depthFailOp, !flip );
                _device.RenderState.StencilPass = XnaHelper.Convert( passOp, !flip );
            }
            else
            {
                _device.RenderState.TwoSidedStencilMode = false;
                flip = false;
            }

            // configure standard version of the stencil operations
            _device.RenderState.StencilFunction = XnaHelper.Convert( function );
            _device.RenderState.ReferenceStencil = refValue;
            _device.RenderState.StencilMask = mask;
            _device.RenderState.StencilFail = XnaHelper.Convert( stencilFailOp, flip );
            _device.RenderState.StencilDepthBufferFail = XnaHelper.Convert( depthFailOp, flip );
            _device.RenderState.StencilPass = XnaHelper.Convert( passOp, flip );
            //_device.RenderState.ColorWriteChannels = XFG.ColorWriteChannels.None;
        }

        public override void SetSurfaceParams( ColorEx ambient, ColorEx diffuse, ColorEx specular, ColorEx emissive, float shininess )
        {
#if (!(XBOX||XBOX360))
            if (//ambient == ColorEx.White &&
                diffuse == ColorEx.Black //&&
                //emissive == ColorEx.Black &&
                //specular == ColorEx.Black &&
                //shininess == 0
                )
            {
                //_fixedFunctionState.MaterialEnabled = false;
                _ffProgramParameters.MaterialAmbient = new ColorEx( 0, 1, 1, 1 );
                _ffProgramParameters.MaterialDiffuse = ColorEx.White;
                _ffProgramParameters.MaterialSpecular = ColorEx.Black;
            }
            else
            {
                //_fixedFunctionState.MaterialEnabled = true;
                _ffProgramParameters.MaterialAmbient = ambient;
                _ffProgramParameters.MaterialDiffuse = diffuse;
                _ffProgramParameters.MaterialSpecular = specular;
                //_ffProgramParameters.MaterialEmissive = emissive;
                //_ffProgramParameters.MaterialShininess = shininess;
            }
#endif
        }

        public override void SetTexture( int stage, bool enabled, string textureName )
        {
            XnaTexture texture = (XnaTexture)TextureManager.Instance.GetByName( textureName );
            texStageDesc[ stage ].Enabled = enabled;
            if ( enabled && texture != null )
            {
                _device.Textures[ stage ] = texture.DXTexture;
                // set stage description
                texStageDesc[ stage ].tex = texture.DXTexture;
                texStageDesc[ stage ].texType = texture.TextureType;
            }
            else
            {
                if ( texStageDesc[ stage ].tex != null )
                {
                    _device.Textures[ stage ] = null;
                }
                // set stage description to defaults
                texStageDesc[ stage ].tex = null;
                texStageDesc[ stage ].autoTexCoordType = TexCoordCalcMethod.None;
                texStageDesc[ stage ].coordIndex = 0;
                texStageDesc[ stage ].texType = TextureType.OneD;
            }
#if !(XBOX || XBOX360 || SILVERLIGHT )
            _ffProgramParameters.SetTextureEnabled( stage, enabled );
#endif
        }

        public override void SetTextureAddressingMode( int stage, TextureAddressing texAddressingMode )
        {
            XFG.TextureAddressMode xnaMode = XnaHelper.Convert( texAddressingMode );
            // set the device sampler states accordingly
            _device.SamplerStates[ stage ].AddressU = xnaMode;
            _device.SamplerStates[ stage ].AddressV = xnaMode;
            _device.SamplerStates[ stage ].AddressW = xnaMode;
        }

        public override void SetTextureBlendMode( int stage, LayerBlendModeEx blendMode )
        {
            if ( blendMode.blendType == LayerBlendType.Color )
            {
                texStageDesc[ stage ].layerBlendMode = blendMode;
            }
        }

        public override void SetTextureCoordCalculation( int stage, TexCoordCalcMethod method, Frustum frustum )
        {
            texStageDesc[ stage ].autoTexCoordType = method;
            texStageDesc[ stage ].frustum = frustum;
        }

        public override void SetTextureCoordSet( int stage, int index )
        {
            texStageDesc[ stage ].coordIndex = index;
        }

        public override void SetTextureLayerAnisotropy( int stage, int maxAnisotropy )
        {
            if ( maxAnisotropy > _capabilities.MaxAnisotropy )
            {
                maxAnisotropy = _capabilities.MaxAnisotropy;
            }

            if ( _device.SamplerStates[ stage ].MaxAnisotropy != maxAnisotropy )
            {
                _device.SamplerStates[ stage ].MaxAnisotropy = maxAnisotropy;
            }
        }

        public override void SetTextureMatrix( int stage, Axiom.Math.Matrix4 xform )
        {
#if !(XBOX || XBOX360 || SILVERLIGHT )
            _ffProgramParameters.SetTextureMatrix( stage, xform.Transpose() );

#endif
        }

        public override void SetTextureUnitFiltering( int stage, FilterType type, Axiom.Graphics.FilterOptions filter )
        {
            XnaTextureType texType = XnaHelper.Convert( texStageDesc[ stage ].texType );
            XFG.TextureFilter texFilter = XnaHelper.Convert( type, filter, _capabilities, texType );
            switch ( type )
            {
                case FilterType.Min:
                    _device.SamplerStates[ stage ].MinFilter = texFilter;
                    break;

                case FilterType.Mag:
                    _device.SamplerStates[ stage ].MagFilter = texFilter;
                    break;

                case FilterType.Mip:
                    _device.SamplerStates[ stage ].MipFilter = texFilter;
                    break;
            }
        }

        XFG.DepthStencilBuffer oriDSB;
        public override void SetViewport( Axiom.Core.Viewport viewport )
        {
            if ( activeViewport != viewport || viewport.IsUpdated )
            {
                // store this viewport and it's target
                activeViewport = viewport;
                activeRenderTarget = viewport.Target;

                //save the original dephstencil buffer
                if ( oriDSB == null )
                {
                    oriDSB = _device.DepthStencilBuffer;
                }
                // get the back buffer surface for this viewport

                XFG.RenderTarget2D back = (XFG.RenderTarget2D)activeRenderTarget.GetCustomAttribute( "XNABACKBUFFER" );

                _device.SetRenderTarget( 0, back );

                XFG.DepthStencilBuffer depth = (XFG.DepthStencilBuffer)activeRenderTarget.GetCustomAttribute( "XNAZBUFFER" );

                // set the render target and depth stencil for the surfaces belonging to the viewport

                //dont know why the depthstencil buffer is disposing itself, have to keep it
                //if (depth.IsDisposed) _device.DepthStencilBuffer = oriDSB;
                //else _device.DepthStencilBuffer = depth;

                //if(depth!=null)
                //if(depth.Format==_device.DepthStencilBuffer.Format)
                _device.DepthStencilBuffer = depth;

                // set the culling mode, to make adjustments required for viewports
                // that may need inverted vertex winding or texture flipping
                this.CullingMode = cullingMode;

                XFG.Viewport xnavp = new XFG.Viewport();

                // set viewport dimensions
                xnavp.X = viewport.ActualLeft;
                xnavp.Y = viewport.ActualTop;
                xnavp.Width = viewport.ActualWidth;
                xnavp.Height = viewport.ActualHeight;

                // Z-values from 0.0 to 1.0 (TODO: standardize with OpenGL)
                xnavp.MinDepth = 0.0f;
                xnavp.MaxDepth = 1.0f;

                // set the current D3D viewport
                _device.Viewport = xnavp;

                // clear the updated flag
                viewport.IsUpdated = false;
            }
        }

        public override void UnbindGpuProgram( GpuProgramType type )
        {
            switch ( type )
            {
                case GpuProgramType.Vertex:
                    _device.VertexShader = null;
                    break;

                case GpuProgramType.Fragment:
                    _device.PixelShader = null;
                    break;
            }
        }

        public override void UseLights( LightList lights, int limit )
        {
            int currentLightCount = lights.Count < limit ? lights.Count : limit;

            List<Light> lightList = new List<Light>();
#if !(XBOX || XBOX360 || SILVERLIGHT )
            _fixedFunctionState.GeneralFixedFunctionState.ResetLightTypeCounts();
            for ( int index = 0; index < currentLightCount; index++ )
            {
                Light light = lights[ index ];
                lightList.Add( light );
                _fixedFunctionState.GeneralFixedFunctionState.IncrementLightTypeCount( light.Type );
            }
            _ffProgramParameters.Lights = lightList;
#endif
        }

        #endregion Methods

        #endregion Axiom.Core.RenderSystem Implementation

        #region IServiceProvider Members

        public object GetService( Type serviceType )
        {
            if ( serviceType == typeof( Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService ) )
                return this.renderTargets[ 0 ];
            else
                return null;
        }

        #endregion

    }
}
