// $Id: DeckBuilder.cs 1723 2008-08-28 18:54:34Z anderson $

using System;
using System.IO;
using UW.ClassroomPresenter.DeckBuilderLibrary;
using Application = System.Windows.Forms.Application;

namespace UW.ClassroomPresenter.DeckBuilder {
    /// <summary>
    /// DeckBuilder is a executable wrapper for DeckBuilderLibrary
    /// </summary>
     
    public class DeckBuilder {
        
        [STAThread]
        public static void Main(string[] args) {
            Console.WriteLine(Application.ProductName + " (Version " + Application.ProductVersion + ") - ");
            //Parse args
            string inputFile = "";
            string outputFile = "";
            for (int i = 0; i < args.Length; i++) {
                if ("--input".StartsWith(args[i])) {
                    if ((i + 1) >= args.Length) {
                        Console.WriteLine("Missing file argument for --input");
                        DeckBuilder.Usage();
                        return;
                    }
                    if ("--".StartsWith(args[i+1])) {
                        Console.WriteLine("Missing file argument for --input");
                        DeckBuilder.Usage();
                        return;
                    }
                    inputFile = args[i+1];
                    i++;
                    if (!File.Exists(inputFile)) {
                        Console.WriteLine("File not found: " + inputFile);
                        DeckBuilder.Usage();
                        return;
                    }
                } else if ("--output".StartsWith(args[i])) {
                    if ((i + 1) >= args.Length) {
                        Console.WriteLine("Missing file argument for --output");
                        DeckBuilder.Usage();
                        return;
                    }
                    if ("--".StartsWith(args[i+1])) {
                        Console.WriteLine("Missing file argument for --output");
                        DeckBuilder.Usage();
                        return;
                    }
                    outputFile = args[i+1];
                    i++;
                } else if ("--help".StartsWith(args[i])) {
                    DeckBuilder.Usage();
                    return;
                } else {
                    Console.WriteLine("Invalid argument: " + args[i]);
                    DeckBuilder.Usage();
                    return;
                }
            }
            DeckBuilderForm dbf = new DeckBuilderForm();
            if (outputFile != "") {
                if (inputFile != null) {
                    //Fully silent mode
                    dbf.OpenFileHelper(new FileInfo(inputFile), false);
                    dbf.saveFileHelper(new FileInfo(outputFile));
                    return;
                } else {
                    Console.WriteLine("Input file not specified");
                    DeckBuilder.Usage();
                    return;
                }
            } else {
                if (inputFile != "") {
                    //UI, but preload file
                    dbf.OpenFileHelper(new FileInfo(inputFile), true);
                }
                //Normal
                System.Windows.Forms.Application.Run(dbf);
            }
        }

        public static void Usage() {
            //Add versioning information
            Console.WriteLine("Usage: DeckBuilder.Exe [--input <filename> [--output <filename>]] | [--help]");
            Console.WriteLine("Arguments:");
            Console.WriteLine("--input      Specify a PPT or CP3 file to open with DeckBuilder");
            Console.WriteLine("--output     Specify an output CP3 file.");
            Console.WriteLine("--help       This help dialog");
        }
    }
}
