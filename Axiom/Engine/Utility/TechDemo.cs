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

using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using Axiom.Configuration;
using Axiom.Core;
using System.Windows.Forms;
using Axiom.Input;
using Axiom.Gui;
using Axiom.MathLib;
using Axiom.Graphics;
using MouseButtons = Axiom.Input.MouseButtons;

namespace Axiom.Utility {
    /// <summary>
    ///     Base class for Axiom examples.
    /// </summary>
    public abstract class TechDemo : IDisposable {
        #region Protected Fields
        protected Engine engine;
        protected Camera camera;
        protected Viewport viewport;
        protected SceneManager scene;
        protected RenderWindow window;
        protected IInputReader input;
        protected Vector3 cameraVector = Vector3.Zero;
        protected float cameraScale;
        protected bool showDebugOverlay = true;
        protected float statDelay = 0.0f;
        protected float debugTextDelay = 0.0f;
        #endregion Protected Fields

        #region Constructors & Destructors

        public TechDemo() {
            // set the global error handler for this applications thread of execution.
            Application.ThreadException += new ThreadExceptionEventHandler(GlobalErrorHandler);

            // add event handlers for frame events
            Engine.Instance.FrameStarted += new FrameEvent(OnFrameStarted);
            Engine.Instance.FrameEnded += new FrameEvent(OnFrameEnded);
        }

        #endregion Constructors & Destructors

        #region Protected Methods

        protected bool Configure() {
            // show the config dialog
            if(engine.ShowConfigDialog()) {
                window = engine.Initialize(true);
                engine.ShowDebugOverlay(showDebugOverlay);
                return true;
            }
            else {
                // cancel configuration
                return false;
            }
        }

        protected void CreateCamera() {
            // create a camera and initialize its position
            camera = scene.CreateCamera("MainCamera");
            camera.Position = new Vector3(0, 0, 500);
            camera.LookAt(new Vector3(0, 0, -300));

            // set the near clipping plane to be very close
            camera.Near = 5;
        }

        protected void TakeScreenshot(string fileName) {
            window.SaveToFile(fileName);
        }

        #endregion Protected Methods

        #region Protected Virtual Methods

        protected virtual void ChooseSceneManager() {
            // Get the SceneManager, a generic one by default
            // REFACTOR: Create SceneManagerFactories and have them register their supported type?
            scene = engine.SceneManagers[SceneType.Generic];
        }

        protected virtual void CreateViewports() {
            Debug.Assert(window != null, "Attempting to use a null RenderWindow.");

            // create a new viewport and set it's background color
            viewport = window.CreateViewport(camera, 0, 0, 100, 100, 100);
            viewport.BackgroundColor = ColorEx.Black;
        }

        protected virtual bool Setup() {
            // get a reference to the engine singleton
            engine = Engine.Instance;

            // allow for setting up resource gathering
            SetupResources();

            // setup the engine
            engine.Setup();

            //show the config dialog and collect options
            if(!Configure()) {
                // shutting right back down
                engine.Shutdown();
                
                return false;
            }

            ChooseSceneManager();
            CreateCamera();
            CreateViewports();

            // set default mipmap level
            TextureManager.Instance.DefaultNumMipMaps = 5;

            // call the overridden CreateScene method
            CreateScene();

            // retreive and initialize the input system
			input = PlatformManager.Instance.CreateInputReader();
            input.Initialize(window, null, true, true, false);

            return true;
        }

        protected virtual void SetupResources() {
            EngineConfig config = new EngineConfig();

            // load the config file
            // relative from the location of debug and releases executables
            config.ReadXml("EngineConfig.xml");

            // interrogate the available resource paths
            foreach(EngineConfig.FilePathRow row in config.FilePath) {
                string fullPath = Application.StartupPath + Path.DirectorySeparatorChar + row.src;

                switch(row.type) {
                    case "Folder":
                        ResourceManager.AddCommonSearchPath(fullPath);
                        break;
                    case "ZipFile":
                        ResourceManager.AddCommonArchive(fullPath, "ZipFile");
                        break;
                    default:
                        throw new Exception(string.Format("Archives of type '{0}' are not supported.", row.type));
                }
            }
        }

        #endregion Protected Virtual Methods

        #region Protected Abstract Methods

        /// <summary>
        /// 
        /// </summary>
        protected abstract void CreateScene();

        #endregion Protected Abstract Methods

        #region Public Methods

        public bool Start() {
            if(!Setup()) {
                return false;
            }

            // start the engines rendering loop
            engine.StartRendering();

            return true;
        }

        public void Dispose() {
            // remove event handlers
            engine.FrameStarted -= new FrameEvent(OnFrameStarted);
            engine.FrameEnded -= new FrameEvent(OnFrameEnded);
        }

        #endregion Public Methods

        #region Event Handlers
        protected virtual bool OnFrameEnded(Object source, FrameEventArgs e) {
            // do nothing by default
            return true;
        }

