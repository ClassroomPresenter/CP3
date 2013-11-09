using System;
using System.Collections;
using System.IO;

namespace UW.ClassroomPresenter.Scripting {
    /// <summary>
    /// Represents a list of events that should be executed in a script
    /// TODO: Should be replaced with a tree of events to allow branching/conditionals
    /// 
    /// NOTE: Events have the following syntax
    ///       [event_name]([event_parameters]);
    /// </summary>
    public class Script {
        /// <summary>
        /// ArrayList containing the events in this script
        /// </summary>
        protected ArrayList m_Events;
        public ArrayList Events {
            get { return this.m_Events; }
        }

        /// <summary>
        /// Constructs a script from the given filename
        /// </summary>
        /// <param name="filename"></param>
        public Script( string filename ) {
            this.m_Events = new ArrayList();

            if( File.Exists( filename ) ) {
                string data = File.ReadAllText( filename );
                ParseScript( data );
            }
        }

        /// <summary>
        /// Static method that parses a "\" separated list of strings
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string[] ParsePath( string s ) {
            return s.Split( new char[] { '\\' } );
        }

        #region Script Parsing

        /// <summary>
        /// Parse out a script file, as one giant string
        /// </summary>
        /// <param name="scriptString">The string to parse</param>
        public void ParseScript( string scriptString ) {
            // Split lines
            string[] commands = scriptString.Split( new char[] { ';' } );
            for( int i=0; i<commands.Length; i++ ) {
                IScriptEvent ev = ParseCommand( i, commands[i] );
                if( ev != null )
                    this.m_Events.Add( ev );
            }
        }

        /// <summary>
        /// Parse a single line of the script
        /// </summary>
        /// <param name="commandNum">The command number</param>
        /// <param name="commandString">The string for this command</param>
        /// <returns>The script command event object</returns>
        protected IScriptEvent ParseCommand( int commandNum, string commandString ) {
            // Trim whitespace
            commandString = commandString.Trim();
            if( commandString == "" )
                return null;

            // Syntax Check
            if( !commandString.EndsWith( ")" ) ) {
                throw new Exception( "Invalid Syntax: Command must end with \")\" - Number: " + commandNum );
            }

            // Parse out the command
            int i = commandString.IndexOfAny( new char[] { '(' } );
            if( i == -1 )
                throw new Exception( "Invalid Syntax: Parameters must begin with \"(\" - Number: " + commandNum );

            string command = commandString.Substring( 0, i );
            command = command.Trim();
            foreach( char c in command ) {
                if( !(Char.IsLetterOrDigit( c )) &&
                    !(c == '_') )
                    throw new Exception( "Invalid Syntax: Bad command character - Number: " + commandNum );
            }

            // Get the parameters
            string parameterString = commandString.Substring( i+1 );
            parameterString = parameterString.Substring( 0, parameterString.Length - 1 );
            ArrayList param = ParseParameters( commandNum, parameterString );

            // Construct the appropriate command
            return BuildCommand( command, param );
        }

        /// <summary>
        /// Parses a string containing the parameters for a command
        /// </summary>
        /// <param name="commandNum"></param>
        /// <param name="parameterString"></param>
        /// <returns></returns>
        protected ArrayList ParseParameters( int commandNum, string parameterString ) {
            ArrayList param = new ArrayList();
            string currentCommand = "";

            bool bInQuotes = false;
            bool bLastCharSlash = false;
            for( int i = 0; i < parameterString.Length; i++ ) {
                char c = parameterString[i];

                // Check to see if we need to enter or leave the quotes
                if( c == '\"' && !bInQuotes ) {
                    bInQuotes = true;
                    bLastCharSlash = false;
                    continue;
                } else if( c == '\"' && bInQuotes && !bLastCharSlash ) {
                    bInQuotes = false;
                    bLastCharSlash = false;
                    continue;
                }

                // If we are in quotes act differently than otherwise
                if( bInQuotes ) {
                    if( bLastCharSlash ) {
                        switch( c ) {
                            case 'n': currentCommand += "\n";
                                break;
                            case 't': currentCommand += "\t";
                                break;
                            case 'b': currentCommand += "\b";
                                break;
                            case 'r': currentCommand += "\r";
                                break;
                            case '\\': currentCommand += "\\";
                                break;
                            case '\"': currentCommand += "\"";
                                break;
                            case '\'': currentCommand += "\'";
                                break;
                            default: currentCommand += c;
                                break;
                        }
                        bLastCharSlash = false;
                        continue;
                    } else if( c == '\\' ) {
                        bLastCharSlash = true;
                        continue;
                    } else {
                        currentCommand += c;
                        bLastCharSlash = false;
                        continue;
                    }
                } else {
                    if( c == ',' ) {
                        param.Add( currentCommand );                        
                        currentCommand = "";
                    } else if( Char.IsLetterOrDigit( c ) ||
                        c == '_' ) {
                        currentCommand += c;
                    }
                    bLastCharSlash = false;
                    continue;
                }
            }

            if( currentCommand != "" )
                param.Add( currentCommand );

            return param;
        }

        #endregion

        /// <summary>
        /// Build the appropriate command from the string and parameters
        /// </summary>
        /// <param name="command">The string of the command to build</param>
        /// <param name="param">The array of parameters</param>
        /// <returns>The script event</returns>
        protected IScriptEvent BuildCommand( string command, ArrayList param ) {
            if( command.ToUpper() == "WAIT" )
                return new Events.WaitEvent( param );
            if( command.ToUpper() == "RANDWAIT" )
                return new Events.RandomWaitEvent( param );
            if( command.ToUpper() == "CLICK" )
                return new Events.ClickEvent( param );
            if( command.ToUpper() == "MENUCLICK" )
                return new Events.MenuClickEvent( param );
            if( command.ToUpper() == "INKDRAWING" )
                return new Events.InkDrawingEvent( param );
            return null;
        }
    }
}
