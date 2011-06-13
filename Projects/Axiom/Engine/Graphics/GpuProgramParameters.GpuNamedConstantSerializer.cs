using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Axiom.Serialization;

namespace Axiom.Graphics
{
	public partial class GpuProgramParameters
	{
        /// <summary>
        /// Simple class for loading / saving GpuNamedConstants
        /// </summary>
        public class GpuNamedConstantsSerializer : Serializer
        {
            public void ExportNamedConstants(GpuNamedConstants pConsts, string filename)
            {
#warning implement Endian.Native.
                ExportNamedConstants(pConsts, filename, Endian.Little);
            }

            public void ExportNamedConstants(GpuNamedConstants pConsts, string filename, Endian endianMode)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="pDest"></param>
            public void ImportNamedConstants(Stream stream, GpuNamedConstants pDest)
            {
                throw new NotImplementedException();
            }
        }
	}
}
