using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CHIP_8
{
    public partial class Form1 : Form
    {

        private CPU cpu;

        private Bitmap screen;

        private Thread runner;

        private bool paused = false;
        private bool runTask = false;
        private string lastRomLoaded = "";

        /**
            Keypad                   Keyboard
            +-+-+-+-+                +-+-+-+-+
            |1|2|3|C|                |1|2|3|4|
            +-+-+-+-+                +-+-+-+-+
            |4|5|6|D|                |Q|W|E|R|
            +-+-+-+-+       =>       +-+-+-+-+
            |7|8|9|E|                |A|S|D|F|
            +-+-+-+-+                +-+-+-+-+
            |A|0|B|F|                |Z|X|C|V|
            +-+-+-+-+                +-+-+-+-+ 
         */
        private Dictionary<Keys, byte> keyMapping = new Dictionary<Keys, byte>
        {
            { Keys.D1, 0x1 },
            { Keys.D2, 0x2 },
            { Keys.D3, 0x3 },
            { Keys.D4, 0xC },
            { Keys.Q, 0x4 },
            { Keys.W, 0x5 },
            { Keys.E, 0x6 },
            { Keys.R, 0xD },
            { Keys.A, 0x7 },
            { Keys.S, 0x8 },
            { Keys.D, 0x9 },
            { Keys.F, 0xE },
            { Keys.Z, 0xA },
            { Keys.X, 0x0 },
            { Keys.C, 0xB },
            { Keys.V, 0xF },
        };

        public Form1()
        {
            InitializeComponent();

            cpu = new CPU();
            cpu.Init();

            screen = new Bitmap(64, 32);
            pictureBox.Image = screen;

            ClearScreen();

            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyUp += new KeyEventHandler(Form1_KeyUp);

            openToolStripMenuItem.Click += new EventHandler(Form1_OpenMenuClick);
            resetToolStripMenuItem.Click += new EventHandler(Form1_ResetRomMenuClick);
            resetEmulatorToolStripMenuItem.Click += new EventHandler(Form1_ResetMenuClick);
            pauseToolStripMenuItem.Click += new EventHandler(Form1_PauseMenuClick);
        }

        void Form1_PauseMenuClick(object sender, System.EventArgs e)
        {
            paused = !paused;
            pauseToolStripMenuItem.Checked = paused;
        }
        
        void UnPause()
        {
            if (paused)
            {
                paused = false;
                pauseToolStripMenuItem.Checked = false;
            }
        }

        void Form1_OpenMenuClick(object sender, System.EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.DefaultExt = "*.c8";
            if (openFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StopAndWaitForRunnerThread();
                cpu.Init();
                cpu.LoadGame(openFile.FileName);
                lastRomLoaded = openFile.FileName;
                UnPause();
                CreateRunnerThread();
            }
        }

        void Form1_ResetRomMenuClick(object sender, System.EventArgs e)
        {
            if (lastRomLoaded.Length == 0)
            {
                return;
            }

            StopAndWaitForRunnerThread();

            cpu.Init();
            ClearScreen();
            cpu.LoadGame(lastRomLoaded);

            CreateRunnerThread();
        }
        
        void Form1_ResetMenuClick(object sender, System.EventArgs e)
        {
            StopAndWaitForRunnerThread();
            cpu.Init();
            ClearScreen();
        }

        void StopAndWaitForRunnerThread()
        {
            runTask = false;

            if (runner != null)
            {
                runner.Join();
            }

            runTask = true;
        }

        void CreateRunnerThread()
        {
            runner = new Thread(new ThreadStart(Run))
            {
                IsBackground = true
            };
            runner.Start();
        }

        void Run()
        {
            runTask = true;

            while (runTask)
            {
                if (paused)
                {
                    Thread.Sleep(3);
                    continue;
                }

                cpu.Cycle();
                if (cpu.DrawFlag)
                {
                    Draw();
                }
                Thread.Sleep(1);                
            }
        }

        void Draw()
        {
            Bitmap newScreen = (Bitmap) screen.Clone();
            
            for (var y = 0; y < CPU.height; y++)
            {
                for (var x = 0; x < CPU.width; x++)
                {
                    if (cpu.Graphics[x, y] == 0)
                    {                        
                        newScreen.SetPixel(x, y, Color.Black);
                    }
                    else
                    {
                        newScreen.SetPixel(x, y, Color.Beige);
                    }
                }
            }

            pictureBox.Image = newScreen;            
            pictureBox.Invalidate();
        }

        void ClearScreen()
        {
            Bitmap newScreen = (Bitmap)screen.Clone();

            for (var y = 0; y < CPU.height; y++)
            {
                for (var x = 0; x < CPU.width; x++)
                {
                    newScreen.SetPixel(x, y, Color.DarkGray);                    
                }
            }

            pictureBox.Image = newScreen;
            pictureBox.Invalidate();
        }

        void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (keyMapping.ContainsKey(e.KeyCode))
            {
                cpu.KeyReleased(keyMapping[e.KeyCode]);
            }
        }

        void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (keyMapping.ContainsKey(e.KeyCode))
            {
                cpu.KeyPressed(keyMapping[e.KeyCode]);
            }
        }

    }
}
