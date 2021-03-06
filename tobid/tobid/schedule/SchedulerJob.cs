﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading;

using tobid.rest.json;
using tobid.rest.position;
using tobid.util.http;
using tobid.util.orc;
using tobid.util;

namespace tobid.scheduler.jobs
{
    public delegate void ReceiveOperation(rest.Operation operation);

    public interface ISchedulerJob
    {
        void Execute();
    }

    public class ShowInfoJob : ISchedulerJob
    {
        private String message;
        public ShowInfoJob(String message)
        {
            this.message = message;
        }

        public void Execute()
        {
            ScreenUtil screen = new ScreenUtil();
            for (int i = 0; i < 5; i++)
            {
                screen.drawSomething(
                    SystemInformation.WorkingArea.Size.Width - 100,
                    SystemInformation.WorkingArea.Size.Height - 100,
                    this.message);

                Thread.Sleep(500);
            }
        }
    }

    /// <summary>
    /// KeepAlive : 向服务器发布主机名，获取配置项
    /// </summary>
    public class KeepAliveJob : ISchedulerJob
    {
        private static log4net.ILog logger = log4net.LogManager.GetLogger("KeepAliveJob");

        private ReceiveOperation receiveOperation;
        public String EndPoint { get; set; }

        public KeepAliveJob(String endPoint, ReceiveOperation receiveOperation)
        {
            this.EndPoint = endPoint;
            this.receiveOperation = receiveOperation;
        }

