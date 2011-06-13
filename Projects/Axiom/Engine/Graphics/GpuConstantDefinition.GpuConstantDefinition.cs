using System;

namespace Axiom.Graphics
{
    /// <summary>
    /// Information about predefined program constants.
    /// </summary>
    /// <note>
    /// Only available for high-level programs but is referenced generically
    /// by GpuProgramParameters.
    /// </note>
    public class GpuConstantDefinition
    {
        /// <summary>
        /// Data type.
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public GpuProgramParameters.GpuConstantType ConstantType;

        /// <summary>
        /// Physical start index in buffer (either float or int buffer)
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public int PhysicalIndex;

        /// <summary>
        /// Logical index - used to communicate this constant to the rendersystem
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public int LogicalIndex;

        /// <summary>
        /// Number of raw buffer slots per element
        /// (some programs pack each array element to float4, some do not)
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public int ElementSize;

        /// <summary>
        /// Length of array
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public int ArraySize;

        /// <summary>
        /// How this parameter varies (bitwise combination of GpuParamVariability)
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public GpuProgramParameters.GpuParamVariability Variability;

        /// <summary>
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public bool IsFloat
        {
            get
            {
                return IsFloatConst( ConstantType );
            }
        }

        /// <summary>
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public bool IsSampler
        {
            get
            {
                return IsSamplerConst( ConstantType );
            }
        }

        /// <summary>
        /// </summary>
        [OgreVersion(1, 7, 2790)]
        public GpuConstantDefinition()
        {
            ConstantType = GpuProgramParameters.GpuConstantType.Unknown;
            PhysicalIndex = Int32.MaxValue;
            ElementSize = 0;
            ArraySize = 1;
            Variability = GpuProgramParameters.GpuParamVariability.Global;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// true when the curent ConstantType is a float based type
        /// </returns>
        [OgreVersion(1, 7, 2790)]
        public static bool IsFloatConst( GpuProgramParameters.GpuConstantType c )
        {
            switch (c)
            {
                case GpuProgramParameters.GpuConstantType.Int1:
                case GpuProgramParameters.GpuConstantType.Int2:
                case GpuProgramParameters.GpuConstantType.Int3:
                case GpuProgramParameters.GpuConstantType.Int4:
                case GpuProgramParameters.GpuConstantType.Sampler1D:
                case GpuProgramParameters.GpuConstantType.Sampler2D:
                case GpuProgramParameters.GpuConstantType.Sampler3D:
                case GpuProgramParameters.GpuConstantType.SamplerCube:
                case GpuProgramParameters.GpuConstantType.Sampler1DShadow:
                case GpuProgramParameters.GpuConstantType.Sampler2DShadow:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// true when the curent ConstantType is an int based type
        /// </returns>
        [OgreVersion(1, 7, 2790)]
        public bool IsSamplerConst( GpuProgramParameters.GpuConstantType c )
        {
            switch ( c )
            {
                case GpuProgramParameters.GpuConstantType.Sampler1D:
                case GpuProgramParameters.GpuConstantType.Sampler2D:
                case GpuProgramParameters.GpuConstantType.Sampler3D:
                case GpuProgramParameters.GpuConstantType.SamplerCube:
                case GpuProgramParameters.GpuConstantType.Sampler1DShadow:
                case GpuProgramParameters.GpuConstantType.Sampler2DShadow:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctype"></param>
        /// <param name="padToMultiplesOf4"></param>
        /// <returns></returns>
        [OgreVersion(1, 7, 2790)]
        public static int GetElementSize( GpuProgramParameters.GpuConstantType ctype, bool padToMultiplesOf4 )
        {
            if ( padToMultiplesOf4 )
            {
                switch ( ctype )
                {
                    case GpuProgramParameters.GpuConstantType.Float1:
                    case GpuProgramParameters.GpuConstantType.Float2:
                    case GpuProgramParameters.GpuConstantType.Float3:
                    case GpuProgramParameters.GpuConstantType.Float4:
                    case GpuProgramParameters.GpuConstantType.Int1:
                    case GpuProgramParameters.GpuConstantType.Int2:
                    case GpuProgramParameters.GpuConstantType.Int3:
                    case GpuProgramParameters.GpuConstantType.Int4:
                    case GpuProgramParameters.GpuConstantType.Sampler1D:
                    case GpuProgramParameters.GpuConstantType.Sampler2D:
                    case GpuProgramParameters.GpuConstantType.Sampler3D:
                    case GpuProgramParameters.GpuConstantType.Sampler1DShadow:
                    case GpuProgramParameters.GpuConstantType.Sampler2DShadow:
                    case GpuProgramParameters.GpuConstantType.SamplerCube:
                        return 4;
                    case GpuProgramParameters.GpuConstantType.Matrix_2X2:
                    case GpuProgramParameters.GpuConstantType.Matrix_2X3:
                    case GpuProgramParameters.GpuConstantType.Matrix_2X4:
                        return 8; // 2 float4s
                    case GpuProgramParameters.GpuConstantType.Matrix_3X2:
                    case GpuProgramParameters.GpuConstantType.Matrix_3X3:
                    case GpuProgramParameters.GpuConstantType.Matrix_3X4:
                        return 12; //3 float4s
                    case GpuProgramParameters.GpuConstantType.Matrix_4X2:
                    case GpuProgramParameters.GpuConstantType.Matrix_4X3:
                    case GpuProgramParameters.GpuConstantType.Matrix_4X4:
                        return 16; //4 float4s
                    default:
                        return 4;
                }
            }
            // else
            {
                switch ( ctype )
                {
                    case GpuProgramParameters.GpuConstantType.Float1:
                    case GpuProgramParameters.GpuConstantType.Int1:
                    case GpuProgramParameters.GpuConstantType.Sampler1D:
                    case GpuProgramParameters.GpuConstantType.Sampler2D:
                    case GpuProgramParameters.GpuConstantType.Sampler3D:
                    case GpuProgramParameters.GpuConstantType.Sampler1DShadow:
                    case GpuProgramParameters.GpuConstantType.Sampler2DShadow:
                    case GpuProgramParameters.GpuConstantType.SamplerCube:
                        return 1;
                    case GpuProgramParameters.GpuConstantType.Float2:
                    case GpuProgramParameters.GpuConstantType.Int2:
                        return 2;
                    case GpuProgramParameters.GpuConstantType.Float3:
                    case GpuProgramParameters.GpuConstantType.Int3:
                        return 3;
                    case GpuProgramParameters.GpuConstantType.Float4:
                    case GpuProgramParameters.GpuConstantType.Int4:
                        return 4;
                    case GpuProgramParameters.GpuConstantType.Matrix_2X2:
                        return 4;
                    case GpuProgramParameters.GpuConstantType.Matrix_2X3:
                    case GpuProgramParameters.GpuConstantType.Matrix_3X2:
                        return 6;
                    case GpuProgramParameters.GpuConstantType.Matrix_2X4:
                    case GpuProgramParameters.GpuConstantType.Matrix_4X2:
                        return 8;
                    case GpuProgramParameters.GpuConstantType.Matrix_3X3:
                        return 9;
                    case GpuProgramParameters.GpuConstantType.Matrix_3X4:
                    case GpuProgramParameters.GpuConstantType.Matrix_4X3:
                        return 12;
                    case GpuProgramParameters.GpuConstantType.Matrix_4X4:
                        return 16;
                    default:
                        return 4;
                }
            }
        }
    }
}