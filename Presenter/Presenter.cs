using System;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using UW.ClassroomPresenter;
using System.Collections.Generic;

namespace UW.ClassroomPresenter {
    class Presenter {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args) {
#if LAUNCH_TWO_VIEWERS
            Thread second = new Thread(new ThreadStart(ViewerThreadStart));
            second.Name = "Second Viewer";
            second.Start();
#endif
            // Handle getting the language
            Microsoft.Win32.RegistryKey regkey = null;
            object o = null;
            string lang = string.Empty;
            CultureInfo cultureInfo;

            // Check the registry for a language setting
            regkey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\UW CSE\\Presenter\\V3", true);
            if( regkey == null )
                regkey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey( "Software\\UW CSE\\Presenter\\V3" );
            if( regkey != null )
                o = regkey.GetValue("Language");
            if (o != null)
                lang = Convert.ToString(o);

            // First, check the registry
            if (lang != string.Empty) {
                cultureInfo = new System.Globalization.CultureInfo(lang);
            // Second, check the config file
            } else if ((lang = ConfigurationManager.AppSettings["UW.ClassroomPresenter.UICulture"]) != null) {
                cultureInfo = new System.Globalization.CultureInfo(lang);
            // Last, get the system's regional settings
            } else {
                cultureInfo = Thread.CurrentThread.CurrentCulture;
                regkey.SetValue("Language", cultureInfo.ToString());
            }

            // Set the culture
            try {
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
#if LAUNCH_TWO_VIEWERS
            second.CurrentUICulture = cultureInfo;
#endif
            } catch { }

            //Parse the input arguments

            List<string> inputFiles = new List<string>();
            bool standalone = false;
            for (int i = 0; i < args.Length; i++) {
                if ("--input".StartsWith(args[i])) {
                    if ((i + 1) >= args.Length) {
                        Usage("Missing file argument for --input");
                        return;
                    }
                    if ("--".StartsWith(args[i + 1])) {
                        Usage("Missing file argument for --input");
                        return;
                    }
                    string inputFile = args[i + 1];
                    i++;
                    if (!File.Exists(inputFile)) {
                        Usage("File not found: " + inputFile);
                        return;
                    }
                    inputFiles.Add(inputFile);
                }
                else if ("--standalone".StartsWith(args[i])) {
                    standalone = true;
                }
                else {
                    Usage("Invalid argument: " + args[i]);
                    return;
                }
            }

            UW.ClassroomPresenter.Viewer.ViewerForm.ViewerThreadStart(inputFiles, standalone);
        }

        private static void Usage(string err) {
            string thisExe = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine(err);
            Console.WriteLine(thisExe + " (Version " + version + ")");
            Console.WriteLine("Usage: " + thisExe + " [--input <file>]");
            Console.WriteLine("Arguments:");
            Console.WriteLine("--input      Specify a PPT or CP3 file to open with Presenter");
            String msg = err +  "\r\n " + thisExe + " (Version " + version + ")\r\n" + 
                "Usage: " + thisExe + " [--input <file>] \r\n" +
                "Arguments:\r\n" + "--input      Specify a PPT or CP3 file to open with Presenter";
            MessageBox.Show(msg, "Usage", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}
