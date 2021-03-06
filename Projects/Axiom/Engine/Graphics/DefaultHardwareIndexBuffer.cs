﻿#region LGPL License

/*
Axiom Graphics Engine Library
Copyright (C) 2003-2010 Axiom Project Team

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
//     <id value="$Id:$"/>
// </file>
#endregion SVN Version Information

#region Namespace Declarations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Axiom.Core;

#endregion Namespace Declarations

namespace Axiom.Graphics
{
	public class DefaultHardwareIndexBuffer : HardwareIndexBuffer
	{
		private readonly byte[] _mpData;

		public DefaultHardwareIndexBuffer( IndexType idxType, int numIndexes, BufferUsage usage )
			: base( null, idxType, numIndexes, usage, true, false )
		{
			_mpData = new byte[ sizeInBytes ];
		}

		public DefaultHardwareIndexBuffer( HardwareBufferManagerBase manager, IndexType idxType, int numIndexes, BufferUsage usage )
			: base( manager, idxType, numIndexes, usage, true, false )
		{
			_mpData = new byte[ sizeInBytes ];
		}

        public override void ReadData(int offset, int length, IntPtr dest)
        {
            var data = Memory.PinObject(_mpData);
            Memory.Copy(dest, data, length);
            Memory.UnpinObject(_mpData);
        }

        public override void WriteData(int offset, int length, Array data, bool discardWholeBuffer)
        {
            var pSource = Memory.PinObject(data);
            var pIntData = Memory.PinObject(_mpData);
            Memory.Copy(pSource, pIntData, length);
            Memory.UnpinObject(data);
            Memory.UnpinObject(_mpData);
        }

        public override void WriteData(int offset, int length, IntPtr src, bool discardWholeBuffer)
        {
            var pIntData = Memory.PinObject(_mpData);
            Memory.Copy(src, pIntData, length);
            Memory.UnpinObject(_mpData);
        }

        public override IntPtr Lock(int offset, int length, BufferLocking locking)
        {
            Debug.Assert(!isLocked);
            isLocked = true;
            var ret = Memory.PinObject(_mpData);
            unsafe
            {
                var v = (byte*)ret.ToPointer();
                v += offset;
                ret = new IntPtr(v);
            }
            return ret;
        }

        protected override IntPtr LockImpl(int offset, int length, BufferLocking locking)
        {
            Debug.Assert(!isLocked);
            isLocked = true;
            var ret = Memory.PinObject(_mpData);
            unsafe
            {
                var v = (byte*)ret.ToPointer();
                v += offset;
                ret = new IntPtr(v);
            }
            return ret;
        }

        public override void Unlock()
        {
            Debug.Assert(isLocked);
            Memory.UnpinObject(_mpData);
            isLocked = false;
        }

        protected override void UnlockImpl()
        {
            Debug.Assert(isLocked);
            Memory.UnpinObject(_mpData);
            isLocked = false;
        }
	}
}
