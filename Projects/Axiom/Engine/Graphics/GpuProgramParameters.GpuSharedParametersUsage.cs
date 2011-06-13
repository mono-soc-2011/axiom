using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Axiom.Graphics
{
    public partial class GpuProgramParameters
    {
        /// <summary>
        /// This class records the usage of a set of shared parameters in a concrete
        /// set of GpuProgramParameters.
        /// </summary>
        public class GpuSharedParametersUsage
        {
            protected struct CopyDataEntry
            {
                public GpuConstantDefinition SrcDefinition;
                public GpuConstantDefinition DstDefinition;
            }

            /// <summary>
            /// list of physical mappings that we are going to bring in
            /// </summary>
            protected List<CopyDataEntry> CopyDataList = new List<CopyDataEntry>();

            /// <summary>
            /// Version of shared params we based the copydata on
            /// </summary>
            protected ulong CopyDataVersion;

            /// <summary>
            /// Get the name of the shared parameter set
            /// </summary>
            public string Name
            {
                get
                {
                    return _sharedParameters.Name;
                }
            }

            /// <summary>
            /// </summary>
            private readonly GpuSharedParameters _sharedParameters;

            /// <summary>
            /// Get's the shared parameters.
            /// </summary>
            public GpuSharedParameters SharedParameters
            {
                get
                {
                    return _sharedParameters;
                }
            }

            /// <summary>
            /// </summary>
            private readonly GpuProgramParameters _parameters;

            /// <summary>
            /// Get's the target Gpu program parameters.
            /// </summary>
            public GpuProgramParameters TargetParameters
            {
                get
                {
                    return _parameters;
                }
            }

            /// <summary>
            /// Optional data the rendersystem might want to store
            /// </summary>
            public object RenderSystemData { get; set; }

            /// <summary>
            /// Default Constructor.
            /// </summary>
            /// <param name="sharedParams"></param>
            /// <param name="gparams"></param>
            public GpuSharedParametersUsage( GpuSharedParameters sharedParams, GpuProgramParameters gparams )
            {
                _sharedParameters = sharedParams;
                _parameters = gparams;
                InitCopyData();
            }

            [AxiomHelper(0, 8, "Should try to use Marshal or something more efficient that aint unsafe here")]
            private void Memcpy(IntPtr dst, IntPtr src, int length)
            {
                unsafe
                {
                    var psrc = (byte*)src;
                    var pdst = (byte*)dst;
                    for (var i = 0; i < length; i++)
                        pdst[ i ] = psrc[ i ];
                }
            }

            /// <summary>
            /// Update the target parameters by copying the data from the shared
            /// parameters.
            /// </summary>
            /// <note>
            /// This method  may not actually be called if the RenderSystem
            /// supports using shared parameters directly in their own shared buffer; in
            /// which case the values should not be copied out of the shared area
            /// into the individual parameter set, but bound separately.
            /// </note>
            public unsafe void CopySharedParamsToTargetParams()
            {
                // check copy data version
                if ( CopyDataVersion != _sharedParameters.Version )
                    InitCopyData();

                foreach ( var e in CopyDataList )
                {
                    if ( e.DstDefinition.IsFloat )
                    {

                        using ( var srcp = _sharedParameters.GetFloatPointer( e.SrcDefinition.PhysicalIndex ) )
                        using ( var dstp = _parameters.GetFloatPointer( e.DstDefinition.PhysicalIndex ) )
                        {
                            var src = (float*)srcp.Pointer;
                            var dst = (float*)dstp.Pointer;

                            // Deal with matrix transposition here!!!
                            // transposition is specific to the dest param set, shared params don't do it
                            if ( _parameters.TransposeMatrices &&
                                 e.DstDefinition.ConstantType == GpuConstantType.Matrix_4X4 )
                            {
                                for ( var row = 0; row < 4; ++row )
                                {
                                    for ( var col = 0; col < 4; ++col )
                                    {
                                        dst[ row*4 + col ] = src[ col*4 + row ];
                                    }
                                }
                            }
                            else
                            {
                                if ( e.DstDefinition.ElementSize == e.SrcDefinition.ElementSize )
                                {
                                    // simple copy
                                    Memcpy( dstp.Pointer, srcp.Pointer,
                                            sizeof ( float )*e.DstDefinition.ElementSize*
                                            e.DstDefinition.ArraySize );
                                }
                                else
                                {
                                    // target params may be padded to 4 elements, shared params are packed
                                    System.Diagnostics.Debug.Assert( e.DstDefinition.ElementSize%4 == 0 );
                                    var iterations = e.DstDefinition.ElementSize/4
                                                     *e.DstDefinition.ArraySize;
                                    var valsPerIteration = e.SrcDefinition.ElementSize/iterations;
                                    for ( var l = 0; l < iterations; ++l )
                                    {
                                        Memcpy( (IntPtr)dst, (IntPtr)src, sizeof ( float )*valsPerIteration );
                                        src += valsPerIteration;
                                        dst += 4;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using ( var srcp = _sharedParameters.GetIntPointer( e.SrcDefinition.PhysicalIndex ) )
                        using ( var dstp = _parameters.GetIntPointer( e.DstDefinition.PhysicalIndex ) )
                        {
                            var src = (int*)srcp.Pointer;
                            var dst = (int*)dstp.Pointer;

                            if ( e.DstDefinition.ElementSize == e.SrcDefinition.ElementSize )
                            {
                                // simple copy
                                Memcpy( dstp.Pointer, srcp.Pointer,
                                        sizeof ( int )*e.DstDefinition.ElementSize*e.DstDefinition.ArraySize );
                            }
                            else
                            {
                                // target params may be padded to 4 elements, shared params are packed
                                System.Diagnostics.Debug.Assert( e.DstDefinition.ElementSize%4 == 0 );
                                var iterations = e.DstDefinition.ElementSize/4
                                                 *e.DstDefinition.ArraySize;
                                var valsPerIteration = e.SrcDefinition.ElementSize/iterations;
                                for ( var l = 0; l < iterations; ++l )
                                {
                                    Memcpy( (IntPtr)dst, (IntPtr)src, sizeof ( int )*valsPerIteration );
                                    src += valsPerIteration;
                                    dst += 4;
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// </summary>
            [OgreVersion(1, 7, 2790)]
            protected void InitCopyData()
            {
                CopyDataList.Clear();
                var sharedMap = _sharedParameters.ConstantDefinitions.Map;
                foreach (var i in sharedMap )
                {
                    var name = i.Key;
                    var sharedDef = i.Value;


                    var instDef = _parameters.FindNamedConstantDefinition(name, false);
                    if ( instDef != null )
                    {
                        // Check that the definitions are the same
                        if ( instDef.ConstantType == sharedDef.ConstantType &&
                             instDef.ArraySize == sharedDef.ArraySize )
                        {
                            var e = new CopyDataEntry();
                            e.SrcDefinition = sharedDef;
                            e.DstDefinition = instDef;
                            CopyDataList.Add( e );
                        }
                    }
                }

                CopyDataVersion = _sharedParameters.Version;
            }
        }
    }
}
