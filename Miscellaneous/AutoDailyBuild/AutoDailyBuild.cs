// $Id: AutoDailyBuild.cs 1869 2009-05-22 18:57:21Z fred $

using System;
using System.IO;
using System.Diagnostics;
using System.Net.Mail;
using System.Text;

namespace AutoDailyBuild {
    /// <summary>
    /// Summary description for AutoDailyBuild.
    /// </summary>
    class AutoDailyBuild {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args) {
            //Paths and settings
            string pathToVSVARS32 = @"C:\Program Files\Microsoft Visual Studio 8\Common7\Tools\vsvars32.bat";
            string drive = "C:";
            string pathToSVNROOT = @"\CP3DailyBuild";
            string pathToSVNTRUNK = pathToSVNROOT + @"\trunk";
            string pathToSVNBIN = @"C:\Program Files\CollabNet Subversion Client\svn.exe";
            string pathToSVNVERSIONBIN = @"c:\Program Files\CollabNet Subversion Client\svnversion.exe";
            string adminEmail = "fred@cs.washington.edu";
            string devListEmail = "presenter-dev@cs.washington.edu";
            string emailServer = "mailhost.cs.washington.edu";
            bool debugMode = false;

            //Build the compilation batch file
            string compileBAT = "";
            //Read in @"C:\Program Files\Microsoft Visual Studio 8\Common7\Tools\vsvars32.bat"
            FileStream fs = new FileStream(pathToVSVARS32, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            compileBAT += sr.ReadToEnd();
            sr.Close();
            fs.Close();

            //Append devenv build command line
            compileBAT += drive + "\r\n";
            compileBAT += "cd \"" + pathToSVNROOT + "\"\r\n";
            compileBAT += "\"" + pathToSVNBIN + "\" update" + "\r\n";
            compileBAT += "cd \"" + pathToSVNTRUNK + @"\presentersetup" + "\"\r\n";
            compileBAT += "devenv presentersetup.sln /rebuild debug /out \"" + drive + pathToSVNROOT + "\\builds\\buildresult.log\"" + "\r\n";
            compileBAT += "date /T > \"" + drive + pathToSVNROOT + "\\builds\\buildtime.txt\"" + "\r\n";
            compileBAT += "time /T >> \"" + drive + pathToSVNROOT + "\\builds\\buildtime.txt\"" + "\r\n";
            compileBAT += "cd \"" + pathToSVNROOT + "\"\r\n";
            compileBAT += "\"" + pathToSVNVERSIONBIN + "\"" + " . >> \"" + drive + pathToSVNROOT + "\\builds\\buildtime.txt\"" + "\r\n";
            if (debugMode) {
                compileBAT += "pause" + "\r\n";  //FOR DEBUGGING
            }

            //Write bat file to temp folder

            fs = new FileStream(drive + "\\buildpresenter.bat", FileMode.Create, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);
            sw.Write(compileBAT);
            sw.Flush();
            fs.Flush();
            sw.Close();
            fs.Close();
            
//            //Delete the old log file, if it exists
            if (File.Exists(drive + pathToSVNROOT + "\\builds\\buildresult.log")) {
                File.Delete(drive + pathToSVNROOT + "\\builds\\buildresult.log");
            }

            //Execute compile batch file
            Process exec = new Process();
            exec.StartInfo.FileName = drive + "\\buildpresenter.bat";
            exec.Start();
            exec.WaitForExit();

            //Parse build log for errors
            /*Look for the following text
             ---------------------- Done ----------------------

                Rebuild All: 7 succeeded, 0 failed, 0 skipped
    
            */
            try {
                fs = new FileStream(drive + pathToSVNROOT + "\\builds\\buildresult.log", FileMode.Open, FileAccess.Read);
                sr = new StreamReader(fs);
            } catch (Exception) {
                //An error occured
                //Send fail email
                string subject = "Daily Build FAILED - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                string body = "A serious error occurred while compiling the project.  No build log could be retrieved.  Build has not been committed to the repository.";
                AutoDailyBuild.SendEmail(emailServer, adminEmail, devListEmail, subject, body, "");
                return;
            }
            string log = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            int resultLocation = log.LastIndexOf("Rebuild All:");
            if (resultLocation < 0) {
                //An error occured
                //Send fail email, attach log
                string subject = "Daily Build FAILED - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                string body = "A serious error occurred while compiling the project.  Build log could not be parsed.  Build has not been committed to the repository.";
                AutoDailyBuild.SendEmail(emailServer, adminEmail, devListEmail, subject, body, drive + pathToSVNROOT + "\\builds\\buildresult.log");
                return;
            }
            string result = log.Substring(resultLocation);
            //Result looks like this:  "========== Rebuild All: 7 succeeded, 0 failed, 0 skipped ==========\r\n\0\r\n\r\n\r\n"
            //Clean it up a bit before parsing
            result = result.Replace("\r\n", "");
            result = result.Replace("\0", "");
            result = result.Replace("Rebuild All: ", "");
            //Result now looks like this: "7 succeeded, 0 failed, 0 skipped"
            result = result.Replace(" succeeded", "");
            result = result.Replace(" failed", "");
            result = result.Replace(" skipped", "");
            result = result.Replace("==========", "");
            result = result.Replace(" ", "");
            //Result now looks like this: "7,0,0"
            string[] parsedResults = result.Split(',');
            //Grab the number failed and check it
            int numFailed = int.Parse(parsedResults[1]);
            if (numFailed == 0) {
                //If no errors - move yesterday's build folders, copy msi to daily build directory, send email success results
                //Parse the buildtime.txt file
                /* Expected format:
                   Mon 07/18/2005 
                   01:29 PM
                   123:123MS
                */

                //Begin the folder copying and SVN commit section
                string dirCopyBAT = "";

                try {
                    fs = new FileStream(drive + pathToSVNROOT + "\\builds\\current build\\buildtime.txt", FileMode.Open, FileAccess.Read);
                    sr = new StreamReader(fs);
                    string lastBuildString = sr.ReadLine();
                    string lastBuildTime = sr.ReadLine();
                    string lastVersion = sr.ReadLine();
                    sr.Close();
                    fs.Close();
                    string[] split = lastBuildString.Split(' ');
                    string lastDate = split[1];
                    //We want the lastDate in the following format for sortability: yyyy-mm-dd
                    string[] split1 = lastDate.Split('/');
                    string year = split1[2];
                    string month = split1[0];
                    string day = split1[1];
                    lastDate = year + "-" + month + "-" + day;

                    //Parse + clean up the version
                    //Clean up the output, because we can have letters and :'s 
                    /*
                    Possible output:
                    4123          normal
                    4123:4168     mixed revision working copy
                    4168M         modified working copy
                    4123S         switched working copy
                    4123:4168MS   mixed revision, modified, switched working copy
                    */
                    lastVersion = lastVersion.Replace("M", "");
                    lastVersion = lastVersion.Replace("S", "");
                    lastVersion = lastVersion.Replace("\r\n", "");
                    //If there is a colon, get the second number
                    if (lastVersion.IndexOf(':') != -1) {
                        //There is a colon!
                        string[] versiontemp = lastVersion.Split(':');
                        lastVersion = versiontemp[1];
                    }

                    //Compare the lastVersion with this version and see if they have changed
                    try {
                        fs = new FileStream(drive + pathToSVNROOT + "\\builds\\buildtime.txt", FileMode.Open, FileAccess.Read);
                        sr = new StreamReader(fs);
                        sr.ReadLine();  //Build string
                        sr.ReadLine();  //Build time
                        string currentVersion = sr.ReadLine();
                        sr.Close();
                        fs.Close();
                        currentVersion = currentVersion.Replace("M", "");
                        currentVersion = currentVersion.Replace("S", "");
                        currentVersion = currentVersion.Replace("\r\n", "");
                        //If there is a colon, get the second number
                        if (currentVersion.IndexOf(':') != -1) {
                            //There is a colon!
                            string[] versiontemp = currentVersion.Split(':');
                            currentVersion = versiontemp[1];
                        }
                        //Now compare
                        int currentv = Int32.Parse(currentVersion);
                        int lastv = Int32.Parse(lastVersion);
                        if (lastv + 1 == currentv) {
                            //Don't commit, send skip email
                            //Don't send the "SKIPPED" email to everyone.
                            string subject = "Daily Build SKIPPED - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                            string body = "Daily Build has not been committed to the repository, since it has not changed from the last build.";
                            AutoDailyBuild.SendEmail(emailServer, adminEmail, adminEmail, subject, body, "");
                            return;
                        }

                    } catch (System.IO.FileNotFoundException) {
                        //Not found, ignore and continue
                    }

                    string lastDirName = lastVersion + "_" + lastDate;

                    dirCopyBAT += drive + "\r\n";
                    dirCopyBAT += "cd \"" + pathToSVNROOT + "\\builds" + "\"\r\n";
                    //Check if this directory already exists, because if it does, we are trying to compile more than once today
                    if (Directory.Exists(drive + pathToSVNROOT + "\\builds\\" + lastDirName)) {
                        //So, we need to do a file system copy of the files to save space
                        dirCopyBAT += "copy /Y \"current build\\*.*\" \"" + lastDirName + "\"\r\n";
                    } else {
                        //The directory does not exist, use svn to copy the directory there
                        dirCopyBAT += "\"" + pathToSVNBIN + "\"" + " copy \"current build\" \"" + lastDirName + "\"\r\n";
                    }
                } catch (System.IO.FileNotFoundException) {
                    //buildtime.txt was not found, ignore and continue
                }

                //Now copy the new files over
                dirCopyBAT += "copy /Y \"" + drive + pathToSVNTRUNK + "\\presentersetup\\debug\\presentersetup.msi\" \"" + drive + pathToSVNROOT + "\\builds\\current build\"" + "\r\n";
                dirCopyBAT += "move /Y \"" + drive + pathToSVNROOT + "\\builds\\buildresult.log\" \"" + drive + pathToSVNROOT + "\\builds\\current build\"" + "\r\n";
                dirCopyBAT += "move /Y \"" + drive + pathToSVNROOT + "\\builds\\buildtime.txt\" \"" + drive + pathToSVNROOT + "\\builds\\current build\"" + "\r\n";

                //Now do svn commit
                string todaysdate = DateTime.Today.ToShortDateString();
                dirCopyBAT += "\"" + pathToSVNBIN + "\" commit " + "\"" + drive + pathToSVNROOT + "\\builds\" -m \"" + todaysdate + " Daily Build\"" + "\r\n";

                if (debugMode) {
                    dirCopyBAT += "pause\r\n";
                }

                //Output the bat
                fs = new FileStream(drive + "\\dircopy.bat", FileMode.Create, FileAccess.ReadWrite);
                sw = new StreamWriter(fs);
                sw.Write(dirCopyBAT);
                sw.Flush();
                fs.Flush();
                sw.Close();
                fs.Close();
                //Exec the bat
                Process execDirCopyBAT = new Process();
                execDirCopyBAT.StartInfo.FileName = drive + "\\dircopy.bat";
                execDirCopyBAT.Start();
                execDirCopyBAT.WaitForExit();
                //Send email

                //Done
            } else {
                //If errors - send fail email, attach build log
                string subject = "Daily Build FAILED - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                string body = "" + numFailed + " errors found while compiling project.  Build has not been committed to the repository.";
                AutoDailyBuild.SendEmail(emailServer, adminEmail, devListEmail, subject, body, drive + pathToSVNROOT + "\\builds\\buildresult.log");
            }
            //Done!
        }

        static void SendEmail(string server, string from, string to, string subject, string body, string attachment) {
            using (MailMessage msg = new MailMessage(from, to, subject, body)) {
                msg.BodyEncoding = Encoding.UTF8;
                if (attachment != "" && attachment != null) {
                    if (File.Exists(attachment)) {
                        msg.Attachments.Add(new Attachment(attachment));
                    }
                }

                SmtpClient smtp = new SmtpClient(server);
                smtp.Send(msg);
            }
        }
    }
}
