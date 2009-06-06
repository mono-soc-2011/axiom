//#define SIS
//#define XBOX360

#region Namespace Declarations

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Axiom.Core;
using Axiom.Overlays;
using Axiom.Math;
using Axiom.Graphics;
#if !( SIS )
using MouseButtons = Axiom.Input.MouseButtons;
using Axiom.Utilities;
using Axiom.Input;
using InputReader = Axiom.Input.InputReader;
#else
using InputReader = SharpInputSystem.InputManager;
#endif


#endregion Namespace Declarations

namespace Axiom.Demos
{
    /// <summary>
    ///     Base class for Axiom examples.
    /// </summary>
    public abstract class TechDemo : IDisposable
    {
        public delegate InputReader ConfigureInput();
        public ConfigureInput SetupInput;

        public TechDemo() { SetupInput += new ConfigureInput( _setupInput ); }

        #region Protected Fields

        protected Root engine;
        public Root Engine
        {
            get
            {
                return engine;
            }
            set
            {
                engine = value;
            }
        }
        protected Camera camera;
        protected Viewport viewport;
        protected SceneManager scene;
        protected RenderWindow window;
        public RenderWindow Window
        {
            get
            {
                return window;
            }
            set
            {
                window = value;
            }
        }
        protected InputReader input;
#if ( SIS )
        protected SharpInputSystem.Mouse mouse;
        protected SharpInputSystem.Keyboard keyboard;
#endif
        protected Vector3 cameraVector = Vector3.Zero;
        protected float cameraScale;
        protected bool showDebugOverlay = true;
        protected float statDelay = 0.0f;
        protected float debugTextDelay = 0.0f;
        protected string debugText = "";
        protected float toggleDelay = 0.0f;
        protected Vector3 camVelocity = Vector3.Zero;
        protected Vector3 camAccel = Vector3.Zero;
        protected float camSpeed = 2.5f;
        protected int aniso = 1;
        protected TextureFiltering filtering = TextureFiltering.Bilinear;
#if USE_CEGUI
        protected CeGui.Renderer guiRenderer = null;
        protected CeGui.GuiSheet rootGuiSheet = null;
#endif
        #endregion Protected Fields

        #region Protected Methods

        public virtual void CreateCamera()
        {
            // create a camera and initialize its position
            camera = scene.CreateCamera( "MainCamera" );
            camera.Position = new Vector3( 0, 0, 500 );
            camera.LookAt( new Vector3( 0, 0, -300 ) );

            // set the near clipping plane to be very close
            camera.Near = 5;

            camera.AutoAspectRatio = true;
        }

        /// <summary>
        ///    Shows the debug overlay, which displays performance statistics.
        /// </summary>
        protected void ShowDebugOverlay( bool show )
        {
            // gets a reference to the default overlay
            Overlay o = OverlayManager.Instance.GetByName( "Core/DebugOverlay" );

            if ( o == null )
            {
                LogManager.Instance.Write( string.Format( "Could not find overlay named '{0}'.", "Core/DebugOverlay" ) );
            }

            if ( show )
            {
                o.Show();
            }
            else
            {
                o.Hide();
            }
        }

        protected void TakeScreenshot( string fileName )
        {
            window.WriteContentsToFile( fileName );
        }

        #endregion Protected Methods

        #region Protected Virtual Methods

        public virtual void ChooseSceneManager()
        {
            // Get the SceneManager, a generic one by default
            scene = engine.CreateSceneManager( "DefaultSceneManager", "TechDemoSMInstance" );
            scene.ClearScene();
        }

        public virtual void CreateViewports()
        {
            Debug.Assert( window != null, "Attempting to use a null RenderWindow." );

            // create a new viewport and set it's background color
            viewport = window.AddViewport( camera, 0, 0, 1.0f, 1.0f, 100 );
            viewport.BackgroundColor = ColorEx.Black;
        }

