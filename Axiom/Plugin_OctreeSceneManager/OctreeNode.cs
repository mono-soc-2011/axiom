using System;
using Axiom;
using Axiom.Core;
using Axiom.MathLib;
using Axiom.Graphics;

namespace Axiom.SceneManagers.Octree {
    /// <summary>
    /// Summary description for OctreeNode.
    /// </summary>
    public class OctreeNode : SceneNode {
        #region Member Variables
        protected static long green = 0xFFFFFFFF;

        protected ushort[] Indexes = {0,1,1,2,2,3,3,0,0,6,6,5,5,1,3,7,7,4,4,2,6,7,5,4};
        protected long[] Colors = {green, green, green, green, green, green, green, green };
        protected Octree octant = null;
        //protected SceneManager scene;
        protected AxisAlignedBox localAABB = new AxisAlignedBox();
        //protected OctreeSceneManager creator;

        protected System.Collections.ArrayList Children;
        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public AxisAlignedBox LocalAABB {
            get{
                return localAABB;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public Octree Octant {
            get{
                return octant;
            }
            set{
                octant = value;
            }
        }

        #endregion

        #region Methods

        public OctreeNode(SceneManager scene): base(scene) {}

        public OctreeNode(SceneManager scene, string name) : base(scene, name) {}

        /// <summary>
        ///     Remove all the children nodes as well from the octree.
        ///	</summary>
        public void RemoveNodeAndChildren() {
            int i;
						
            OctreeSceneManager man = (OctreeSceneManager)this.creator;
            man.RemoveOctreeNode(this);

            for(i=0;i<Children.Count;i++) {
                //OctreeNode child = (OctreeNode)Childern[i];
				
                OctreeNode child = (OctreeNode)RemoveChild(i);
                child.RemoveNodeAndChildren();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public override Node RemoveChild(int index) {
            OctreeNode child = (OctreeNode)base.RemoveChild(index);
            child.RemoveNodeAndChildren();
            return child;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override Node RemoveChild(string name) {
            OctreeNode child = (OctreeNode)base.RemoveChild(name);
            child.RemoveNodeAndChildren();
            return child;
        }

        /// <summary>
        ///     Determines if the center of this node is within the given box.
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        public bool IsInBox(AxisAlignedBox box) {
            Vector3 center = worldAABB.Maximum.MidPoint(worldAABB.Minimum);
            Vector3 min = box.Minimum;
            Vector3 max = box.Maximum;

            return (max > center && min < center);
        }

        /// <summary>
        ///     Adds all the attached scenenodes to the render queue.
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="queue"></param>
        public void AddToRenderQueue(Camera cam, RenderQueue queue) {
			
            int i;
            for(i=0;i<objectList.Count;i++) {
                SceneObject obj = (SceneObject)objectList[i];
                obj.NotifyCurrentCamera(cam);
			
                if(obj.IsVisible) {
                    obj.UpdateRenderQueue(queue);
                }
            }
        }

        /// <summary>
        ///     Same as SceneNode, only it doesn't care about children...
        /// </summary>
        protected override void UpdateBounds() {
            //update bounds from attached objects
            for(int i=0;i<objectList.Count;i++) {
                SceneObject obj = objectList[i];

                localAABB.Merge(obj.BoundingBox);

                worldAABB.Merge(obj.GetWorldBoundingBox(true));
            }

            if(!worldAABB.IsNull) {
                OctreeSceneManager oManager = (OctreeSceneManager)this.creator;
                oManager.UpdateOctreeNode(this);
            }
        }
    }
    #endregion
}