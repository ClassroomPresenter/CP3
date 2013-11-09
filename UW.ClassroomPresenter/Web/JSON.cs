using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UW.ClassroomPresenter.Web {
    public class JSON {
        public enum Token {
            NONE,
            OBJECT_OPEN,
            OBJECT_CLOSE,
            ARRAY_OPEN,
            ARRAY_CLOSE,
            KEY_VALUE_SEPARATOR,
            FIELD_SEPARATOR,
            STRING,
            NUMBER,
            TRUE,
            FALSE,
            NULL
        }

        // Decode the json string.
        public static object Decode(string json) {
          if (json != null) {
            char[] charArray = json.ToCharArray();
            int index = 0;
            bool success = true;
            object value = ParseValue(charArray, ref index, ref success);
            return value;
          }
          else {
            return null;
          }
        }


        public static string Encode(object json) {
            // Not Implemented
            return null;
        }

        // Parse an arbitrary value.
        protected static object ParseValue(char[] json, ref int index, ref bool success) {
            Token token = LookAhead(json, index);
            switch (token) {
                case Token.STRING:
                    return ParseString(json, ref index, ref success);
                case Token.NUMBER:
                    return ParseNumber(json, ref index, ref success);
                case Token.OBJECT_OPEN:
                    return ParseObject(json, ref index, ref success);
                case Token.ARRAY_OPEN:
                    return ParseArray(json, ref index, ref success);
                case Token.TRUE:
                    NextToken(json, ref index);
                    return true;
                case Token.FALSE:
                    NextToken(json, ref index);
                    return false;
                case Token.NULL:
                    NextToken(json, ref index);
                    return null;
                default:
                    break;
            }

            success = false;
            return null;
        }

        // Parse a JSON object.
        protected static List<KeyValuePair<string, object>> ParseObject(char[] json, ref int index, ref bool success) {
            List<KeyValuePair<string, object>> table = new List<KeyValuePair<string, object>>();

            // {
            NextToken(json, ref index);
            while (true) {
                Token token = LookAhead(json, index);
                switch (token) {
                    case Token.NONE:            // None
                        success = false;
                        return null;
                    case Token.FIELD_SEPARATOR: // Comma
                        NextToken(json, ref index);
                        break;
                    case Token.OBJECT_CLOSE:    // Curly-brace close
                        NextToken(json, ref index);
                        return table;
                    default:                    // Field
                        // Parse the name
                        string name = ParseString(json, ref index, ref success);
                        if (!success) {
                            success = false;
                            return null;
                        }
                        // Parse the separator (':')
                        token = NextToken(json, ref index);
                        if (token != JSON.Token.KEY_VALUE_SEPARATOR) {
                            success = false;
                            return null;
                        }
                        // Parse the value
                        object value = ParseValue(json, ref index, ref success);
                        if (!success) {
                            success = false;
                            return null;
                        }
                        table.Add(new KeyValuePair<string,object>(name, value));
                        break;
                }
            }
        }

        // Parse a JSON array.
        protected static List<object> ParseArray(char[] json, ref int index, ref bool success) {
            List<object> array = new List<object>();

            // [
            NextToken(json, ref index);
            while (true) {
                Token token = LookAhead(json, index);
                switch (token) {
                    case Token.NONE: // None
                        success = false;
                        return null;
                    case Token.FIELD_SEPARATOR: // Comma
                        NextToken(json, ref index);
                        break;
                    case Token.ARRAY_CLOSE:
                        NextToken(json, ref index);
                        return array;
                    default:
                        object value = ParseValue(json, ref index, ref success);
                        if (!success) {
                            return null;
                        }
                        array.Add(value);
                        break;
                }
            }
        }

        // Parse a JSON string.
        protected static string ParseString(char[] json, ref int index, ref bool success) {
            StringBuilder s = new StringBuilder();
            char c;

            EatWhitespace(json, ref index);

            // "
            c = json[index++];

            bool complete = false;
            while (!complete) {
                if (index == json.Length) {
                    break;
                }

                c = json[index++];
                if (c == '"') {
                    complete = true;
                    break;
                }
                else if (c == '\\') {
                    if (index == json.Length) {
                        break;
                    }
                    c = json[index++];
                    if (c == '"') {
                        s.Append('"');
                    }
                    else if (c == '\\') {
                        s.Append('\\');
                    }
                    else if (c == '/') {
                        s.Append('/');
                    }
                    else if (c == 'b') {
                        s.Append('\b');
                    }
                    else if (c == 'f') {
                        s.Append('\f');
                    }
                    else if (c == 'n') {
                        s.Append('\n');
                    }
                    else if (c == 'r') {
                        s.Append('\r');
                    }
                    else if (c == 't') {
                        s.Append('\t');
                    }
                    else if (c == 'u') {
                        int remainingLength = json.Length - index;
                        if (remainingLength >= 4) {
                            // parse the 32 bit hex into an integer codepoint
                            uint codePoint;
                            if (!(success = UInt32.TryParse(new string(json, index, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out codePoint))) {
                                return "";
                            }
                            // convert the integer codepoint to a unicode char and add to string
                            s.Append(Char.ConvertFromUtf32((int)codePoint));
                            // skip 4 chars
                            index += 4;
                        }
                        else {
                            break;
                        }
                    }
                }
                else {
                    s.Append(c);
                }
            }

            if (!complete) {
                success = false;
                return null;
            }

            return s.ToString();
        }

        // Parse an input number.
        protected static double ParseNumber(char[] json, ref int index, ref bool success) {
            EatWhitespace(json, ref index);

            int lastIndex = GetLastIndexOfNumber(json, index);
            int charLength = (lastIndex - index) + 1;
            double number;
            success = Double.TryParse(new string(json, index, charLength), out number);
            index = lastIndex + 1;
            return number;
        }

        // Quick and dirty look ahead to find symbols that can be in a number.
        protected static int GetLastIndexOfNumber(char[] json, int index) {
            int lastIndex;
            for (lastIndex = index; lastIndex < json.Length; lastIndex++) {
                if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1) {
                    break;
                }
            }
            return lastIndex - 1;
        }

        // Quick and dirty look ahead to find symbols that are whitespace.
        protected static void EatWhitespace(char[] json, ref int index) {
            for (; index < json.Length; index++) {
                if (" \t\n\r".IndexOf(json[index]) == -1) {
                    break;
                }
            }
        }

        // Peek the next token.
        protected static Token LookAhead(char[] json, int index) {
            int saveIndex = index;
            return NextToken(json, ref saveIndex);
        }

        // Consume the next token.
        protected static Token NextToken(char[] json, ref int index) {
            EatWhitespace(json, ref index);

            if (index == json.Length) {
                return Token.NONE;
            }

            char c = json[index];
            index++;
            switch (c) {
                case '{':
                    return Token.OBJECT_OPEN;
                case '}':
                    return Token.OBJECT_CLOSE;
                case '[':
                    return Token.ARRAY_OPEN;
                case ']':
                    return Token.ARRAY_CLOSE;
                case ',':
                    return Token.FIELD_SEPARATOR;
                case '"':
                    return Token.STRING;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-':
                    return Token.NUMBER;
                case ':':
                    return Token.KEY_VALUE_SEPARATOR;
            }
            index--;

            int remainingLength = json.Length - index;

            // false
            if (remainingLength >= 5) {
                if (json[index] == 'f' &&
                    json[index + 1] == 'a' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 's' &&
                    json[index + 4] == 'e') {
                    index += 5;
                    return Token.FALSE;
                }
            }

            // true
            if (remainingLength >= 4) {
                if (json[index] == 't' &&
                    json[index + 1] == 'r' &&
                    json[index + 2] == 'u' &&
                    json[index + 3] == 'e') {
                    index += 4;
                    return Token.TRUE;
                }
            }

            // null
            if (remainingLength >= 4) {
                if (json[index] == 'n' &&
                    json[index + 1] == 'u' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 'l') {
                    index += 4;
                    return Token.NULL;
                }
            }

            return Token.NONE;
        }
    }
}