        protected virtual bool Setup()
        {
            // instantiate the Root singleton
            //engine = new Root( "AxiomEngine.log" );
            engine = Root.Instance;

            // add event handlers for frame events
            engine.FrameStarted += new FrameEvent( OnFrameStarted );
            engine.FrameEnded += new FrameEvent( OnFrameEnded );

            window = Root.Instance.Initialize( true, "Axiom Engine Demo Window" );

            TechDemoListener rwl = new TechDemoListener( window );
            WindowEventMonitor.Instance.RegisterListener( window, rwl );

            ResourceGroupManager.Instance.InitializeAllResourceGroups();

            ShowDebugOverlay( showDebugOverlay );

            ChooseSceneManager();
            CreateCamera();
            CreateViewports();

            //CreateGUI();

            // set default mipmap level
            TextureManager.Instance.DefaultMipmapCount = 5;

            input = SetupInput();

            // call the overridden CreateScene method
            CreateScene();
            return true;
        }

        protected InputReader _setupInput()
        {
            InputReader ir = null;
#if  !( XBOX || XBOX360 ) && !( SIS )
            // retrieve and initialize the input system
            ir = PlatformManager.Instance.CreateInputReader();
            ir.Initialize( window, true, true, false, false );
#endif
#if ( SIS )
            SharpInputSystem.ParameterList pl = new SharpInputSystem.ParameterList();
            pl.Add( new SharpInputSystem.Parameter( "WINDOW", this.window.Handle ) );

            //Default mode is foreground exclusive..but, we want to show mouse - so nonexclusive
            pl.Add( new SharpInputSystem.Parameter( "w32_mouse", "CLF_BACKGROUND" ) );
            pl.Add( new SharpInputSystem.Parameter( "w32_mouse", "CLF_NONEXCLUSIVE" ) );

            //This never returns null.. it will raise an exception on errors
            ir = SharpInputSystem.InputManager.CreateInputSystem( pl );
            mouse = ir.CreateInputObject<SharpInputSystem.Mouse>( true, "" );
            keyboard = ir.CreateInputObject<SharpInputSystem.Keyboard>( true, "" );
#endif
            return ir;
        }

#if USE_CEGUI
        protected virtual void CreateGUI()
        {
            // Next, we need a renderer. CeGui is not bound to any graphics API and
            // the renderers are the glue that let CeGui interface with a graphics API
            // like OpenGL (in this case) or a 3D engine like Axiom.
            guiRenderer = new CeGui.Renderers.Axiom.Renderer( window, RenderQueueGroupID.Overlay, false, scene );

            // Initialize the CeGui system. This should be the first method called before
            // using any of the CeGui routines.
            CeGui.GuiSystem.Initialize( guiRenderer );

            // All graphics used by any CeGui themes are stored in image files that are mapped
            // by a special XML description which tells CeGui what can be found where on the
            // images. Obviously, we need to load these resources
            //
            // Note that it is possible, and even usual, for these steps to
            // be done automatically via a "scheme" definition, or even from the
            // cegui.conf configuration file, however for completeness, and as an
            // example, virtually everything is being done manually in this example
            // code.

            // Widget sets are collections of widgets that provide the widget classes defined
            // in CeGui (like PushButton, CheckBox and so on) with their own distinctive look
            // (like a theme) and possibly even custom behavior.
            //
            // Here we load all compiled widget sets we can find in the current directory. This
            // is done to demonstrate how you could add widget set dynamically to your
            // application. Other possibilities would be to hardcode the widget set an
            // application uses or determining the assemblies to load from a configuration file.
            string[] assemblyFiles = System.IO.Directory.GetFiles(
              System.IO.Directory.GetCurrentDirectory(), "CeGui.WidgetSets.*.dll"
            );
            foreach ( string assemblyFile in assemblyFiles )
            {
                CeGui.WindowManager.Instance.AttachAssembly(
                  System.Reflection.Assembly.LoadFile( assemblyFile )
                );
            }

            // Imagesets area a collection of named areas within a texture or image file. Each
            // area becomes an Image, and has a unique name by which it can be referenced. Note
            // that an Imageset would normally be specified as part of a scheme file, although
            // as this example is demonstrating, it is not a requirement.
            //
            // Again, we load all image sets we can find, this time searching the resources folder.
            string[] imageSetFiles = System.IO.Directory.GetFiles(
              System.IO.Directory.GetCurrentDirectory() + "\\Resources", "*.imageset"
            );

            foreach ( string imageSetFile in imageSetFiles )
                CeGui.ImagesetManager.Instance.CreateImageset( imageSetFile );

            // When the gui imagery side of thigs is set up, we should load in a font.
            // You should always load in at least one font, this is to ensure that there
            // is a default available for any gui element which needs to draw text.
            // The first font you load is automatically set as the initial default font,
            // although you can change the default later on if so desired.  Again, it is
            // possible to list fonts to be automatically loaded as part of a scheme, so
            // this step may not usually be performed explicitly.
            //
            // Fonts are loaded via the FontManager singleton.
            CeGui.FontManager.Instance.CreateFont( "Default", "Arial", 9, CeGui.FontFlags.None );
            CeGui.FontManager.Instance.CreateFont( "WindowTitle", "Arial", 12, CeGui.FontFlags.Bold );
            CeGui.GuiSystem.Instance.SetDefaultFont( "Default" );

            // The next thing we do is to set a default mouse cursor image.  This is
            // not strictly essential, although it is nice to always have a visible
            // cursor if a window or widget does not explicitly set one of its own.
            //
            // This is a bit hacky since we're assuming the SuaveLook image set, referenced
            // below, will always be available.
            CeGui.GuiSystem.Instance.SetDefaultMouseCursor(
              CeGui.ImagesetManager.Instance.GetImageset( "SuaveLook" ).GetImage( "Mouse-Arrow" )
            );

            // Now that the system is initialised, we can actually create some UI elements,
            // for this first example, a full-screen 'root' window is set as the active GUI
            // sheet, and then a simple frame window will be created and attached to it.
            //
            // All windows and widgets are created via the WindowManager singleton.
            CeGui.WindowManager winMgr = CeGui.WindowManager.Instance;

            // Here we create a "DefaultWindow". This is a native type, that is, it does not
            // have to be loaded via a scheme, it is always available. One common use for the
            // DefaultWindow is as a generic container for other windows. Its size defaults
            // to 1.0f x 1.0f using the relative metrics mode, which means when it is set as
            // the root GUI sheet window, it will cover the entire display. The DefaultWindow
            // does not perform any rendering of its own, so is invisible.
            //
            // Create a DefaultWindow called 'Root'.
            rootGuiSheet = winMgr.CreateWindow( "DefaultWindow", "Root" ) as CeGui.GuiSheet;

            // Set the GUI root window (also known as the GUI "sheet"), so the gui we set up
            // will be visible.
            CeGui.GuiSystem.Instance.GuiSheet = rootGuiSheet;


            // Add the dialog as child to the root gui sheet. The root gui sheet is the desktop
            // and we've just added a window to it, so the window will appear on the desktop.
            // Logical, right?
            rootGuiSheet.AddChild( new Gui.DebugRTTWindow(
                                    //new CeGui.WidgetSets.Suave.SuaveGuiBuilder()
                                    //new CeGui.WidgetSets.Taharez.TLGuiBuilder()
                                    new CeGui.WidgetSets.Windows.WLGuiBuilder()
                                ));
        }
#endif
        #endregion Protected Virtual Methods

