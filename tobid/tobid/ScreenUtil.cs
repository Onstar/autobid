﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

using System.Runtime.InteropServices;

namespace tobid.util
{
    public enum MouseEventFlags
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp = 0x0040,
        Wheel = 0x0800,
        Absolute = 0x8000
    }

    class ScreenUtil
    {
        [DllImport("User32.dll")]
        public extern static System.IntPtr GetDC(System.IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public static IDictionary<string, byte> keycode = new Dictionary<string, byte>();
        static ScreenUtil(){
            keycode.Add("0", 48);
            keycode.Add("1", 49);
            keycode.Add("2", 50);
            keycode.Add("3", 51);
            keycode.Add("4", 52);
            keycode.Add("5", 53);
            keycode.Add("6", 54);
            keycode.Add("7", 55);
            keycode.Add("8", 56);
            keycode.Add("9", 57);
            keycode.Add("BACKSPACE", 0x8);
            keycode.Add("DELETE", 0x2e);
            keycode.Add("+", 48);
        }

        public void drawSomething(int x, int y, String something)
        {
            System.IntPtr DesktopHandle = GetDC(System.IntPtr.Zero);
            Graphics g = Graphics.FromHdc(DesktopHandle);
            //g.DrawRectangle(new Pen(Color.Red), new Rectangle(10, 10, 100, 100));

            SolidBrush brush = new SolidBrush(Color.Red);
            Font font = new System.Drawing.Font("黑体",16);
            g.DrawString(something, font, brush, new PointF(x, y));
        }

        public void screenCapture(int x, int y, int width, int height){

            Bitmap image = new Bitmap(width, height);
            Graphics imgGraphics = Graphics.FromImage(image);
            imgGraphics.CopyFromScreen(x, y, 0, 0, new Size(width,height));
            //image.Save("e:\\xxx.jpg", ImageFormat.Jpeg);
        }

        public byte[] screenCaptureAsByte(int x, int y, int width, int height)
        {
            Bitmap image = new Bitmap(width, height);
            Graphics imgGraphics = Graphics.FromImage(image);
            imgGraphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            MemoryStream ms = new MemoryStream();
            image.Save(ms, ImageFormat.Bmp);
            //image.Save("e:\\xxx.bmp", ImageFormat.Bmp);
            byte[] bytes = ms.GetBuffer();
            ms.Close();
            return bytes;
        }

        public Bitmap subImage(Bitmap bitmap){
            
            Point point = scan(bitmap);
            Rectangle cloneRect = new Rectangle(point.X, point.Y, bitmap.Width - point.X, bitmap.Height - point.Y);
            Bitmap subImg = bitmap.Clone(cloneRect, bitmap.PixelFormat);
            return subImg;
        }

        public static Boolean isWhite(Color color)
        {
            float brightness = color.GetBrightness();
            float hue = color.GetHue();
            float saturation = color.GetSaturation();
            System.Console.WriteLine(String.Format("R:{0}, G:{1}, B:{2}, {{bright:{3}, hue:{4}, sa:{5}}}", color.R, color.G, color.B, brightness, hue, saturation));
            return ((color.R >225 && color.G > 225));
            //return (brightness > 0.8 && hue > 0.8) || (color.R > 225 && color.G > 225);
        }

        public static Point scan(Bitmap bitmap)
        {
            Point p = new Point();
            Boolean bFound = false;
            for (int x = 0; !bFound && x < bitmap.Width; x++)
            {
                int countWhite = 0;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (isWhite(color))
                        countWhite++;
                }
                System.Console.WriteLine("COUNT:" + countWhite);
                if (countWhite != bitmap.Height)
                {
                    bFound = true;
                    p.X = x;
                }
            }

            bFound = false;
            for (int x = 0; !bFound && x < bitmap.Height; x++)
            {
                int countWhite = 0;
                for (int y = p.Y+1; y < 15; y++)
                {
                    Color color = bitmap.GetPixel(y, x);
                    
                    //System.Console.WriteLine("B:" + brightness);
                    if (isWhite(color))
                        countWhite++;
                }
                if (countWhite != 15)
                {
                    bFound = true;
                    p.Y = x;
                }
            }
            return p;
        }

        /// <summary>  
        /// 判断图形里是否存在另外一个图形 并返回所在位置  
        /// </summary>  
        /// <param name="p_SourceBitmap">原始图形</param>  
        /// <param name="p_PartBitmap">小图形</param>  
        /// <param name="p_Float">溶差</param>  
        /// <returns>坐标</returns>  
        public static Point GetImageContains(Bitmap p_SourceBitmap, Bitmap p_PartBitmap, int p_Float)
        {
            int _SourceWidth = p_SourceBitmap.Width;
            int _SourceHeight = p_SourceBitmap.Height;

            int _PartWidth = p_PartBitmap.Width;
            int _PartHeight = p_PartBitmap.Height;

            Bitmap _SourceBitmap = new Bitmap(_SourceWidth, _SourceHeight);
            Graphics _Graphics = Graphics.FromImage(_SourceBitmap);
            _Graphics.DrawImage(p_SourceBitmap, new Rectangle(0, 0, _SourceWidth, _SourceHeight));
            _Graphics.Dispose();
            BitmapData _SourceData = _SourceBitmap.LockBits(new Rectangle(0, 0, _SourceWidth, _SourceHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] _SourceByte = new byte[_SourceData.Stride * _SourceHeight];
            Marshal.Copy(_SourceData.Scan0, _SourceByte, 0, _SourceByte.Length);  //复制出p_SourceBitmap的相素信息   

            Bitmap _PartBitmap = new Bitmap(_PartWidth, _PartHeight);
            _Graphics = Graphics.FromImage(_PartBitmap);
            _Graphics.DrawImage(p_PartBitmap, new Rectangle(0, 0, _PartWidth, _PartHeight));
            _Graphics.Dispose();
            BitmapData _PartData = _PartBitmap.LockBits(new Rectangle(0, 0, _PartWidth, _PartHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] _PartByte = new byte[_PartData.Stride * _PartHeight];
            Marshal.Copy(_PartData.Scan0, _PartByte, 0, _PartByte.Length);   //复制出p_PartBitmap的相素信息   


            for (int i = 0; i != _SourceHeight; i++)
            {
                if (_SourceHeight - i < _PartHeight) return new Point(-1, -1);  //如果 剩余的高 比需要比较的高 还要小 就直接返回               
                int _PointX = -1;    //临时存放坐标 需要包正找到的是在一个X点上  
                bool _SacnOver = true;   //是否都比配的上  
                for (int z = 0; z != _PartHeight - 1; z++)       //循环目标进行比较  
                {
                    int _TrueX = GetImageContains(_SourceByte, _PartByte, i * _SourceData.Stride, _SourceWidth, _PartWidth, p_Float, z);

                    if (_TrueX == -1)   //如果没找到   
                    {
                        _PointX = -1;    //设置坐标为没找到  
                        _SacnOver = false;   //设置不进行返回  
                        break;
                    }
                    else
                    {
                        if (z == 0) _PointX = _TrueX;
                        if (_PointX != _TrueX)   //如果找到了 也的保证坐标和上一行的坐标一样 否则也返回  
                        {
                            _PointX = -1;//设置坐标为没找到  
                            _SacnOver = false;  //设置不进行返回  
                            break;
                        }
                    }
                }
                if (_SacnOver) return new Point(_PointX, i);
            }
            return new Point(-1, -1);
        }  

        /// <summary>  
        /// 判断图形里是否存在另外一个图形 所在行的索引  
        /// </summary>  
        /// <param name="p_Source">原始图形数据</param>  
        /// <param name="p_Part">小图形数据</param>  
        /// <param name="p_SourceIndex">开始位置</param>  
        /// <param name="p_SourceWidth">原始图形宽</param>  
        /// <param name="p_PartWidth">小图宽</param>  
        /// <param name="p_Float">溶差</param>  
        /// <returns>所在行的索引 如果找不到返回-1</returns>  
        private static int GetImageContains(byte[] p_Source, byte[] p_Part, int p_SourceIndex, int p_SourceWidth, int p_PartWidth, int p_Float, int _PartIndex)
        {   
            int _SourceIndex = p_SourceIndex;
            for (int i = 0; i < p_SourceWidth; i++)
            {
                if (p_SourceWidth - i < p_PartWidth) return -1;
                Color _CurrentlyColor = Color.FromArgb((int)p_Source[_SourceIndex + 3], (int)p_Source[_SourceIndex + 2], (int)p_Source[_SourceIndex + 1], (int)p_Source[_SourceIndex]);
                Color _CompareColoe = Color.FromArgb((int)p_Part[3], (int)p_Part[2], (int)p_Part[1], (int)p_Part[0]);
                _SourceIndex += 4;

                bool _ScanColor = ScanColor(_CurrentlyColor, _CompareColoe, p_Float);

                if (_ScanColor)
                {
                    _PartIndex += 4;
                    int _SourceRVA = _SourceIndex;
                    bool _Equals = true;
                    for (int z = 0; z != p_PartWidth - 1; z++)
                    {
                        _CurrentlyColor = Color.FromArgb((int)p_Source[_SourceRVA + 3], (int)p_Source[_SourceRVA + 2], (int)p_Source[_SourceRVA + 1], (int)p_Source[_SourceRVA]);
                        _CompareColoe = Color.FromArgb((int)p_Part[_PartIndex + 3], (int)p_Part[_PartIndex + 2], (int)p_Part[_PartIndex + 1], (int)p_Part[_PartIndex]);

                        if (!ScanColor(_CurrentlyColor, _CompareColoe, p_Float))
                        {
                            _PartIndex = 0;
                            _Equals = false;
                            break;
                        }
                        _PartIndex += 4;
                        _SourceRVA += 4;
                    }
                    if (_Equals) return i;
                }
                else
                {
                    _PartIndex = 0;
                }
            }
            return -1;
        }  
        /// <summary>  
        /// 检查色彩(可以根据这个更改比较方式  
        /// </summary>  
        /// <param name="p_CurrentlyColor">当前色彩</param>  
        /// <param name="p_CompareColor">比较色彩</param>  
        /// <param name="p_Float">溶差</param>  
        /// <returns></returns>  
        private static bool ScanColor(Color p_CurrentlyColor, Color p_CompareColor, int p_Float)
        {
            int _R = p_CurrentlyColor.R;
            int _G = p_CurrentlyColor.G;
            int _B = p_CurrentlyColor.B;

            return (_R <= p_CompareColor.R + p_Float && _R >= p_CompareColor.R - p_Float) && (_G <= p_CompareColor.G + p_Float && _G >= p_CompareColor.G - p_Float) && (_B <= p_CompareColor.B + p_Float && _B >= p_CompareColor.B - p_Float);

        }  
    }

    public class ImageHelper
    {
        /// <summary>
        /// 判断图形里是否存在另外一个图形 并返回所在位置
        /// </summary>
        /// <param name=”p_SourceBitmap”>原始图形</param>
        /// <param name=”p_PartBitmap”>小图形</param>
        /// <param name=”p_Float”>溶差</param>
        /// <returns>坐标</returns>
        public Point GetImageContains(Bitmap p_SourceBitmap, Bitmap p_PartBitmap, int p_Float)
        {
            int _SourceWidth = p_SourceBitmap.Width;
            int _SourceHeight = p_SourceBitmap.Height;
            int _PartWidth = p_PartBitmap.Width;
            int _PartHeight = p_PartBitmap.Height;
            Bitmap _SourceBitmap = new Bitmap(_SourceWidth, _SourceHeight);
            Graphics _Graphics = Graphics.FromImage(_SourceBitmap);
            _Graphics.DrawImage(p_SourceBitmap, new Rectangle(0, 0, _SourceWidth, _SourceHeight));
            _Graphics.Dispose();
            BitmapData _SourceData = _SourceBitmap.LockBits(new Rectangle(0, 0, _SourceWidth, _SourceHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] _SourceByte = new byte[_SourceData.Stride * _SourceHeight];
            Marshal.Copy(_SourceData.Scan0, _SourceByte, 0, _SourceByte.Length);  //复制出p_SourceBitmap的相素信息
            _SourceBitmap.UnlockBits(_SourceData);
            Bitmap _PartBitmap = new Bitmap(_PartWidth, _PartHeight);
            _Graphics = Graphics.FromImage(_PartBitmap);
            _Graphics.DrawImage(p_PartBitmap, new Rectangle(0, 0, _PartWidth, _PartHeight));
            _Graphics.Dispose();
            BitmapData _PartData = _PartBitmap.LockBits(new Rectangle(0, 0, _PartWidth, _PartHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] _PartByte = new byte[_PartData.Stride * _PartHeight];
            Marshal.Copy(_PartData.Scan0, _PartByte, 0, _PartByte.Length);   //复制出p_PartBitmap的相素信息
            _PartBitmap.UnlockBits(_PartData);
            for (int i = 0; i != _SourceHeight; i++)
            {
                if (_SourceHeight - i < _PartHeight) return new Point(-1, -1);  //如果 剩余的高 比需要比较的高 还要小 就直接返回
                int _PointX = -1;    //临时存放坐标 需要包正找到的是在一个X点上
                bool _SacnOver = true;   //是否都比配的上
                for (int z = 0; z != _PartHeight - 1; z++)       //循环目标进行比较
                {
                    int _TrueX = GetImageContains(_SourceByte, _PartByte, (i + z) * _SourceData.Stride, z * _PartData.Stride, _SourceWidth, _PartWidth, p_Float);
                    if (_TrueX == -1)   //如果没找到
                    {
                        _PointX = -1;    //设置坐标为没找到
                        _SacnOver = false;   //设置不进行返回
                        break;
                    }
                    else
                    {
                        if (z == 0) _PointX = _TrueX;
                        if (_PointX != _TrueX)   //如果找到了 也的保证坐标和上一行的坐标一样 否则也返回
                        {
                            _PointX = -1;//设置坐标为没找到
                            _SacnOver = false;  //设置不进行返回
                            break;
                        }
                    }
                }
                if (_SacnOver) return new Point(_PointX, i);
            }
            return new Point(-1, -1);
        }
        /// <summary>
        /// 判断图形里是否存在另外一个图形 所在行的索引
        /// </summary>
        /// <param name=”p_Source”>原始图形数据</param>
        /// <param name=”p_Part”>小图形数据</param>
        /// <param name=”p_SourceIndex”>开始位置</param>
        /// <param name=”p_SourceWidth”>原始图形宽</param>
        /// <param name=”p_PartWidth”>小图宽</param>
        /// <param name=”p_Float”>溶差</param>
        /// <returns>所在行的索引 如果找不到返回-1</returns>
        private int GetImageContains(byte[] p_Source, byte[] p_Part, int p_SourceIndex, int p_PartIndex, int p_SourceWidth, int p_PartWidth, int p_Float)
        {
            int _PartIndex = p_PartIndex;//
            int _PartRVA = _PartIndex;//p_PartX轴起点
            int _SourceIndex = p_SourceIndex;//p_SourceX轴起点
            for (int i = 0; i < p_SourceWidth; i++)
            {
                if (p_SourceWidth - i < p_PartWidth) return -1;
                Color _CurrentlyColor = Color.FromArgb((int)p_Source[_SourceIndex + 3], (int)p_Source[_SourceIndex + 2], (int)p_Source[_SourceIndex + 1], (int)p_Source[_SourceIndex]);
                Color _CompareColoe = Color.FromArgb((int)p_Part[_PartRVA + 3], (int)p_Part[_PartRVA + 2], (int)p_Part[_PartRVA + 1], (int)p_Part[_PartRVA]);
                _SourceIndex += 4;//成功，p_SourceX轴加4
                bool _ScanColor = ScanColor(_CurrentlyColor, _CompareColoe, p_Float);
                if (_ScanColor)
                {
                    _PartRVA += 4;//成功，p_PartX轴加4
                    int _SourceRVA = _SourceIndex;
                    bool _Equals = true;
                    for (int z = 0; z != p_PartWidth - 1; z++)
                    {
                        _CurrentlyColor = Color.FromArgb((int)p_Source[_SourceRVA + 3], (int)p_Source[_SourceRVA + 2], (int)p_Source[_SourceRVA + 1], (int)p_Source[_SourceRVA]);
                        _CompareColoe = Color.FromArgb((int)p_Part[_PartRVA + 3], (int)p_Part[_PartRVA + 2], (int)p_Part[_PartRVA + 1], (int)p_Part[_PartRVA]);
                        if (!ScanColor(_CurrentlyColor, _CompareColoe, p_Float))
                        {
                            _PartRVA = _PartIndex;//失败，重置p_PartX轴开始
                            _Equals = false;
                            break;
                        }
                        _PartRVA += 4;//成功，p_PartX轴加4
                        _SourceRVA += 4;//成功，p_SourceX轴加4
                    }
                    if (_Equals) return i;
                }
                else
                {
                    _PartRVA = _PartIndex;//失败，重置p_PartX轴开始
                }
            }
            return -1;
        }
        /// <summary>
        /// 检查色彩(可以根据这个更改比较方式
        /// </summary>
        /// <param name=”p_CurrentlyColor”>当前色彩</param>
        /// <param name=”p_CompareColor”>比较色彩</param>
        /// <param name=”p_Float”>溶差</param>
        /// <returns></returns>
        private bool ScanColor(Color p_CurrentlyColor, Color p_CompareColor, int p_Float)
        {
            int _R = p_CurrentlyColor.R;
            int _G = p_CurrentlyColor.G;
            int _B = p_CurrentlyColor.B;
            return (_R <= p_CompareColor.R + p_Float && _R >= p_CompareColor.R - p_Float) && (_G <= p_CompareColor.G + p_Float && _G >= p_CompareColor.G - p_Float) && (_B <= p_CompareColor.B + p_Float && _B >= p_CompareColor.B - p_Float);
        }
    }
}
