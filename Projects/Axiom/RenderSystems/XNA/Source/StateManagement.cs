﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Axiom.RenderSystems.Xna
{
	internal class StateManagement
	{
		static readonly Stack<ManagedDeviceState> Stack = new Stack<ManagedDeviceState>();

		public StateManagement()
		{
			Stack.Push( new ManagedDeviceState() );
		}

		public ManagedDeviceState PushState( GraphicsDevice device)
		{
			var rs = new ManagedDeviceState();
			if (Stack.Count == 0) rs.Reset(device);
			else rs.Reset(Stack.Peek());
			Stack.Push(rs);
			return rs;
		}

		public void PopState( GraphicsDevice device)
		{
			var poppedRs = Stack.Pop();
			var curRs = Stack.Peek();
			curRs.Reset(device);
		}

		public ManagedDeviceState PeekState( GraphicsDevice device)
		{
			return Stack.Peek();
		}

		public void ResetState( GraphicsDevice device)
		{
			Stack.Peek().Reset(device);
		}

		public void CommitState( GraphicsDevice device)
		{
			Stack.Peek().Commit(device);
		}

		public ManagedBlendState BlendState
		{
			get
			{
				return Stack.Peek().BlendState;
			}
			set
			{
				Stack.Peek().BlendState = value;
			}
		}
		public ManagedRasterizerState RasterizerState
		{
			get
			{
				return Stack.Peek().RasterizerState;
			}
			set
			{
				Stack.Peek().RasterizerState = value;
			}
		}
		public ManagedDepthStencilState DepthStencilState
		{
			get
			{
				return Stack.Peek().DepthStencilState;
			}
			set
			{
				Stack.Peek().DepthStencilState = value;
			}
		}

		public ManagedSamplerStateList SamplerStates
		{
			get
			{
				return Stack.Peek().SamplerStates;
			}
		}
	}

	internal class ManagedSamplerStateList : List<ManagedSamplerState>
	{

		private readonly ManagedSamplerState[] _samplerStates;

		public ManagedSamplerStateList( int maxSamplers)
		{
			_samplerStates = new ManagedSamplerState[ maxSamplers ];
			for ( int i = 0; i < maxSamplers; i++ )
				_samplerStates[ i ] = new ManagedSamplerState();
		}

		public ManagedSamplerState this[ int index ]
		{
			get
			{
				return _samplerStates[ index ];
			}
			set
			{
				_samplerStates[ index ].Reset( value );
			}
		}
	}

	internal class ManagedDeviceState
	{
		const int MaxSamplers = 16;

		private ManagedRasterizerState _rasterizerState = new ManagedRasterizerState();
		public ManagedRasterizerState RasterizerState
		{
			get
			{
				return _rasterizerState;
			}
			set
			{
				_rasterizerState.Reset( value );
			}

		}

		private ManagedDepthStencilState _depthStencilState = new ManagedDepthStencilState();
		public ManagedDepthStencilState DepthStencilState
		{
			get
			{
				return _depthStencilState;
			}
			set
			{
				_depthStencilState.Reset( value );
			}

		}
		private ManagedBlendState _blendState = new ManagedBlendState();
		public ManagedBlendState BlendState
		{
			get
			{
				return _blendState;
			}
			set
			{
				_blendState.Reset( value );
			}
		}

		private ManagedSamplerStateList _samplerStates = new ManagedSamplerStateList( MaxSamplers );
		public ManagedSamplerStateList SamplerStates
		{
			get
			{
				return _samplerStates;
			}
		}

		public ManagedDeviceState()
		{
		}

		internal void Reset( ManagedDeviceState rs )
		{
			_blendState.Reset( rs.BlendState );
			_depthStencilState.Reset( rs.DepthStencilState );
			_rasterizerState.Reset( rs.RasterizerState );

			for ( int i = 0; i < MaxSamplers; i++ )
				SamplerStates[ i ].Reset( rs.SamplerStates[ i ] );

		}

		internal void Reset( GraphicsDevice device )
		{

			_blendState.Reset( device.BlendState );
			_depthStencilState.Reset( device.DepthStencilState );
			_rasterizerState.Reset( device.RasterizerState );


			for ( int i = 0; i < MaxSamplers; i++ )
				SamplerStates[ i ].Reset( device.SamplerStates[ i ] );
		}

		internal void Commit( GraphicsDevice device )
		{

			_blendState.Commit( device );
			_depthStencilState.Commit( device );
			_rasterizerState.Commit( device );

			for ( int i = 0; i < MaxSamplers; i++ )
				SamplerStates[ i ].Commit( device, i );
		}

	}

	internal interface IManagedDeviceState
	{
		bool IsDirty { get; }

		void Reset( IManagedDeviceState state );
		void Reset( GraphicsResource state );
		void Commit( GraphicsDevice device );
		void Commit( GraphicsDevice device, int index );
	}

	// Summary:
	//     Contains blend state for the device.
	internal class ManagedBlendState : IManagedDeviceState
	{
		BlendState _internalState;

		// Summary:
		//     Creates an instance of the BlendState class with default values, using additive
		//     color and alpha blending.
		public ManagedBlendState()
		{
			_internalState = new BlendState();
		}

		#region Microsoft.Xna.Framework.Graphics.BlendState Property Wrappers

		// Summary:
		//     Gets or sets the arithmetic operation when blending alpha values. The default
		//     is BlendFunction.Add.
		public BlendFunction AlphaBlendFunction
		{
			get
			{
				return _internalState.AlphaBlendFunction;
			}
			set
			{
				_internalState.AlphaBlendFunction = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the blend factor for the destination alpha, which is the percentage
		//     of the destination alpha included in the blended result. The default is Blend.One.
		public Blend AlphaDestinationBlend
		{
			get
			{
				return _internalState.AlphaDestinationBlend;
			}
			set
			{
				_internalState.AlphaDestinationBlend = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the alpha blend factor. The default is Blend.One.
		public Blend AlphaSourceBlend
		{
			get
			{
				return _internalState.AlphaSourceBlend;
			}
			set
			{
				_internalState.AlphaSourceBlend = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the four-component (RGBA) blend factor for alpha blending.
		public Color BlendFactor
		{
			get
			{
				return _internalState.BlendFactor;
			}
			set
			{
				_internalState.BlendFactor = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the arithmetic operation when blending color values. The default
		//     is BlendFunction.Add.
		public BlendFunction ColorBlendFunction
		{
			get
			{
				return _internalState.ColorBlendFunction;
			}
			set
			{
				_internalState.ColorBlendFunction = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the blend factor for the destination color. The default is Blend.One.
		public Blend ColorDestinationBlend
		{
			get
			{
				return _internalState.ColorDestinationBlend;
			}
			set
			{
				_internalState.ColorDestinationBlend = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the blend factor for the source color. The default is Blend.One.
		public Blend ColorSourceBlend
		{
			get
			{
				return _internalState.ColorSourceBlend;
			}
			set
			{
				_internalState.ColorSourceBlend = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets which color channels (RGBA) are enabled for writing during color
		//     blending. The default value is ColorWriteChannels.None.
		public ColorWriteChannels ColorWriteChannels
		{
			get
			{
				return _internalState.ColorWriteChannels;
			}
			set
			{
				_internalState.ColorWriteChannels = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets which color channels (RGBA) are enabled for writing during color
		//     blending. The default value is ColorWriteChannels.None.
		public ColorWriteChannels ColorWriteChannels1
		{
			get
			{
				return _internalState.ColorWriteChannels1;
			}
			set
			{
				_internalState.ColorWriteChannels1 = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets which color channels (RGBA) are enabled for writing during color
		//     blending. The default value is ColorWriteChannels.None.
		public ColorWriteChannels ColorWriteChannels2
		{
			get
			{
				return _internalState.ColorWriteChannels2;
			}
			set
			{
				_internalState.ColorWriteChannels2 = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets which color channels (RGBA) are enabled for writing during color
		//     blending. The default value is ColorWriteChannels.None.
		public ColorWriteChannels ColorWriteChannels3
		{
			get
			{
				return _internalState.ColorWriteChannels3;
			}
			set
			{
				_internalState.ColorWriteChannels3 = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets a bitmask which defines which samples can be written during
		//     multisampling. The default is 0xffffffff.
		public int MultiSampleMask
		{
			get
			{
				return _internalState.MultiSampleMask;
			}
			set
			{
				_internalState.MultiSampleMask = value;
				IsDirty = true;
			}
		}

		#endregion Microsoft.Xna.Framework.Graphics.BlendState Property Wrappers

		#region IManagedState Implementation

		public bool IsDirty
		{
			get;
			protected set;
		}

		public void Reset( IManagedDeviceState state )
		{
			if ( !(state is ManagedBlendState) )
				throw new ArgumentException( "Expected BlendState." );

			Reset( ( (ManagedBlendState)state )._internalState );
		}

		public void Reset( GraphicsResource state )
		{
			if ( !( state is BlendState ) )
				throw new ArgumentException( "Expected BlendState." );
			if ( _internalState.GraphicsDevice != null )
				_internalState = new BlendState();

			var blendState = (BlendState)state;
			this.AlphaBlendFunction = blendState.AlphaBlendFunction;
			this.AlphaDestinationBlend = blendState.AlphaDestinationBlend;
			this.AlphaSourceBlend = blendState.AlphaSourceBlend;
			this.BlendFactor = blendState.BlendFactor;
			this.ColorBlendFunction = blendState.ColorBlendFunction;
			this.ColorDestinationBlend = blendState.ColorDestinationBlend;
			this.ColorSourceBlend = blendState.ColorSourceBlend;
			this.ColorWriteChannels = blendState.ColorWriteChannels;
			this.ColorWriteChannels1 = blendState.ColorWriteChannels1;
			this.ColorWriteChannels2 = blendState.ColorWriteChannels2;
			this.ColorWriteChannels3 = blendState.ColorWriteChannels3;
			this.MultiSampleMask = blendState.MultiSampleMask;
		
		}

		public void Commit( GraphicsDevice device )
		{
			Commit( device, 0 );
		}

		public void Commit( GraphicsDevice device, int index )
		{
			if ( IsDirty )
				device.BlendState = _internalState;			
		}

		#endregion IManagedState Implementation

	}

	// Summary:
	//     Contains depth-stencil state for the device.
	internal class ManagedDepthStencilState : IManagedDeviceState
	{

		DepthStencilState _internalState;

		// Summary:
		//     Creates an instance of DepthStencilState with default values.
		public ManagedDepthStencilState()
		{
			_internalState = new DepthStencilState();
		}

		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil test passes
		//     and the depth-buffer test fails for a counterclockwise triangle. The default
		//     is StencilOperation.Keep.
		public StencilOperation CounterClockwiseStencilDepthBufferFail
		{
			get
			{
				return _internalState.CounterClockwiseStencilDepthBufferFail;
			}
			set
			{
				_internalState.CounterClockwiseStencilDepthBufferFail = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil test fails for
		//     a counterclockwise triangle. The default is StencilOperation.Keep.
		public StencilOperation CounterClockwiseStencilFail
		{
			get
			{
				return _internalState.CounterClockwiseStencilFail;
			}
			set
			{
				_internalState.CounterClockwiseStencilFail = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the comparison function to use for counterclockwise stencil
		//     tests. The default is CompareFunction.Always.
		public CompareFunction CounterClockwiseStencilFunction
		{
			get
			{
				return _internalState.CounterClockwiseStencilFunction;
			}
			set
			{
				_internalState.CounterClockwiseStencilFunction = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil and depth-tests
		//     pass for a counterclockwise triangle. The default is StencilOperation.Keep.
		public StencilOperation CounterClockwiseStencilPass
		{
			get
			{
				return _internalState.CounterClockwiseStencilPass;
			}
			set
			{
				_internalState.CounterClockwiseStencilPass = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Enables or disables depth buffering. The default is true.
		public bool DepthBufferEnable
		{
			get
			{
				return _internalState.DepthBufferEnable;
			}
			set
			{
				_internalState.DepthBufferEnable = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the comparison function for the depth-buffer test. The default
		//     is CompareFunction.LessEqual
		public CompareFunction DepthBufferFunction
		{
			get
			{
				return _internalState.DepthBufferFunction;
			}
			set
			{
				_internalState.DepthBufferFunction = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Enables or disables writing to the depth buffer. The default is true.
		public bool DepthBufferWriteEnable
		{
			get
			{
				return _internalState.DepthBufferWriteEnable;
			}
			set
			{
				_internalState.DepthBufferWriteEnable = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Specifies a reference value to use for the stencil test. The default is 0.
		public int ReferenceStencil
		{
			get
			{
				return _internalState.ReferenceStencil;
			}
			set
			{
				_internalState.ReferenceStencil = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil test passes
		//     and the depth-test fails. The default is StencilOperation.Keep.
		public StencilOperation StencilDepthBufferFail
		{
			get
			{
				return _internalState.StencilDepthBufferFail;
			}
			set
			{
				_internalState.StencilDepthBufferFail = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets stencil enabling. The default is false.
		public bool StencilEnable
		{
			get
			{
				return _internalState.StencilEnable;
			}
			set
			{
				_internalState.StencilEnable = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil test fails.
		//     The default is StencilOperation.Keep.
		public StencilOperation StencilFail
		{
			get
			{
				return _internalState.StencilFail;
			}
			set
			{
				_internalState.StencilFail = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the comparison function for the stencil test. The default is
		//     CompareFunction.Always.
		public CompareFunction StencilFunction
		{
			get
			{
				return _internalState.StencilFunction;
			}
			set
			{
				_internalState.StencilFunction = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the mask applied to the reference value and each stencil buffer
		//     entry to determine the significant bits for the stencil test. The default
		//     mask is Int32.MaxValue.
		public int StencilMask
		{
			get
			{
				return _internalState.StencilMask;
			}
			set
			{
				_internalState.StencilMask = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the stencil operation to perform if the stencil test passes.
		//     The default is StencilOperation.Keep.
		public StencilOperation StencilPass
		{
			get
			{
				return _internalState.StencilPass;
			}
			set
			{
				_internalState.StencilPass = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the write mask applied to values written into the stencil buffer.
		//     The default mask is Int32.MaxValue.
		public int StencilWriteMask
		{
			get
			{
				return _internalState.StencilWriteMask;
			}
			set
			{
				_internalState.StencilWriteMask = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Enables or disables two-sided stenciling. The default is false.
		public bool TwoSidedStencilMode
		{
			get
			{
				return _internalState.TwoSidedStencilMode;
			}
			set
			{
				_internalState.TwoSidedStencilMode = value;
				IsDirty = true;
			}
		}

		#region IManagedState Implementation

		public bool IsDirty
		{
			get;
			protected set;
		}

		public void Reset( IManagedDeviceState state )
		{
			if ( !( state is ManagedDepthStencilState ) )
				throw new ArgumentException( "Expected DepthStencilState." );

			Reset( ( (ManagedDepthStencilState)state )._internalState );
		}

		public void Reset( GraphicsResource state )
		{
			if ( !( state is DepthStencilState ) )
				throw new ArgumentException( "Expected DepthStencilState." );
			if ( _internalState.GraphicsDevice != null )
				_internalState = new DepthStencilState();

			var depthStencilState = (DepthStencilState)state;
			this.CounterClockwiseStencilDepthBufferFail = depthStencilState.CounterClockwiseStencilDepthBufferFail;
			this.CounterClockwiseStencilFail = depthStencilState.CounterClockwiseStencilFail;
			this.CounterClockwiseStencilFunction = depthStencilState.CounterClockwiseStencilFunction;
			this.CounterClockwiseStencilPass = depthStencilState.CounterClockwiseStencilPass;
			this.DepthBufferEnable = depthStencilState.DepthBufferEnable;
			this.DepthBufferFunction = depthStencilState.DepthBufferFunction;
			this.DepthBufferWriteEnable = depthStencilState.DepthBufferWriteEnable;
			this.ReferenceStencil = depthStencilState.ReferenceStencil;
			this.StencilDepthBufferFail = depthStencilState.StencilDepthBufferFail;
			this.StencilEnable = depthStencilState.StencilEnable;
			this.StencilFail = depthStencilState.StencilFail;
			this.StencilFunction = depthStencilState.StencilFunction;
			this.StencilMask = depthStencilState.StencilMask;
			this.StencilPass = depthStencilState.StencilPass;
			this.StencilWriteMask = depthStencilState.StencilWriteMask;
			this.TwoSidedStencilMode = depthStencilState.TwoSidedStencilMode;
			IsDirty = true;
		}

		public void Commit( GraphicsDevice device )
		{
			Commit( device, 0 );
		}

		public void Commit( GraphicsDevice device, int index )
		{
			if ( IsDirty )
			{
				device.DepthStencilState = _internalState;
				IsDirty = false;
			}
		}
		#endregion IManagedState Implementation

	}

	// Summary:
	//     Contains rasterizer state, which determines how to convert vector data (shapes)
	//     into raster data (pixels).
	internal class ManagedRasterizerState : IManagedDeviceState
	{

		RasterizerState _internalState;
		// Summary:
		//     Initializes a new instance of the rasterizer class.
		public ManagedRasterizerState()
		{
			_internalState = new RasterizerState();
			_internalState.FillMode = FillMode.Solid;
			_internalState.CullMode = CullMode.CullClockwiseFace;

		}

		// Summary:
		//     Specifies the conditions for culling or removing triangles. The default value
		//     is CullMode.CounterClockwise.
		public CullMode CullMode
		{
			get
			{
				return _internalState.CullMode;
			}
			set
			{
				_internalState.CullMode = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Sets or retrieves the depth bias for polygons, which is the amount of bias
		//     to apply to the depth of a primitive to alleviate depth testing problems
		//     for primitives of similar depth. The default value is 0.
		public float DepthBias
		{
			get
			{
				return _internalState.DepthBias;
			}
			set
			{
				_internalState.DepthBias = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     The fill mode, which defines how a triangle is filled during rendering. The
		//     default is FillMode.Solid.
		public FillMode FillMode
		{
			get
			{
				return _internalState.FillMode;
			}
			set
			{
				_internalState.FillMode = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Enables or disables multisample antialiasing. The default is true.
		public bool MultiSampleAntiAlias
		{
			get
			{
				return _internalState.MultiSampleAntiAlias;
			}
			set
			{
				_internalState.MultiSampleAntiAlias = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Enables or disables scissor testing. The default is false.
		public bool ScissorTestEnable
		{
			get
			{
				return _internalState.ScissorTestEnable;
			}
			set
			{
				_internalState.ScissorTestEnable = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets a bias value that takes into account the slope of a polygon.
		//     This bias value is applied to coplanar primitives to reduce aliasing and
		//     other rendering artifacts caused by z-fighting. The default is 0.
		public float SlopeScaleDepthBias
		{
			get
			{
				return _internalState.SlopeScaleDepthBias;
			}
			set
			{
				_internalState.SlopeScaleDepthBias = value;
				IsDirty = true;
			}
		}

		#region IManagedState Implementation

		public bool IsDirty
		{
			get;
			protected set;
		}

		public void Reset( IManagedDeviceState state )
		{
			if ( !( state is ManagedRasterizerState ) )
				throw new ArgumentException( "Expected RasterizerState." );

			Reset( ( (ManagedRasterizerState)state )._internalState );
		}

		public void Reset( GraphicsResource state )
		{
			if ( !( state is RasterizerState ) )
				throw new ArgumentException( "Expected RasterizerState." );
			if ( _internalState.GraphicsDevice != null )
				_internalState = new RasterizerState();

			var rasterizerState = (RasterizerState)state;
			this.CullMode = rasterizerState.CullMode;
			this.DepthBias = rasterizerState.DepthBias;
			this.FillMode = rasterizerState.FillMode;
			this.MultiSampleAntiAlias = rasterizerState.MultiSampleAntiAlias;
			this.ScissorTestEnable = rasterizerState.ScissorTestEnable;
			this.SlopeScaleDepthBias = rasterizerState.SlopeScaleDepthBias;
			IsDirty = true;
		}

		public void Commit( GraphicsDevice device )
		{
			Commit( device, 0 );
		}

		public void Commit( GraphicsDevice device, int index )
		{
			if ( IsDirty )
			{
				device.RasterizerState = _internalState;
				IsDirty = false;
			}
		}

		#endregion IManagedState Implementation

	}

	// Summary:
	//     Contains sampler state, which determines how to sample texture data.
	internal class ManagedSamplerState : IManagedDeviceState
	{
		SamplerState _internalState;

		// Summary:
		//     Initializes a new instance of the sampler state class.
		public ManagedSamplerState()
		{
			_internalState = new SamplerState();
		}

		// Summary:
		//     Gets or sets the texture-address mode for the u-coordinate.
		public TextureAddressMode AddressU
		{
			get
			{
				return _internalState.AddressU;
			}
			set
			{
				_internalState.AddressU = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the texture-address mode for the v-coordinate.
		public TextureAddressMode AddressV
		{
			get
			{
				return _internalState.AddressV;
			}
			set
			{
				_internalState.AddressV = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the texture-address mode for the w-coordinate.
		public TextureAddressMode AddressW
		{
			get
			{
				return _internalState.AddressW;
			}
			set
			{
				_internalState.AddressW = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the type of filtering during sampling.
		public TextureFilter Filter
		{
			get
			{
				return _internalState.Filter;
			}
			set
			{
				_internalState.Filter = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the maximum anisotropy. The default value is 0.
		public int MaxAnisotropy
		{
			get
			{
				return _internalState.MaxAnisotropy;
			}
			set
			{
				_internalState.MaxAnisotropy = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the level of detail (LOD) index of the largest map to use.
		public int MaxMipLevel
		{
			get
			{
				return _internalState.MaxMipLevel;
			}
			set
			{
				_internalState.MaxMipLevel = value;
				IsDirty = true;
			}
		}
		//
		// Summary:
		//     Gets or sets the mipmap LOD bias. The default value is 0.
		public float MipMapLevelOfDetailBias
		{
			get
			{
				return _internalState.MipMapLevelOfDetailBias;
			}
			set
			{
				_internalState.MipMapLevelOfDetailBias = value;
				IsDirty = true;
			}
		}

		#region IManagedState Implementation

		public bool IsDirty
		{
			get;
			protected set;
		}

		public void Reset( IManagedDeviceState state )
		{
			if ( !( state is ManagedSamplerState ) )
				throw new ArgumentException( "Expected SamplerState." );

			Reset( ( (ManagedSamplerState)state )._internalState );
		}

		public void Reset( GraphicsResource state )
		{
			if ( !( state is SamplerState ) )
				throw new ArgumentException( "Expected SamplerState." );
			if ( _internalState.GraphicsDevice != null )
				_internalState = new SamplerState();

			var samplerState = (SamplerState)state;
			this.AddressU = samplerState.AddressU;
			this.AddressV = samplerState.AddressV;
			this.AddressW = samplerState.AddressW;
			this.Filter = samplerState.Filter;
			this.MaxAnisotropy = samplerState.MaxAnisotropy;
			this.MaxMipLevel = samplerState.MaxMipLevel;
			this.MipMapLevelOfDetailBias = samplerState.MipMapLevelOfDetailBias;
			IsDirty = true;
		}

		public void Commit( GraphicsDevice device )
		{
			throw new NotSupportedException( "SamplerStates must be indexed." );
		}

		public void Commit( GraphicsDevice device, int index )
		{
			if ( IsDirty )
			{
				device.SamplerStates[ index ] = _internalState;
				IsDirty = false;
			}
		}

		#endregion IManagedState Implementation
	}

}