        #region Protected Abstract Methods

        /// <summary>
        /// 
        /// </summary>
        public abstract void CreateScene();

        #endregion Protected Abstract Methods

        #region Public Methods

        public void Start()
        {
            try
            {
                if ( Setup() )
                {
                    // start the engines rendering loop
                    engine.StartRendering();
                }
            }
            catch ( Exception ex )
            {
                // try logging the error here first, before Root is disposed of
                if ( LogManager.Instance != null )
                {
                    LogManager.Instance.Write( LogManager.BuildExceptionString( ex ) );
                }
            }
        }

        public void Dispose()
        {
            if ( engine != null )
            {
                // remove event handlers
                engine.FrameStarted -= new FrameEvent( OnFrameStarted );
                engine.FrameEnded -= new FrameEvent( OnFrameEnded );

                //engine.Dispose();
            }
            scene.RemoveAllCameras();
            scene.RemoveCamera( camera );
            camera = null;
            Root.Instance.RenderSystem.DetachRenderTarget( window );
            window.Dispose();

            engine.Dispose();
        }

        #endregion Public Methods

        protected virtual bool OnFrameEnded( Object source, FrameEventArgs e )
        {
            return true;
        }

        protected virtual bool OnFrameStarted( Object source, FrameEventArgs e )
        {
            float scaleMove = 200 * e.TimeSinceLastFrame;

            // reset acceleration zero
            camAccel = Vector3.Zero;

            // set the scaling of camera motion
            cameraScale = 100 * e.TimeSinceLastFrame;

#if  !( XBOX || XBOX360 ) && !( SIS )
            // TODO: Move this into an event queueing mechanism that is processed every frame
            input.Capture();

            if ( input.IsKeyPressed( KeyCodes.Escape ) )
            {
                Root.Instance.QueueEndRendering();
                return false;
            }

            if ( input.IsKeyPressed( KeyCodes.A ) )
            {
                camAccel.x = -0.5f;
            }

            if ( input.IsKeyPressed( KeyCodes.D ) )
            {
                camAccel.x = 0.5f;
            }

            if ( input.IsKeyPressed( KeyCodes.W ) )
            {
                camAccel.z = -1.0f;
            }

            if ( input.IsKeyPressed( KeyCodes.S ) )
            {
                camAccel.z = 1.0f;
            }

            //camAccel.y += (float)( input.RelativeMouseZ * 0.1f );

            if ( input.IsKeyPressed( KeyCodes.Left ) )
            {
                camera.Yaw( cameraScale );
            }

            if ( input.IsKeyPressed( KeyCodes.Right ) )
            {
                camera.Yaw( -cameraScale );
            }

            if ( input.IsKeyPressed( KeyCodes.Up ) )
            {
                camera.Pitch( cameraScale );
            }

            if ( input.IsKeyPressed( KeyCodes.Down ) )
            {
                camera.Pitch( -cameraScale );
            }

            // subtract the time since last frame to delay specific key presses
            toggleDelay -= e.TimeSinceLastFrame;

            // toggle rendering mode
            if ( input.IsKeyPressed( KeyCodes.R ) && toggleDelay < 0 )
            {
                if ( camera.PolygonMode == PolygonMode.Points )
                {
                    camera.PolygonMode = PolygonMode.Solid;
                }
                else if ( camera.PolygonMode == PolygonMode.Solid )
                {
                    camera.PolygonMode = PolygonMode.Wireframe;
                }
                else
                {
                    camera.PolygonMode = PolygonMode.Points;
                }

                SetDebugText( String.Format( "Rendering mode changed to '{0}'.", camera.PolygonMode ) );

                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.T ) && toggleDelay < 0 )
            {
                // toggle the texture settings
                switch ( filtering )
                {
                    case TextureFiltering.Bilinear:
                        filtering = TextureFiltering.Trilinear;
                        aniso = 1;
                        break;
                    case TextureFiltering.Trilinear:
                        filtering = TextureFiltering.Anisotropic;
                        aniso = 8;
                        break;
                    case TextureFiltering.Anisotropic:
                        filtering = TextureFiltering.Bilinear;
                        aniso = 1;
                        break;
                }
                SetDebugText( String.Format( "Texture Filtering changed to '{0}'.", filtering ) );

                // set the new default
                MaterialManager.Instance.SetDefaultTextureFiltering( filtering );
                MaterialManager.Instance.DefaultAnisotropy = aniso;

                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.P ) && toggleDelay < 0 )
            {
                string[] temp = Directory.GetFiles( Environment.CurrentDirectory, "screenshot*.jpg" );
                string fileName = string.Format( "screenshot{0}.jpg", temp.Length + 1 );

                TakeScreenshot( fileName );

                // show on the screen for some seconds
                SetDebugText( string.Format( "Wrote screenshot '{0}'.", fileName ) );

                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.B ) && toggleDelay < 0 )
            {
                scene.ShowBoundingBoxes = !scene.ShowBoundingBoxes;

                SetDebugText( String.Format( "Bounding boxes {0}.", scene.ShowBoundingBoxes ? "visible" : "hidden" ) );

                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.F ) && toggleDelay < 0 )
            {
                // hide all overlays, includes ones besides the debug overlay
                viewport.ShowOverlays = !viewport.ShowOverlays;
                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.Comma ) && toggleDelay < 0 )
            {
                Root.Instance.MaxFramesPerSecond = 60;

                SetDebugText( String.Format( "Limiting framerate to {0} FPS.", Root.Instance.MaxFramesPerSecond ) );

                toggleDelay = .3f;
            }

            if ( input.IsKeyPressed( KeyCodes.Period ) && toggleDelay < 0 )
            {
                Root.Instance.MaxFramesPerSecond = 0;

                SetDebugText( String.Format( "Framerate limit OFF.", Root.Instance.MaxFramesPerSecond ) );

                toggleDelay = .3f;
            }