        public void Execute()
        {
            logger.Debug(String.Format("{0} - {1} KeepAliveJob.Execute()", Thread.CurrentThread.Name, DateTime.Now));
            string hostName = System.Net.Dns.GetHostName();
            String epKeepAlive = this.EndPoint + "/command/keepAlive.do";
            RestClient restKeepAlive = new RestClient(endpoint: epKeepAlive, method: HttpVerb.POST);
            String rtn = restKeepAlive.MakeRequest(String.Format("?ip={0}", hostName));
            tobid.rest.Client client = Newtonsoft.Json.JsonConvert.DeserializeObject<tobid.rest.Client>(rtn, new OperationConvert());
            if (client.operation != null && client.operation.Length > 0)
            {
                foreach (tobid.rest.Operation operation in client.operation)
                {
                    if(operation is tobid.rest.BidOperation){

                        if (SubmitPriceJob.setConfig(client.config, operation))
                            this.receiveOperation(operation);
                    }
                    else if (operation is tobid.rest.LoginOperation)
                    {
                        LoginJob.setConfig(client.config, operation);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// SubmitPrice : 每秒检查，符合条件执行出价Action
    /// </summary>
    public class SubmitPriceJob : ISchedulerJob
    {
        private static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SubmitPriceJob));
        private static Object lockObj = new Object();

        private static Bid operation;
        private static DateTime startTime = new DateTime();
        private static DateTime expireTime = new DateTime();
        private static DateTime lastUpdate = new DateTime();
        private static int deltaPrice;
        private static int executeCount = 1;

        public String EndPoint { get; set; }
        private OrcUtil m_orcPrice;
        private OrcUtil m_orcLoading;
        private CaptchaUtil m_captchaUtil;
        
        public SubmitPriceJob(String endPoint, OrcUtil orcPrice, OrcUtil orcLoading, CaptchaUtil captchaUtil)
        {
            this.EndPoint = endPoint;
            this.m_orcPrice = orcPrice;
            this.m_orcLoading = orcLoading;
            this.m_captchaUtil = captchaUtil;
        }

        public static Boolean setConfig(rest.Config config, rest.Operation operation)
        {
            logger.Info("setConfig {...}");
            Boolean rtn = false;
            if (Monitor.TryEnter(SubmitPriceJob.lockObj, 500))
            {
                //if (operation.startTime > SubmitPriceJob.startTime)
                if(operation.updateTime > SubmitPriceJob.lastUpdate)//确保同一个版本（修改）的Operation只被配置并执行一次，避免多次执行
                {
                    SubmitPriceJob.lastUpdate = operation.updateTime;
                    SubmitPriceJob.executeCount = 0;

                    SubmitPriceJob.deltaPrice = ((rest.BidOperation)operation).price;
                    SubmitPriceJob.startTime = operation.startTime;
                    SubmitPriceJob.expireTime = operation.expireTime;

                    logger.DebugFormat("PRICE:{0}", ((rest.BidOperation)operation).price);
                    logger.DebugFormat("startTime:{0}", operation.startTime);
                    logger.DebugFormat("expireTime:{0}", operation.expireTime);

                    Bid bid = Newtonsoft.Json.JsonConvert.DeserializeObject<Bid>(operation.content);
                    SubmitPriceJob.operation = bid;
                    rtn = true;
                }
                Monitor.Exit(SubmitPriceJob.lockObj);
            }
            else
            {
                logger.Error("obtain SubmitPriceJob.lockObj timeout on setConfig(...)");
            }
            return rtn;
        }

        public void Execute(){

            DateTime now = DateTime.Now;
            logger.Debug(String.Format("{0} - NOW:{1}, {{Start:{2}, Expire:{3}, Count:{4}}}", 
                Thread.CurrentThread.Name, now, 
                SubmitPriceJob.startTime, SubmitPriceJob.expireTime, 
                SubmitPriceJob.executeCount));
            if (Monitor.TryEnter(SubmitPriceJob.lockObj, 500))
            {
                if (now >= SubmitPriceJob.startTime && now <= SubmitPriceJob.expireTime && SubmitPriceJob.executeCount==0)
                {
                    //TODO:这里可以加入逻辑，如果this.submit成功，SubmitPriceJob.executeCount++。
                    //这样在下一秒可以自动执行一次未成功的出价。但是DeltaPrice应该-=100，同时需要保证DeltaPrice>=+300
                    SubmitPriceJob.executeCount++;
                    logger.Debug("trigger Fired");

                    this.givePrice(SubmitPriceJob.operation.give, deltaPrice);//出价
                    this.submit(this.EndPoint, SubmitPriceJob.operation.submit);//提交
                }
                
                Monitor.Exit(SubmitPriceJob.lockObj);
            }
            else
            {
                logger.Error("obtain SubmitPriceJob.lockObj timeout on Execute(...)");
            }
        }

        /// <summary>
        /// 获取当前价格，+delta，出价
        /// </summary>
        /// <param name="givePrice">坐标</param>
        /// <param name="delta">差价</param>
        private void givePrice(GivePrice givePrice, int delta)
        {
            logger.Info("BEGIN givePRICE");
            logger.Info("\tBEGIN identify PRICE...");
            byte[] content = new ScreenUtil().screenCaptureAsByte(givePrice.price.x, givePrice.price.y, 52, 18);
            String txtPrice = this.m_orcPrice.getCharFromPic(new Bitmap(new System.IO.MemoryStream(content)));
            int price = Int32.Parse(txtPrice);
            price += delta;
            txtPrice = String.Format("{0:D}", price);
            logger.InfoFormat("\tEND   identified PRICE = %s", txtPrice);

            //INPUT BOX
            logger.InfoFormat("\tBEGIN input PRICE : {0}", txtPrice);
            ScreenUtil.SetCursorPos(givePrice.inputBox.x, givePrice.inputBox.y);
            ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);

            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);

            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);

            for (int i = 0; i < txtPrice.Length; i++)
            {
                System.Threading.Thread.Sleep(50);
                ScreenUtil.keybd_event(ScreenUtil.keycode[txtPrice[i].ToString()], 0, 0, 0);
            }
            logger.Info("\tEND   input PRICE");

            //点击出价
            logger.Info("\tBEGIN click BUTTON[出价]");
            System.Threading.Thread.Sleep(50);
            ScreenUtil.SetCursorPos(givePrice.button.x, givePrice.button.y);
            ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);
            logger.Info("\tEND   click BUTTON[出价]");
            logger.Info("END   givePRICE");
        }

