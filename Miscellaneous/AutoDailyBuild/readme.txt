// $Id: readme.txt 843 2005-12-29 22:36:48Z shoat $

AutoDailyBuild - Oliver Chung (shoat@cs)

***Instructions for configuring the settings and environment***

Configurable settings for the AutoDailyBuild system are located at the top of the AutoDailyBuild.cs file and need to be compiled each time they are changed.

The following are the original settings, as configured to run on "tofurkey" on the "pediddle" account.

            string pathToVSVARS32 = @"C:\Program Files\Microsoft Visual Studio 8\Common7\Tools\vsvars32.bat";
            string drive = "c:";
            string pathToSVNROOT = @"\Classroom Presenter Daily Build";
            string pathToSVNTRUNK = pathToSVNROOT + @"\trunk";
            string pathToSVNBIN = drive + @"\Program Files\Subversion\bin\svn.exe";
            string pathToSVNVERSIONBIN = drive + @"\Program Files\Subversion\bin\svnversion.exe";
            string emailFrom = "pediddle@cs.washington.edu";
            string emailTo = "presenter-dev@cs.washington.edu, shoat@cs.washington.edu";
            string emailServer = "mailhost.cs.washington.edu";
            bool debugMode = false;

***Instructions for configuring the scheduled task***

The AutoDailyBuild system is designed to run unattended on a system using the Windows Scheduled Task feature.  Scheduled Tasks is located in the Control Panels.  To create a new task, simply use the "Add Scheduled Task" wizard and follow the instructions.  You will need to specify the path to the AutoDailyBuild.exe, a user account (and password for that account) so that it can run regardless of who is logged in to the current system, and a scheduled run time.

The following are the original settings, as configured to run on "tofurkey" on the "pediddle" account.

Run: C:\Classroom Presenter Daily Build\trunk\Miscellaneous\AutoDailyBuild\bin\Debug\AutoDailyBuild.exe
Start in: C:\Classroom Presenter Daily Build\trunk\Miscellaneous\AutoDailyBuild\bin\Debug
Run as: CSERESEARCH\pediddle
Run only if logged on: Unchecked
Enabled (scheduled task runs at specified time): Checked
Scheduled to run: Every day at 12:00 AM
Delete the task if it is not scheduled to run again: Unchecked
Stop the task if it runs for 1 hours 0 minutes: Checked
Only start the task if the computer has been idle for at least ___ minutes: Unchecked
Stop the task if the computer ceases to be idle: Unchecked
Don't start the task if the computer is running on batteries: Unchecked
Stop the task if battery mode begins: Unchecked
Wake the computer to run this task: Checked

***Instructions for configuring SVN***

AutoDailyBuild assumes that ALL SVN credentials are cached before running.  In order to set up the system on a new computer or account, some steps need to be followed to ensure this occurs:

-Log in to the windows account that will be hosting AutoDailyBuild
-Install TortoiseSVN and command-line SVN tools
-Check out a copy of the Root of the SVN directory (no development should be done in this directory!)
-Configure the paths and settings for AutoDailyBuild, using the directories and executables from the previous steps.
-Commit a change to SVN using TortioseSVN and cache your web login credentials.  Confirm that the commit was successful.
-Manually run the AutoDailyBuild.exe program and ensure that there are no errors.  If errors are encountered, the best way to diagnose them is to set DebugMode and examine the output when running AutoDailyBuild.exe manually.  
-Add a new scheduled task for AutoDailyBuild.exe and ensure it runs as expected.