#endif
#if DEBUG
			if ( !input.IsMousePressed( MouseButtons.Left ) )
			{
				float cameraYaw = -input.RelativeMouseX * .13f;
				float cameraPitch = -input.RelativeMouseY * .13f;

				camera.Yaw( cameraYaw );
				camera.Pitch( cameraPitch );
			}
			else
			{
				// TODO unused
				cameraVector.x += input.RelativeMouseX * 0.13f;
			}
#endif
#if ( SIS )
            // TODO: Move this into an event queueing mechanism that is processed every frame
            mouse.Capture();
            keyboard.Capture();

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_ESCAPE ) )
            {
                Root.Instance.QueueEndRendering();

                return;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_A ) )
            {
                camAccel.x = -0.5f;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_D ) )
            {
                camAccel.x = 0.5f;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_W ) )
            {
                camAccel.z = -1.0f;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_S ) )
            {
                camAccel.z = 1.0f;
            }

            camAccel.y += (float)( mouse.MouseState.Z.Relative * 0.1f );

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_LEFT ) )
            {
                camera.Yaw( cameraScale );
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_RIGHT ) )
            {
                camera.Yaw( -cameraScale );
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_UP ) )
            {
                camera.Pitch( cameraScale );
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_DOWN ) )
            {
                camera.Pitch( -cameraScale );
            }

            // subtract the time since last frame to delay specific key presses
            toggleDelay -= e.TimeSinceLastFrame;

            // toggle rendering mode
            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_R ) && toggleDelay < 0 )
            {
                if ( camera.SceneDetail == SceneDetailLevel.Points )
                {
                    camera.SceneDetail = SceneDetailLevel.Solid;
                }
                else if ( camera.SceneDetail == SceneDetailLevel.Solid )
                {
                    camera.SceneDetail = SceneDetailLevel.Wireframe;
                }
                else
                {
                    camera.SceneDetail = SceneDetailLevel.Points;
                }

                Console.WriteLine( "Rendering mode changed to '{0}'.", camera.SceneDetail );

                toggleDelay = 1;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_T ) && toggleDelay < 0 )
            {
                // toggle the texture settings
                switch ( filtering )
                {
                    case TextureFiltering.Bilinear:
                        filtering = TextureFiltering.Trilinear;
                        aniso = 1;
                        break;
                    case TextureFiltering.Trilinear:
                        filtering = TextureFiltering.Anisotropic;
                        aniso = 8;
                        break;
                    case TextureFiltering.Anisotropic:
                        filtering = TextureFiltering.Bilinear;
                        aniso = 1;
                        break;
                }
                Console.WriteLine( "Texture Filtering changed to '{0}'.", filtering );

                // set the new default
                MaterialManager.Instance.SetDefaultTextureFiltering( filtering );
                MaterialManager.Instance.DefaultAnisotropy = aniso;

                toggleDelay = 1;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_P ) )
            {
                string[] temp = Directory.GetFiles( Environment.CurrentDirectory, "screenshot*.jpg" );
                string fileName = string.Format( "screenshot{0}.jpg", temp.Length + 1 );

                TakeScreenshot( fileName );

                // show briefly on the screen
                window.DebugText = string.Format( "Wrote screenshot '{0}'.", fileName );

                // show for 2 seconds
                debugTextDelay = 2.0f;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_B ) )
            {
                scene.ShowBoundingBoxes = !scene.ShowBoundingBoxes;
            }

            if ( keyboard.IsKeyDown( SharpInputSystem.KeyCode.Key_F ) )
            {
                // hide all overlays, includes ones besides the debug overlay
                viewport.OverlaysEnabled = !viewport.OverlaysEnabled;
            }

            if ( !mouse.MouseState.IsButtonDown( SharpInputSystem.MouseButtonID.Left ) )
            {
                float cameraYaw = -mouse.MouseState.X.Relative * .13f;
                float cameraPitch = -mouse.MouseState.Y.Relative * .13f;

                camera.Yaw( cameraYaw );
                camera.Pitch( cameraPitch );
            }
            else
            {
                cameraVector.x += mouse.MouseState.X.Relative * 0.13f;
            }

