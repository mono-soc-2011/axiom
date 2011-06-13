using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Axiom.Graphics
{
    partial class GpuProgramParameters
    {
        

        [OgreVersion(1, 7, 2790)]
        public class GpuLogicalBufferStruct
        {
            [OgreVersion(1, 7, 2790)]
            public object Mutex 
            { 
                get
                {
                    return Map;
                }
            }

            /// <summary>
            /// Map from logical index to physical buffer location
            /// </summary>
            [OgreVersion(1, 7, 2790)]
            public readonly GpuLogicalIndexUseMap Map = new GpuLogicalIndexUseMap();

            /// Shortcut to know the buffer size needs
            [OgreVersion(1, 7, 2790)]
            public int BufferSize;
        };
    }
}