        protected virtual bool OnFrameStarted(Object source, FrameEventArgs e) {
            // reset the camera
            cameraVector.x = 0;
            cameraVector.y = 0;
            cameraVector.z = 0;

            // set the scaling of camera motion
            cameraScale = 100 * e.TimeSinceLastFrame;

            // TODO: Move this into an event queueing mechanism that is processed every frame
            input.Capture();

            if(input.IsKeyPressed(KeyCodes.Escape)) {
                // returning false from the FrameStart event will cause the engine's render loop to shut down
                Engine.Instance.Shutdown();
                return false;
            }

            if(input.IsKeyPressed(KeyCodes.A)) {
                cameraVector.x = -cameraScale;
            }

            if(input.IsKeyPressed(KeyCodes.D)) {
                cameraVector.x = cameraScale;
            }

            if(input.IsKeyPressed(KeyCodes.W)) {
                cameraVector.z = -cameraScale;
            }

            if(input.IsKeyPressed(KeyCodes.S)) {
                cameraVector.z = cameraScale;
            }

            if(input.IsKeyPressed(KeyCodes.Left)) {
                camera.Yaw(cameraScale);
            }

            if(input.IsKeyPressed(KeyCodes.Right)) {
                camera.Yaw(-cameraScale);
            }

            if(input.IsKeyPressed(KeyCodes.Up)) {
                camera.Pitch(cameraScale);
            }

            if(input.IsKeyPressed(KeyCodes.Down)) {
                camera.Pitch(-cameraScale);
            }

            if(input.IsKeyPressed(KeyCodes.T)) {
                camera.SceneDetail = SceneDetailLevel.Wireframe;
            }

            if(input.IsKeyPressed(KeyCodes.Y)) {
                camera.SceneDetail = SceneDetailLevel.Solid;
            }

            if(input.IsKeyPressed(KeyCodes.P)) {
                string[] temp = Directory.GetFiles(Environment.CurrentDirectory, "screenshot*.jpg");
                string fileName = string.Format("screenshot{0}.jpg", temp.Length + 1);
                
                // show briefly on the screen
                window.DebugText = string.Format("Wrote screenshot '{0}'.", fileName);

                TakeScreenshot(fileName);

                // show for 2 seconds
                debugTextDelay = 2.0f;
            }

            if(input.IsKeyPressed(KeyCodes.B)) {
                scene.ShowBoundingBoxes = !scene.ShowBoundingBoxes;
            }

            if(input.IsKeyPressed(KeyCodes.F)) {
                // hide all overlays, includes ones besides the debug overlay
                viewport.OverlaysEnabled = !viewport.OverlaysEnabled;
            }

            cameraVector.z += -input.RelativeMouseZ * 0.13f;

            if(!input.IsMousePressed(MouseButtons.Button0)) {
                float cameraYaw = -input.RelativeMouseX * 0.13f;
                float cameraPitch = -input.RelativeMouseY * 0.13f;
                
                camera.Yaw(cameraYaw);
                camera.Pitch(cameraPitch);
            } 
            else {
                cameraVector.x += input.RelativeMouseX * 0.13f;
            }

            // move the camera based on the accumulated movement vector
            camera.MoveRelative(cameraVector);

            // update performance stats once per second
            if(statDelay < 0.0f) {
                UpdateStats();
                statDelay = 1.0f;
            }
            else {
                statDelay -= e.TimeSinceLastFrame;
            }

            // turn off debug text when delay ends
            if(debugTextDelay < 0.0f) {
                debugTextDelay = 0.0f;
                window.DebugText = "";
            }
            else if(debugTextDelay > 0.0f) {
                debugTextDelay -= e.TimeSinceLastFrame;
            }

            return true;
        }

        protected void UpdateStats() {
            GuiElement element = GuiManager.Instance.GetElement("Core/CurrFps");
            element.Text = string.Format("Current FPS: {0}", Engine.Instance.CurrentFPS);

            element = GuiManager.Instance.GetElement("Core/BestFps");
            element.Text = string.Format("Best FPS: {0}", Engine.Instance.BestFPS);

            element = GuiManager.Instance.GetElement("Core/WorstFps");
            element.Text = string.Format("Worst FPS: {0}", Engine.Instance.WorstFPS);

            element = GuiManager.Instance.GetElement("Core/AverageFps");
            element.Text = string.Format("Average FPS: {0}", Engine.Instance.AverageFPS);

            element = GuiManager.Instance.GetElement("Core/NumTris");
            element.Text = string.Format("Triangle Count: {0}", scene.TargetRenderSystem.FacesRendered);

            element = GuiManager.Instance.GetElement("Core/DebugText");
            element.Text = window.DebugText;
        }

        public static void GlobalErrorHandler(Object source, ThreadExceptionEventArgs e) {
            // show the error
            MessageBox.Show("An exception has occured.  Please check the log file for more information.\n\nError:\n" + e.Exception.ToString(), "Exception!");

            // log the error
            //System.Diagnostics.Trace.WriteLine(e.Exception.ToString());
        }

        #endregion Event Handlers
    }
}