        private Boolean submit(String URL, SubmitPrice submitPoints)
        {
            logger.Info("BEGIN giveCAPTCHA");
            logger.Info("\tBEGIN make INPUT blank");
            ScreenUtil.SetCursorPos(submitPoints.inputBox.x, submitPoints.inputBox.y);
            ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);

            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["BACKSPACE"], 0, 0, 0);

            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            System.Threading.Thread.Sleep(50); ScreenUtil.keybd_event(ScreenUtil.keycode["DELETE"], 0, 0, 0);
            logger.Info("\tEND   make INPUT blank");

            logger.Info("\tBEGIN identify CAPTCHA...");
            byte[] binaryCaptcha = null;
            Boolean isLoading = true;
            int retry = 0;
            while (isLoading)
            {
                logger.InfoFormat("\t try LOADING = {0}", retry++);
                binaryCaptcha = new ScreenUtil().screenCaptureAsByte(submitPoints.captcha[0].x, submitPoints.captcha[0].y, 128, 28);
                String strLoading = this.m_orcLoading.getCharFromPic(new Bitmap(new MemoryStream(binaryCaptcha)));
                logger.InfoFormat("\t LOADING = {0}", strLoading);
                if ("正在获取校验码".Equals(strLoading))
                {
                    if (retry > 2)
                    {//重试0,1,2,3都在获取校验码
                        logger.InfoFormat("Abort，close & re-open");
                        ScreenUtil.SetCursorPos(submitPoints.buttons[0].x+188, submitPoints.buttons[0].y);//取消按钮
                        ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);
                        return false;//放弃本次出价
                    }
                    Thread.Sleep(250);
                }
                else
                    isLoading = false;
            }

            logger.Info("\t\tBEGIN post CAPTACH");
            String txtCaptcha = new HttpUtil().postByteAsFile(URL + "/receive/captcha.do", binaryCaptcha);
            logger.Info("\t\tEND   post CAPTACH");
            byte[] binaryTips = new ScreenUtil().screenCaptureAsByte(submitPoints.captcha[1].x, submitPoints.captcha[1].y, 112, 16);
            String strActive = this.m_captchaUtil.getActive(txtCaptcha, new Bitmap(new System.IO.MemoryStream(binaryTips)));
            logger.InfoFormat("\tEND   identified CAPTCHA = {0}, ACTIVE = {1}", txtCaptcha, strActive);

            logger.Info("\tBEGIN input CAPTCHA");
            {
                for (int i = 0; i < strActive.Length; i++)
                {
                    ScreenUtil.keybd_event(ScreenUtil.keycode[strActive[i].ToString()], 0, 0, 0);
                    System.Threading.Thread.Sleep(50);
                }
            }
            logger.Info("\tEND   input CAPTCHA");

            logger.Info("\tBEGIN click BUTTON[确定]");
            ScreenUtil.SetCursorPos(submitPoints.buttons[0].x, submitPoints.buttons[0].y);
            ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);
            logger.Info("\tEND   click BUTTON[确定]");

            ScreenUtil.SetCursorPos(submitPoints.buttons[0].x + 188 / 2, submitPoints.buttons[0].y - 10);//确定按钮

            //System.Threading.Thread.Sleep(3000);
            //if (points.Length > 2)
            //{
            //    System.Threading.Thread.Sleep(50);
            //    ScreenUtil.SetCursorPos(points[2].X, points[2].Y);
            //    ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);

            //    if (points.Length > 3)
            //    {
            //        System.Threading.Thread.Sleep(50);
            //        ScreenUtil.SetCursorPos(points[3].X, points[3].Y);
                    //ScreenUtil.mouse_event((int)(MouseEventFlags.Absolute | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);
            //    }
            //}
            logger.Info("END   giveCAPTCHA");
            return true;
        }
    }
}
