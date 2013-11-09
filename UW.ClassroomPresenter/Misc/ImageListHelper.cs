using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace UW.ClassroomPresenter.Misc {
    // NOTE: Code from http://www.codeproject.com/cs/miscctrl/AlphaImageImagelist.asp
    //       Author: Narb M
    public class ImageListHelper {
        /// <summary>
        /// A BITMAPINFO stucture used to store an alphablended bitmap for adding to imagelist.
        /// </summary>
        [StructLayout( LayoutKind.Sequential )]
        private class BITMAPINFO {
            public Int32 biSize;
            public Int32 biWidth;
            public Int32 biHeight;
            public Int16 biPlanes;
            public Int16 biBitCount;
            public Int32 biCompression;
            public Int32 biSizeImage;
            public Int32 biXPelsPerMeter;
            public Int32 biYPelsPerMeter;
            public Int32 biClrUsed;
            public Int32 biClrImportant;
            public Int32 colors;
        };

        [DllImport( "comctl32.dll" )]
        private static extern bool ImageList_Add( IntPtr hImageList, IntPtr hBitmap, IntPtr hMask );
        [DllImport( "comctl32.dll" )]
        private static extern bool ImageList_Replace( IntPtr hImageList, int i, IntPtr hbmImage, IntPtr hbmMask );
        [DllImport( "kernel32.dll" )]
        private static extern bool RtlMoveMemory( IntPtr dest, IntPtr source, int dwcount );
        [DllImport( "gdi32.dll" )]
        private static extern IntPtr CreateDIBSection( IntPtr hdc, [In, MarshalAs( UnmanagedType.LPStruct )]BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset );
        [DllImport( "gdi32.dll" )]
        private static extern bool DeleteObject( IntPtr hObject );

        /// <summary>
        /// Add an entry to the given image list
        /// </summary>
        /// <param name="bm">The image</param>
        /// <param name="il">The image list</param>
        public static void Add( Bitmap bm, ImageList il ) {
            ImageListHelper.AddHelper( bm, il, false, 0 );
        }
        /// <summary>
        /// Replace an entry in the given image list
        /// </summary>
        /// <param name="bm">The image</param>
        /// <param name="index">The index to replace</param>
        /// <param name="il">The image list</param>
        public static void Replace( Bitmap bm, int index, ImageList il ) {
            ImageListHelper.AddHelper( bm, il, true, index );
        }

        /// <summary>
        /// Add or replace an entry in the given image list
        /// </summary>
        /// <param name="bm">The image</param>
        /// <param name="il">The image list to modify</param>
        /// <param name="replace">If true replace the existing index with the one given</param>
        /// <param name="replaceIndex">The replacement index</param>
        private static void AddHelper( Bitmap bm, ImageList il, bool replace, int replaceIndex ) {
            IntPtr hBitmap, ppvBits;
            BITMAPINFO bmi = new BITMAPINFO();
            
            // Resize the image to dimensions of imagelist before adding
            if( bm.Size != il.ImageSize ) {
                bm = new Bitmap( bm, il.ImageSize.Width, il.ImageSize.Height );
            }

            // Required due to the way bitmap is copied and read
            bmi.biSize = 40;            // Needed for RtlMoveMemory()
            bmi.biBitCount = 32;        // Number of bits
            bmi.biPlanes = 1;           // Number of planes
            bmi.biWidth = bm.Width;     // Width of our new bitmap
            bmi.biHeight = bm.Height;   // Height of our new bitmap
            bm.RotateFlip( RotateFlipType.RotateNoneFlipY );

            // Create our new bitmap
            hBitmap = CreateDIBSection( new IntPtr( 0 ), bmi, 0,
                      out ppvBits, new IntPtr( 0 ), 0 );
            
            // Copy the bitmap
            BitmapData bitmapData = bm.LockBits( new Rectangle( 0, 0,
                       bm.Width, bm.Height ), ImageLockMode.ReadOnly,
                       PixelFormat.Format32bppArgb );
            RtlMoveMemory( ppvBits, bitmapData.Scan0,
                           bm.Height * bitmapData.Stride );
            bm.UnlockBits( bitmapData );

            // Adds the new bitmap to the imagelist control or replaces the existing bitmap
            if( replace )
                ImageList_Replace( il.Handle, replaceIndex, hBitmap, new IntPtr( 0 ) );
            else
                ImageList_Add( il.Handle, hBitmap, new IntPtr( 0 ) );
        }
    }
}
