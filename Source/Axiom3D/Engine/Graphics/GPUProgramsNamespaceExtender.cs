#region LGPL License
/*
Axiom Game Engine Library
Copyright (C) 2003  Axiom Project Team

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

#region Namespace Declarations
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
#endregion

namespace Axiom
{
    public class GPUProgramsNamespaceExtender : INamespaceExtender
    {
        public IEnumerable<K> Subtree<K>()
        {
            if (typeof(K).GetInterface("IHighLevelGpuProgramFactory") == null)
                throw new ArgumentOutOfRangeException("GPUProgramsNamespaceExtender supports only IHighLevelGpuProgramFactory-compatible instances");

            IEnumerator
                enu = HighLevelGpuProgramManager.Instance.Resources.GetEnumerator();

            while (enu.MoveNext())
                yield return (K)enu.Current;
        }

        const string 
            NAMESPACE_NAME = "/Axiom/GPUPrograms/";

        public string Namespace
        {
            get 
            { 
                return NAMESPACE_NAME; 
            }
        }

        public K GetObject<K>(string objectName)
        {
            if (typeof(K).GetInterface("IHighLevelGpuProgramFactory") == null)
                throw new ArgumentOutOfRangeException("GPUProgramsNamespaceExtender supports only IHighLevelGpuProgramFactory-compatible instances");

            return (K)((object)GpuProgramManager.Instance.GetByName(objectName));
        }
    }
}