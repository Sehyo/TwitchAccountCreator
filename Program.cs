/**
* Author Alex Noble
* This software relies on the website http://guerrillamail.com to generate temporary emails
* Used in account creation for Twitch.tv.
* Also relies on the service "DeathByCaptcha",
* And utilises IP changing on the fly by integrating the HMA! VPN Client in Windows
*/
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using WatiN.Core;
using RedTomahawk.TORActivator;
using DeathByCaptcha;
// Some names this programme generated.
// suspiciousscissors
// wrongdoghouse
// operationalberry
// cleanapple
namespace TwitchAccountCreator
{
    class Program
    {
        // Twitch have added Captcha Trigger.
        // Below code is to simulate human input to trigger the captcha.
        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        static void triggerCaptchaAndFillPassword(IntPtr hWnd)
        {
            SetForegroundWindow(hWnd);
            browser.TextField(Find.ById("user_password")).Click();
            browser.TextField(Find.ById("user_password")).TypeText("h");
            SendKeys.SendWait("{BACKSPACE}");
            foreach(char c in password)
                SendKeys.SendWait(c.ToString());
            // Okay, we faked "real" keyboard input,
            // Now just fake "real" mouse input to trigger captcha.
            RECT rectangle = new RECT();
            GetWindowRect(hWnd, ref rectangle);
            // Calculate middle of the window and then lets click it!
            int x = ((rectangle.Right - rectangle.Left) / 2) + rectangle.Left;
            int y = ((rectangle.Bottom - rectangle.Top) / 2) + rectangle.Top;
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        // We also have to simulate a "real" mouse click to trigger the captcha!
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        // Has to be within the window, so here is some code to find the position of it on screen.
         [DllImport("user32.dll", SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
         [StructLayout(LayoutKind.Sequential)]
         private struct RECT
         {
             public int Left;
             public int Top;
             public int Right;
             public int Bottom;
          }

        public static Browser browser;
        public static Browser mailBrowser;
        public static CEngine cEngine;
        public static string nextUsername;
        public static string password = "halo2000";
        public static string[] adjectives;
        public static string[] nouns;
        public static string email;
        public static int captchaID;
        public static string oauth;
        [STAThread]
        static void Main(string[] args)
        {
            browser = new IE();
            mailBrowser = new IE();
            cEngine = new CEngine();
            initialiseWordLists();
            while(true)
            {
                //connectToRandomProxy();
                changeHMAIP();
                openWebsite("http://guerrillamail.com", 1);
                logOutFromTwitch();
                refreshMail();
                nextUsername = generateUsername();
                registerAccount();
                verifyEmail();
                getOAuth();
                saveAccount();
            }
        }

        static void changeHMAIP()
        {
            Process proc = null;
            try
            {
                string targetDir = "";
                proc = new Process();
                proc.StartInfo.WorkingDirectory = targetDir;
                proc.StartInfo.FileName = "hmavpnchangeip.bat";
                proc.StartInfo.CreateNoWindow = false;
                proc.Start();
                proc.WaitForExit();
                Console.WriteLine("HMA IP Changed.");
                Console.WriteLine("Waiting >.<");
            }
            catch
            {
                Console.WriteLine("Failed to change IP in HMA");
            }
        }

        static void openWebsite(string url, int whatBrowser) // To prevent disconnections and stuff.
        {
            while(true)
            {
                try
                {
                    if(whatBrowser == 0)
                    {
                        browser.GoTo(url);
                        if(browser.Html.Contains("You have been blocked from using Twitch"))
                        {
                            changeHMAIP();
                            openWebsite(url, 0);
                        }
                    }
                    else
                        mailBrowser.GoTo(url);
                    if(whatBrowser == 0)
                        if(browser.Html.Contains("proxy server") || browser.Html.Contains("This page can"))
                        {
                            //page not found
                            Console.WriteLine("Waiting for connection...");
                            Thread.Sleep(1000);
                            browser.GoTo(url);
                        }
                        else
                        {
                            // Page found :)
                            Console.WriteLine("Connected!");
                            break;
                        }
                    else
                        if(mailBrowser.Html.Contains("proxy server") || mailBrowser.Html.Contains("This page can"))
                        {
                            //page not found
                            Console.WriteLine("Waiting for connection...");
                            Thread.Sleep(1000);
                            mailBrowser.GoTo(url);
                        }
                        else
                        {
                            // Page found :)
                            Console.WriteLine("Connected!");
                            break;
                        }
                    }
               catch{}
            }
        }

        static void logOutFromTwitch()
        {
            openWebsite("http://www.twitch.tv/logout", 0);
        }

        static void refreshMail()
        {
            openWebsite("https://www.guerrillamail.com/?fgt=1", 1);
            openWebsite("http://www.guerrillamail.com", 1);
        }

        static void saveAccount()
        {
            File.AppendAllText("accounts.txt", nextUsername + oauth + Environment.NewLine);
        }

        static void getOAuth()
        {
            openWebsite("https://api.twitch.tv/kraken/oauth2/authorize?response_type=token&amp;client_id=q6batx0epp608isickayubi39itsckt&amp;redirect_uri=http://twitchapps.com/tmi/&amp;scope=chat_login", 0);
            browser.Button(Find.ByClass("button primary_button round")).Click();
            oauth = ":" + browser.TextField(Find.ByClass("span9")).Value;
        }

        static bool emailReceived()
        {
            string html = mailBrowser.Html;
            if(html.Contains("Twitch")) return true;
            return false;
        }

        unsafe static void activateAccount()
        {
            string html = mailBrowser.Html;
            // Get position of "Verify Twitch" (Subject) in Html.
            int pos = html.IndexOf("Verify Twitch");
            // Work backwards till we find a numeric :)
            int endPosOfMailID;
            int startPosOfMailID;
            while(!Char.IsNumber(html[pos]))    pos--;
            endPosOfMailID = pos+1;
            while(Char.IsNumber(html[pos])) pos--;
            startPosOfMailID = pos+1;
            string mailID = html.Substring(startPosOfMailID, endPosOfMailID - startPosOfMailID);
            Console.WriteLine("Mail ID: {0}", mailID);
            string linkToEmail = "http://www.guerrillamail.com/inbox?mail_id=" + mailID;
            openWebsite(linkToEmail, 1);
            // Find Twitch Verification Code
            string verificationLink = "";
            html = mailBrowser.Html;
            pos = html.IndexOf("verify_email?email_verification_code");
            char[] someHtml = new char[5];
            int indexInCArray = 3;
            bool httpFound = false;
            string ourPreciousString = "";
            while(!ourPreciousString.ToUpper().Contains("HCTIWT")) // Loop backwards in the html to find 'http'
            {
                ourPreciousString += html[pos];
                pos--;
            }
            while(html[pos+1] != '"')
            {
               pos++;
                verificationLink += html[pos];
            }
            verificationLink = "http://www." + verificationLink;
            Console.WriteLine(verificationLink);
            openWebsite(verificationLink, 1);
        }

        static void connectToRandomProxy()
        {
            Console.WriteLine("Connecting To Random Proxy.");
            string[] lines = File.ReadAllLines("proxies.txt");
            Random rand = new Random();
            string line = lines[rand.Next(lines.Length)];
           // cEngine.EnableProxy(line);
            Console.WriteLine("Connected To Proxy: " + line);
        }
        // To create new tab: SendKeys.SendWait("^{t}");

        static void verifyEmail()
        {
            //browser.GoTo("http://www.twitch.tv/settings?type=email_notice");
            // Press Verify Button.
            //browser.Button(Find.ById("verify_email")).Click();
            //Console.WriteLine("Verify Button clicked.");
            while(true)
            {
                Console.WriteLine("Waiting for verification Email...");
                try
                {
                    if(mailBrowser.Html.Contains("Twitch"))
                    {
                        Console.WriteLine("Mail Received!");
                        // Refresh website, otherwise the Html is not correct for some reason .-.
                        openWebsite("http://www.guerrillamail.com", 1);
                        break;
                    }
                }
                catch{}
            }
            activateAccount();
        }

        static void registerAccount()
        {
            Random rand = new Random();
            int month = rand.Next(1,12);
            int day = rand.Next(1,25);
            int year = rand.Next(1960, 1995);
            Console.WriteLine("Registering Account..");
            openWebsite("http://www.twitch.tv/user/signup", 0); // <-- Crashes if proxy is slow
            browser.WaitForComplete();
            browser.TextField(Find.ById("user_login")).Value = nextUsername;
            Console.WriteLine("Name filled in.");
            triggerCaptchaAndFillPassword(browser.hWnd);
            Console.WriteLine("Password filled in.");
            #region Select Month
            Console.WriteLine("Month: {0}", month);
            switch (month)
            {
                case 1:
                    browser.SelectList(Find.ById("date_month")).Option("January").Select();
                    break;
                case 2:
                    browser.SelectList(Find.ById("date_month")).Option("February").Select();
                    break;
                case 3:
                    browser.SelectList(Find.ById("date_month")).Option("March").Select();
                    break;
                case 4:
                    browser.SelectList(Find.ById("date_month")).Option("April").Select();
                    break;
                case 5:
                    browser.SelectList(Find.ById("date_month")).Option("May").Select();
                    break;
                case 6:
                    browser.SelectList(Find.ById("date_month")).Option("June").Select();
                    break;
                case 7:
                    browser.SelectList(Find.ById("date_month")).Option("July").Select();
                    break;
                case 8:
                    browser.SelectList(Find.ById("date_month")).Option("August").Select();
                    break;
                case 9:
                    browser.SelectList(Find.ById("date_month")).Option("September").Select();
                    break;
                case 10:
                    browser.SelectList(Find.ById("date_month")).Option("October").Select();
                    break;
                case 11:
                    browser.SelectList(Find.ById("date_month")).Option("November").Select();
                    break;
                case 12:
                    browser.SelectList(Find.ById("date_month")).Option("December").Select();
                    break;
            }
            #endregion
            // Select Day
            browser.SelectList(Find.ById("date_day")).Option(day.ToString()).Select();
            // Select Year
            browser.SelectList(Find.ById("date_year")).Option(year.ToString()).Select();
            typeCaptcha();
            // Fill in email.
            getEmail();
            browser.TextField(Find.ById("user_email")).Value = email;
            // Click Register!
            browser.NativeDocument.Body.SetFocus();
            browser.Button(Find.ById("subwindow_create_submit")).Click();
            browser.WaitForComplete();
            Thread.Sleep(1000);
            while(true) // Checks if username is taken and makes new username till it's not :D
            {

                try
                {
                if(browser.Html.Contains("Login has already been taken") || browser.Html.Contains("Please correct your phrase in the captcha"))
                {
                    if(browser.Html.Contains("Login has already been taken"))
                    {
                        // Generate new username and try again.
                        nextUsername = generateUsername();
                        // Change username value
                        browser.TextField(Find.ById("user_login")).Value = nextUsername;
                        browser.NativeDocument.Body.SetFocus();
                        browser.Button(Find.ById("subwindow_create_submit")).Click();
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        // Report previous captcha wrong to DBC.
                        Client client = (Client) new SocketClient("USERNAMEREMOVED","PASSWORDREMOVED");
                        client.Report(captchaID);
                        typeCaptcha();
                        browser.NativeDocument.Body.SetFocus();
                        browser.Button(Find.ById("subwindow_create_submit")).Click();
                        Thread.Sleep(1000);
                    }
                }
                else break;
                }
                catch{}
            }
        }
        
        static void getEmail()
        {
            Console.WriteLine("Getting Temporary Email Address");
            openWebsite("https://www.guerrillamail.com/", 1);
            email = mailBrowser.Span(Find.ById("inbox-id")).Text + "@guerrillamail.com";
            Console.WriteLine("Temporary Email: {0}", email);
        }

        static void typeCaptcha()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Getting Captcha Image...");
            // Get image
            Image image = browser.Image(Find.BySrc(t => t.Contains("http://www.google.com/recaptcha/api/")));
            Console.WriteLine("Link to captcha: {0}", image.Src);
            WebClient webClient = new WebClient();
            byte[] captchaImage = webClient.DownloadData(image.Src);

            Client client = (Client) new SocketClient("USERNAMEREMOVED","PASSWORDREMOVED");
            Captcha captcha = client.Decode(captchaImage,120);
            if(captcha.Solved && captcha.Correct)
            {
                Console.WriteLine("CAPTCHA {0}: {1}", captcha.Id, captcha.Text);
                captchaID = captcha.Id;
            }
            browser.TextField(Find.ById("recaptcha_response_field")).Value = captcha.Text;
            Console.WriteLine("Captcha filled in");
        }

        static string generateUsername()
        {
            Random rand = new Random();
            return adjectives[rand.Next(adjectives.Length)] + nouns[rand.Next(nouns.Length)];
        }

        static void initialiseWordLists()
        {
            adjectives = File.ReadAllLines("adjectiveList.txt");
            nouns = File.ReadAllLines("nounList.txt");
        }
    }
}