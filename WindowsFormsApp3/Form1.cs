using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace WindowsFormsApp3
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            using var mod = SkyrimMod.CreateFromBinaryOverlay(@"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm", SkyrimRelease.SkyrimSE);
            var worldspace = mod.Worldspaces.First();
            FindMinMax(worldspace);
        }

        public (float, float) FindMinMax(IWorldspaceGetter worldspace)
        {
            float minheight = float.MaxValue;
            float maxheight = float.MaxValue;
            int mincellx = int.MaxValue;
            int maxcellx = int.MaxValue;
            int mincelly = int.MaxValue;
            int maxcelly = int.MaxValue;
            /*
            foreach (var block in worldspace.SubCells)
            {
                foreach (var subblock in block.Items)
                {
                    foreach (var subcell in subblock.Items)
                    {
                        var land = subcell.Landscape;
                        float[,] heightmap = ParseHeights((Noggog.ReadOnlyMemorySlice<byte>)land.VertexHeightMap);
                        foreach (var pos in heightmap)
                        {
                            if (minheight == float.MaxValue || minheight > pos)
                            {
                                minheight = pos;
                            }
                            if (maxheight == float.MaxValue || maxheight < pos)
                            {
                                maxheight = pos;
                            }

                            if (maxcellx == int.MaxValue || maxcellx < subcell.Grid.Point.X)
                            {
                                maxcellx = subcell.Grid.Point.X;
                            }

                            if (mincellx == int.MaxValue || mincellx > subcell.Grid.Point.X)
                            {
                                mincellx = subcell.Grid.Point.X;
                            }

                            if (maxcelly == int.MaxValue || maxcelly < subcell.Grid.Point.Y)
                            {
                                maxcelly = subcell.Grid.Point.Y;
                            }

                            if (mincelly == int.MaxValue || mincelly > subcell.Grid.Point.Y)
                            {
                                mincelly = subcell.Grid.Point.Y;
                            }
                        }

                        //txtDebug.Text += subcell.EditorID;

                        if (subcell.EditorID == "Whiterun")
                        {
                            txtDebug.Text += "Woop, whiterun";
                            CreateFromCell(heightmap);
                        }
                    }
                }
                //CreateFromBlock(block);
            }*/
            //txtDebug.Text += "Min cell X: " + mincellx + " Max cell X: " + maxcellx + " Min cell y: " + mincelly + " Max cell y: " + maxcelly;
            //            txtDebug.Text += "Minheight: " + minheight + " Maxheight: " + maxheight + "\r\n";
            CreateFromWorld(worldspace);
            return (minheight, maxheight);
        }

        
        void CreateFromWorld(IWorldspaceGetter wrld)
        {
            const int cellsize = 32; // Each cell has 32 x 32 datapoints. Some of those are double, which we don't care about, it will just look more gridlike.
            const int maxheight = 9771;  // -4842 is the lowest point of the map. 9771 is the total max
            const int minheight = 4842; // These are just used to calculate the color of each pixel later.
            const int worldwidthincells = 118; // How many cells the world is in X and Y
            const int worldheightincells = 95;

            Bitmap bitmap = new Bitmap(cellsize * worldwidthincells, cellsize * worldheightincells, System.Drawing.Imaging.PixelFormat.Format48bppRgb);
            const int pixelsize = 6; // Each pixel is 6 bytes long
            byte[] outBuffer = new byte[bitmap.Width * bitmap.Height * pixelsize];

            foreach (var block in wrld.SubCells)
            {
                foreach(var subblock in block.Items)
                {
                    bool black = false;
                    foreach (var cell in subblock.Items)
                    {
                        if (cell.Grid == null) continue;
                        int cell_x_normalized = cell.Grid.Point.X + 57; // -57 and -43 are the lowest numbers respectively
                        int cell_y_normalized = cell.Grid.Point.Y + 43;
                        if (!cell.Landscape.TryGet(out var land)
                            || land.VertexHeightMap == null) continue;
                        float[,] heightmap = ParseHeights(land.VertexHeightMap.Value);
                        for (int y = 0; y < 32; y++)
                        {
                            int rowoffsetbytes = (
                                                    (cell_y_normalized * bitmap.Width * cellsize)
                                                    + (y * bitmap.Width)
                                                    + (cell_y_normalized != 0 ? -bitmap.Width : 0)
                                                 ) * pixelsize; // this is the offset, in bytes, to find the correct row in the buffer


                            for (int x = 0; x < 32; x++)
                            {
                                // Decide the color of the pixel
                                float percent = (heightmap[y, x] + minheight) / maxheight;
                                ushort color = (ushort)(ushort.MaxValue * percent);
                                //ushort color = ushort.MaxValue / 2; //
                                byte[] snippet = BitConverter.GetBytes(color).Concat(BitConverter.GetBytes(color)).Concat(BitConverter.GetBytes(color)).ToArray(); // converted to byte array

                                // Figure out what position to put it in
                                                      
                                int column = (
                                                (cell_x_normalized * cellsize)
                                                + x
                                                + (cell_x_normalized != 0 ? -1 : 0)
                                             ) *pixelsize; // offset, in positions, in the row
                                int position = rowoffsetbytes + column;
 //                               if(position < outBuffer.Length)
 //                               {
                                    
                                snippet.CopyTo(outBuffer, position);
//                                }
                            }
                        }
                        black = !black;
                    }
                    
                }
            }

            // Lock the unmanaged bits for efficient writing.
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            // Bulk copy pixel data from a byte array:
            Marshal.Copy(outBuffer, 0, data.Scan0, outBuffer.Length);

            // When finished, unlock the unmanaged bits
            bitmap.UnlockBits(data);

            pictureBox.Image = bitmap;
            bitmap.Save(@"C:\debug\tamriel.bmp");
        }

        public float[,] ParseHeights(ReadOnlyMemorySlice<byte> input_in)
        {
            byte[] input = input_in.ToArray();
            float[,] returner = new float[32, 32];
            float offset = BitConverter.ToSingle(input, 0);
            float row_offset = 0;
            for (int r = 0; r < 32; r++)
            {
                row_offset = 0;
                for (int c = 0; c < 32; c++)
                {
                    sbyte pos = (sbyte)input[r * 32 + c];
                    row_offset += pos;
                    returner[r, 31-c] = offset + row_offset;
                }
            }
            return returner;
        }
    }
}
