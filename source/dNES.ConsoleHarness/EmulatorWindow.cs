using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace dNES.ConsoleHarness
{
    public class NesWindow : GameWindow
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private float _avgfps;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int ROWS = 240;
        private const int COLS = 256;
        private int _textureId;
        private Bitmap _bitmap;
        private BitmapData _bitmapData;

        public NesWindow()
            : base(256, 240, GraphicsMode.Default, "NES")
        {
            if (!Initialize())
            {
                Console.WriteLine("Error initializing, press any key to exit");
                Console.ReadKey();
                Exit();
            }
        }

        private bool Initialize()
        {
            bool retVal = false;
            try
            {
                GL.ClearColor(Color.Black);

                _bitmap = new Bitmap(COLS, ROWS);
                for (int i = 0; i < ROWS; i++)
                {
                    for (int j = 0; j < COLS; j++)
                    {
                        _bitmap.SetPixel(j, i, Color.Black);
                    }
                }
                _bitmapData = _bitmap.LockBits(new Rectangle(0, 0, COLS, ROWS), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                _textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _textureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, COLS, ROWS, 0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, _bitmapData.Scan0);

                _bitmap.UnlockBits(_bitmapData);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

                var handle = GetConsoleWindow();
#if DEBUG
                ShowWindow(handle, SW_SHOW);
#else
                ShowWindow(handle, SW_HIDE);
#endif
                retVal = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return retVal;
        }

        /// <summary>
        /// Get the input from the user
        /// </summary>
        /// <param name="e"></param>
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            try
            {
                // Gets the KeyboardState for this frame. KeyboardState allows us to check the status of keys.
                var input = Keyboard.GetState();

                Update(e.Time);
                Render(e.Time);
#if DEBUG

                // Check if the Escape button is currently being pressed.
                if (input.IsKeyDown(Key.Escape))
                {
                    // If it is, exit the window.
                    Exit();
                }
#endif
                base.OnUpdateFrame(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            base.OnResize(e);
        }

        protected override void OnUnload(EventArgs e)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            base.OnUnload(e);
        }

        /// <summary>
        /// Update the game logic
        /// </summary>
        /// <param name="dt">Time since last frame</param>
        private void Update(double dt)
        {
            try
            {
                _avgfps = (_avgfps + (1.0f / (float)dt)) / 2.0f;
                Title = string.Format("NES (FPS:{0:0.00})", _avgfps);

                _bitmapData = _bitmap.LockBits(new Rectangle(0, 0, COLS, ROWS), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, COLS, ROWS, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, _bitmapData.Scan0);

                int stride = _bitmapData.Stride;
                unsafe
                {
                    byte* ptr = (byte*)_bitmapData.Scan0.ToPointer();
                    try
                    {
                        for (int i = 0; i < ROWS; i++)
                        {
                            for (int j = 0; j < COLS; j++)
                            {
                                try
                                {
                                    ptr[(j * 3) + i * stride] = Color.CornflowerBlue.B; // blue value
                                    ptr[(j * 3) + i * stride + 1] = Color.CornflowerBlue.G; // green value
                                    ptr[(j * 3) + i * stride + 2] = Color.CornflowerBlue.R; // red value
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                _bitmap.UnlockBits(_bitmapData);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Render the game to the screen 
        /// </summary>
        /// <param name="dt">Time since last frame</param>
        private void Render(double dt)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);

            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.Begin(PrimitiveType.Quads);

            GL.TexCoord2(-1, 1); GL.Vertex2(-1, 1);
            GL.TexCoord2(-1, 1); GL.Vertex2(1, 1);
            GL.TexCoord2(-1, 1); GL.Vertex2(1, -1);
            GL.TexCoord2(-1, 1); GL.Vertex2(-1, -1);

            GL.End();
            GL.Flush();
            Context.SwapBuffers();
        }
    }
}