#endif
            camVelocity += ( camAccel * scaleMove * camSpeed );

            // move the camera based on the accumulated movement vector
            camera.MoveRelative( camVelocity * e.TimeSinceLastFrame );

            // Now dampen the Velocity - only if user is not accelerating
            if ( camAccel == Vector3.Zero )
            {
                camVelocity *= ( 1 - ( 6 * e.TimeSinceLastFrame ) );
            }


            // update performance stats once per second
            if ( statDelay < 0.0f && showDebugOverlay )
            {
                UpdateStats();
                statDelay = 1.0f;
            }
            else
            {
                statDelay -= e.TimeSinceLastFrame;
            }

            // turn off debug text when delay ends
            if ( debugTextDelay < 0.0f )
            {
                debugTextDelay = 0.0f;
                debugText = "";
            }
            else if ( debugTextDelay > 0.0f )
            {
                debugTextDelay -= e.TimeSinceLastFrame;
            }

            OverlayElement element = OverlayManager.Instance.Elements.GetElement( "Core/DebugText" );
            element.Text = debugText;

            return true;
        }

        protected void UpdateStats()
        {
            // TODO: Replace with CEGUI
            OverlayElement element = OverlayManager.Instance.Elements.GetElement( "Core/CurrFps" );
            element.Text = string.Format( "Current FPS: {0:#.00}", Root.Instance.CurrentFPS );

            element = OverlayManager.Instance.Elements.GetElement( "Core/BestFps" );
            element.Text = string.Format( "Best FPS: {0:#.00}", Root.Instance.BestFPS );

            element = OverlayManager.Instance.Elements.GetElement( "Core/WorstFps" );
            element.Text = string.Format( "Worst FPS: {0:#.00}", Root.Instance.WorstFPS );

            element = OverlayManager.Instance.Elements.GetElement( "Core/AverageFps" );
            element.Text = string.Format( "Average FPS: {0:#.00}", Root.Instance.AverageFPS );

            element = OverlayManager.Instance.Elements.GetElement( "Core/NumTris" );
            element.Text = string.Format( "Triangle Count: {0}", scene.TargetRenderSystem.FacesRendered );

            element = OverlayManager.Instance.Elements.GetElement("Core/NumBatches");
            element.Text = string.Format("Batch Count: {0}", scene.TargetRenderSystem.BatchesRendered);
        }

        /// <summary>
        /// Show a text message on screen for two seconds.
        /// </summary>
        /// <param name="text"></param>
        protected void SetDebugText( string text )
        {
            SetDebugText( text, 2.0f );
        }

        /// <summary>
        /// Show a text message on screen for the specified amount of time.
        /// </summary>
        /// <param name="text">Text to show</param>
        /// <param name="delay">Duration in seconds</param>
        protected void SetDebugText( string text, float delay )
        {
            debugText = text;
            debugTextDelay = delay;
        }

    }

    public class TechDemoListener : IWindowEventListener
    {
        private RenderWindow _mw;
        public TechDemoListener( RenderWindow mainWindow )
        {
            Contract.RequiresNotNull( mainWindow, "mainWindow" );

            _mw = mainWindow;
        }

        /// <summary>
        /// Window has moved position
        /// </summary>
        /// <param name="rw">The RenderWindow which created this event</param>
        public void WindowMoved( RenderWindow rw )
        {
        }

        /// <summary>
        /// Window has resized
        /// </summary>
        /// <param name="rw">The RenderWindow which created this event</param>
        public void WindowResized( RenderWindow rw )
        {
        }

        /// <summary>
        /// Window has closed
        /// </summary>
        /// <param name="rw">The RenderWindow which created this event</param>
        public void WindowClosed( RenderWindow rw )
        {
            Contract.RequiresNotNull( rw, "RenderWindow" );

            // Only do this for the Main Window
            if ( rw == _mw )
            {
                Root.Instance.QueueEndRendering();
            }
        }

        /// <summary>
        /// Window lost/regained the focus
        /// </summary>
        /// <param name="rw">The RenderWindow which created this event</param>
        public void WindowFocusChange( RenderWindow rw )
        {
            Contract.RequiresNotNull( rw, "RenderWindow" );

            if ( !rw.IsActive )
                rw.IsActive = true;
        }

    }

}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               