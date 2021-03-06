﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace tobid.util.orc
{
    /// <summary>
    /// 根据提示获取有效验证码
    /// </summary>
    public class CaptchaUtil
    {
        private OrcUtil[] orcTips;
        private OrcUtil orcNo;
        public CaptchaUtil(OrcUtil tips0, OrcUtil tips1, OrcUtil no)
        {
            this.orcNo = no;
            this.orcTips = new OrcUtil[]{ tips0, tips1 };
        }

        private List<Bitmap> subImgs;
        public List<Bitmap> SubImgs { get { return this.subImgs; } }

        public String getActive(String captcha, Bitmap bitmapTips)
        {
            int indexTips = 0;
            String tips = this.orcTips[indexTips].getCharFromPic(bitmapTips, 0, 0);
            String numbers = "";
            if (tips.StartsWith("请输入"))
                numbers = this.orcNo.getCharFromPic(bitmapTips);
            else
            {
                indexTips++;
                tips = this.orcTips[indexTips].getCharFromPic(bitmapTips, x: 20);
                numbers = this.orcNo.getCharFromPic(bitmapTips, x:20);
            }

            this.subImgs = new List<Bitmap>();
            for (int i = 0; i < this.orcTips[indexTips].SubImgs.Count; i++)
                this.subImgs.Add(this.orcTips[indexTips].SubImgs[i]);
            for (int i = 0; i < this.orcNo.SubImgs.Count; i++)
                this.subImgs.Add(this.orcNo.SubImgs[i]);

            char[] arrayno = numbers.ToCharArray();
            String start = String.Format("{0}", arrayno[0]);
            String end = String.Format("{0}", arrayno[1]);

            if ('第'.Equals(tips[3]))
                return captcha.Substring(Int16.Parse(start) - 1, Int16.Parse(end) - Int16.Parse(start) + 1);
            else if ('前'.Equals(tips[3]))
                return captcha.Substring(0, Int16.Parse(start));
            else if ('后'.Equals(tips[3]))
                return captcha.Substring(captcha.Length - Int16.Parse(start), Int16.Parse(start));
            else
                return captcha;
        }
    }

    /// <summary>
    /// 识别图片
    /// </summary>
    public class OrcUtil
    {
        static public String getSingleChar(Bitmap img, IDictionary<Bitmap, String> dict)
        {
            String result = "";
            int width = img.Width;
            int height = img.Height;
            int min = width * height;
            foreach (Bitmap bi in dict.Keys)
            {
                if (width > bi.Width || height > bi.Height)
                    continue;
                
                int count = 0;
                for (int x = 0; x < width; ++x)
                    for (int y = 0; y < height; ++y)
                    {
                        Color imgPoint = img.GetPixel(x, y);
                        Color biPoint = bi.GetPixel(x, y);
                        if (isWhite(imgPoint) != isWhite(biPoint))
                        {
                            count++;
                            if (count >= min)
                                goto Label1;
                        }
                    }
            Label1:
                if (count < min)
                {
                    min = count;
                    result = dict[bi];
                }
            }
            return result;
        }

        static private int isWhite(Color point)
        {
            if (point.R + point.G + point.B > 100)
                return 1;
            return 0;
        }

        private int[] offsetX;
        private int offsetY;
        private int width, height;
        private int minNearSpots;
        private IDictionary<Bitmap, String> dict;
        private List<Bitmap> subImgs;
        public List<Bitmap> SubImgs { get { return this.subImgs; } }

        static public OrcUtil getInstance(tobid.rest.OrcConfig orcConfig, IDictionary<Bitmap, String> dict)
        {
            OrcUtil rtn = getInstance(orcConfig.offsetX, orcConfig.offsetY, orcConfig.width, orcConfig.height, dict);
            rtn.minNearSpots = orcConfig.minNearSpots;
            return rtn;
        }

        static public OrcUtil getInstance(int[] offsetX, int offsetY, int width, int height, IDictionary<Bitmap, String> dict)
        {
            OrcUtil rtn = new OrcUtil();
            rtn.offsetX = offsetX;
            rtn.offsetY = offsetY;
            rtn.width = width;
            rtn.height = height;
            rtn.dict = dict;
            return rtn;
        }

        /// <summary>
        /// 创建Orc实例(from Stream)
        /// </summary>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        static public OrcUtil getInstance(int[] offsetX, int offsetY, int width, int height, Stream resource)
        {
            OrcUtil rtn = new OrcUtil();
            rtn.offsetX = offsetX;
            rtn.offsetY = offsetY;
            rtn.width = width;
            rtn.height = height;
            rtn.dict = new Dictionary<Bitmap, String>();

            System.Resources.ResXResourceReader resxReader = new System.Resources.ResXResourceReader(resource);
            IDictionaryEnumerator enumerator = resxReader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                DictionaryEntry entry = (DictionaryEntry)enumerator.Current;
                rtn.dict.Add((Bitmap)entry.Value, (String)entry.Key);
            }
            return rtn;
        }

        /// <summary>
        /// 创建Orc实例(from Directory)
        /// </summary>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        static public OrcUtil getInstance(int[] offsetX, int offsetY, int width, int height, String dictPath)
        {
            OrcUtil rtn = new OrcUtil();
            rtn.offsetX = offsetX;
            rtn.offsetY = offsetY;
            rtn.width = width;
            rtn.height = height;
            rtn.dict = new Dictionary<Bitmap, String>();

            String[] files = System.IO.Directory.GetFiles(dictPath);
            foreach (String file in files)
            {
                String name = new System.IO.FileInfo(file).Name;
                String[] array = name.Split(new char[] { '.' });
                Bitmap bitmap = new Bitmap(file);
                rtn.dict.Add(bitmap, array[0]);
            }
            return rtn;
        }

        /// <summary>
        /// 识别图片中的文字
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="x">x偏移量</param>
        /// <param name="y">y偏移量</param>
        /// <returns></returns>
        public String getCharFromPic(Bitmap image, int x=0, int y=0)
        {
            this.subImgs = new List<Bitmap>();
            image.Save("xxx.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            StringBuilder sb = new StringBuilder();
            ImageTool it = new ImageTool();
            it.setImage(image);
            it = it.changeToGrayImage().changeToBlackWhiteImage();
            if (minNearSpots != 0)
                it = it.removeBadBlock(1, 1, this.minNearSpots);
            for (int i = 0; i < this.offsetX.Length; i++)
            {
                Rectangle cloneRect = new Rectangle(this.offsetX[i]+x, this.offsetY+y, this.width, this.height);
                Bitmap subImg = it.Image.Clone(cloneRect, it.Image.PixelFormat);
                this.subImgs.Add(subImg);
                String s = OrcUtil.getSingleChar(subImg, this.dict);
                sb.Append(s);
            }
            return sb.ToString();
        }
    }

    class ImageTool
    {
        private Bitmap image;
        private int width;
        private int height;

        static private Color WHITE = Color.FromArgb(255, 255, 255);
        static private Color BLACK = Color.FromArgb(0, 0, 0);

        public Bitmap Image
        {
            get { return this.image; }
        }

        public void setImage(Bitmap image)
        {
            this.image = image;
            this.width = image.Width;
            this.height = image.Height;
        }

        /// <summary>
        /// 灰度处理
        /// </summary>
        /// <returns></returns>
        public ImageTool changeToGrayImage()
        {
            int gray;
            Color point;
            for (int i = 0; i < this.height; i++)
                for (int j = 0; j < this.width; j++)
                {
                    point = image.GetPixel(j, i);
                    gray = (point.R + point.G + point.B) / 3;
                    image.SetPixel(j, i, Color.FromArgb(gray, gray, gray));
                }
            return this;
        }

        /// <summary>
        /// 二值化
        /// </summary>
        /// <returns></returns>
        public ImageTool changeToBlackWhiteImage()
        {
            int avgGrayValue = this.getAvgValue();
            for (int i = 0; i < this.height; i++)
                for (int j = 0; j < this.width; j++)
                {
                    Color point = this.image.GetPixel(j, i);
                    image.SetPixel(j, i, point.R < avgGrayValue ? BLACK : WHITE);
                }
            return this;
        }

        /// <summary>
        /// 中值过滤
        /// </summary>
        /// <param name="whiteAreaMinPercent"></param>
        /// <param name="removeLighter"></param>
        /// <returns></returns>
        public ImageTool middleValueFilter(int whiteAreaMinPercent, Boolean removeLighter)
        {
            int modify = 0;
            int avg = this.getAvgValue();
            while (this.getWhitePercent() < whiteAreaMinPercent)
            {
                for (int i = 0; i < this.height; i++)
                    for (int j = 0; j < this.width; j++)
                    {
                        Color point = this.image.GetPixel(j, i);
                        if (removeLighter)
                        {
                            if (((point.R + point.G + point.B) / 3) > avg - modify)
                                this.image.SetPixel(j, i, WHITE);
                        }
                        else
                        {
                            if (((point.R + point.G + point.B) / 3) < avg + modify)
                                this.image.SetPixel(j, i, WHITE);
                        }
                    }
                modify++;
            }
            return this;
        }

        /// <summary>
        /// 去除噪点和单点组成的干扰线 
        /// 注意: 去除噪点之前应该对图像黑白化
        /// </summary>
        /// <param name="blockWidth"></param>
        /// <param name="blockHeight"></param>
        /// <param name="neighborhoodMinCount"></param>
        /// <returns></returns>
        public ImageTool removeBadBlock(int blockWidth, int blockHeight, int neighborhoodMinCount)
        {
            int val;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int counter = 0;//初始化邻居数为0
                    int topLeftXIndex = x - 1;
                    int topLeftYIndex = y - 1;
                    //x1 y1是以x,y左上角点为顶点的矩形,该矩形包围在传入的矩形的外围,计算传入的矩形的有效邻居数目
                    if (isBlackBlock(x, y, blockWidth, blockHeight))
                    {//只有当块是全黑色才计算
                        for (int x1 = topLeftXIndex; x1 <= topLeftXIndex + blockWidth + 1; x1++)
                            for (int y1 = topLeftYIndex; y1 <= topLeftYIndex + blockHeight + 1; y1++)
                            {
                                //判断这个点是否存在
                                if (x1 < width && x1 >= 0 && y1 < height && y1 >= 0)
                                {
                                    //判断这个点是否是传入矩形的外围点
                                    if (x1 == topLeftXIndex || x1 == topLeftXIndex + blockWidth + 1
                                            || y1 == topLeftYIndex || y1 == topLeftYIndex + blockHeight + 1)
                                    {
                                        //这里假定图像已经被黑白化,取Red值认为不是0就是255
                                        val = image.GetPixel(x1, y1).R;
                                        //如果这个邻居是黑色,就把中心点的有效邻居数目加一
                                        if (val == 0)
                                            counter++;
                                    }
                                }
                            }

                        if (counter < neighborhoodMinCount)
                            image.SetPixel(x, y, WHITE);
                    }
                }

            return this;
        }

        private bool isBlackBlock(int startX, int startY, int blockWidth, int blockHeight)
        {
            int counter = 0;//统计黑色像素点的个数
            int total = 0;//统计有效像素点的个数
            int val;
            for (int x1 = startX; x1 <= startX + blockWidth - 1; x1++)
                for (int y1 = startY; y1 <= startY + blockHeight - 1; y1++)
                {
                    //判断这个点是否存在
                    if (x1 < width && x1 >= 0 && y1 < height && y1 >= 0)
                    {
                        total++;//有效像素点的个数
                        //这里假定图像已经被黑白化,取Red值认为不是0就是255 
                        val = this.image.GetPixel(x1, y1).R;
                        //如果这个点是黑色,就把黑色像素点的数目加一
                        if (val == 0)
                            counter++;
                    }
                }

            return counter == total && total != 0;
        }

        private int getWhitePercent()
        {
            int white = 0;
            for (int i = 0; i < this.height; i++)
                for (int j = 0; j < this.width; j++)
                {
                    Color point = this.image.GetPixel(j, i);
                    if (((point.R + point.G + point.B) / 3) == 255)
                        white++;
                }
            return (int)Math.Ceiling(((float)white * 100 / (width * height)));
        }

        private int getAvgValue()
        {
            Color point;
            int total = 0;
            for (int i = 0; i < this.height; i++)
                for (int j = 0; j < this.width; j++)
                {

                    point = image.GetPixel(j, i);
                    total += (point.R + point.G + point.B) / 3;
                }
            return total / (this.width * this.height);
        }
    }
}
