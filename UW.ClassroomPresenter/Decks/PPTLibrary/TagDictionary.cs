// $Id: TagDictionary.cs 2227 2013-07-22 23:23:44Z fred $

// Notes: it might be fun to set this up so that it can store any serializable object by using the Encode/Decode methods
// Notes: it's unclear what happens when a tag does not exist and the bracket operator is used on it.
// Response: it just returns the empty string: "".

using System;

namespace PPTLibrary {
    using System.Collections;
    using PowerPoint = Microsoft.Office.Interop.PowerPoint;
    using Core = Microsoft.Office.Core;
    using System.Diagnostics;

    /// <summary>
    /// A hashtable based on the Tags of a PowerPoint object.
    /// </summary>
    /// <remarks>
    /// Keep in mind that a TagDictionary is just a wrapper around the Tags object.
    /// That means, for example, that even if you were to perfectly synchronize access
    /// to this dictionary, someone might still have a handle to the underlying tags
    /// and change them. It also restricts the keys and values you can use. <b>Keys
    /// and values may only be strings.</b> Appropriate
    /// exceptions will be thrown if these conditions are violated (ArgumentException for
    /// non-strings and ArgumentOutOfRangeException for empty strings.)
    ///
    /// Furthermore, the actual keys/values stored in the Tags object may not be identical
    /// to the keys/values stored in this Dictionary object if modifications are necessary
    /// to ensure appropriate behavior. For example, all value strings starting with ' '
    /// might be prefixed with an extra ' ' when entered, and a string consisting only of
    /// ' ' might represent the empty string (so that containment tests can work
    /// properly).
    /// </remarks>
    public class TagDictionary : IDictionary {
        #region IDictionary

        public bool IsFixedSize { get { return false; } }
        public bool IsReadOnly { get { return false; } }
        public ICollection Keys {
            get {
                ArrayList list = new ArrayList();
                for (int i = 1; i <= this.Tags.Count; i++) {
                    string key = this.Tags.Name(i);
                    if (this.IsEncodedKey(key) &&
                        this.IsEncodedValue(this.Tags.Value(i)))
                        list.Add(this.DecodeKey(key));
                }
                return list;
            }
        }

        public ICollection Values {
            get {
                ArrayList list = new ArrayList();
                foreach (string key in this.Keys)
                    list.Add(this[key]);
                return list;
            }
        }

        object IDictionary.this[object key] {
            get {
                if (!(key is string)) throw new ArgumentException("must be a string", "key");
                return this[(string)key];
            }
            set {
                if (!(key is string)) throw new ArgumentException("must be a string", "key");
                if (!(value is string)) throw new ArgumentException("must be a string", "value");
                this[(string)key] = (string)value;
            }
        }

        public void Add(object key, object value) {
            if (!(key is string)) throw new ArgumentException("must be a string", "key");
            if (!(value is string)) throw new ArgumentException("must be a string", "value");
            this.Add((string)key, (string)value);
        }

        public void Clear() {
            foreach (string key in this.Keys)
                this.Remove(key);
        }

        public bool Contains(object key) {
            if (!(key is string)) throw new ArgumentException("must be a string", "key");
            return this.Contains((string)key);
        }

        public IDictionaryEnumerator GetEnumerator() {
            throw new NotSupportedException();
        }

        public void Remove(object key) {
            if (!(key is string)) throw new ArgumentException("must be a string", "key");
            this.Remove((string)key);
        }

        #endregion

        #region String-based Dictionary operations

        /// <remarks>
        /// Attempting to get a non-existant key returns an empty string and
        /// adds that key/value pair to the dictionary.
        /// </remarks>
        public string this[string key] {
            get {
                if (key == null) throw new ArgumentNullException("key");
                key = this.CheckOrMakeUpper(key);

                // Add the key with an empty string if it does not exist.
                if (!this.Contains(key))
                    this[key] = "";

                return this.DecodeValue(this.Tags[this.EncodeKey(key)]);
            }
            set {
                if (key == null) throw new ArgumentNullException("key");
                if (value == null) throw new ArgumentNullException("value");
                key = this.CheckOrMakeUpper(key);
                this.Tags.Add(this.EncodeKey(key), this.EncodeValue(value));
            }
        }

        public void Add(string key, string value) {
            if (key == null) throw new ArgumentNullException("key");
            if (value == null) throw new ArgumentNullException("value");
            key = this.CheckOrMakeUpper(key);
            if (this.Contains(key)) throw new ArgumentException("An element with the same key already exists", "key");
            this[key] = value;
        }

        /// <remarks>Encoding of values is used to ensure that
        /// Contains returns false if the key is not already in the
        /// table. The problem is that Tags automatically add a
        /// queried key with a value of ""; so, we need to distinguish
        /// between NOT PRESENT and EMPTY STRING.</remarks>
        public bool Contains(string key) {
            if (key == null) throw new ArgumentNullException("key");
            key = this.CheckOrMakeUpper(key);
            return this.IsEncodedValue(this.Tags[this.EncodeKey(key)]);
        }

        public void Remove(string key) {
            if (key == null) throw new ArgumentNullException("key");
            key = this.CheckOrMakeUpper(key);
            this.Tags.Delete(this.EncodeKey(key));
        }

        #endregion

        #region ICollection

        public int Count { get { return this.Keys.Count; } }

        public bool IsSynchronized { get { throw new NotSupportedException(); } }
        public object SyncRoot { get { throw new NotSupportedException(); } }
        public void CopyTo(Array array, int index) { throw new NotSupportedException(); }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() { throw new NotSupportedException(); }

        #endregion

        #region TagDictionary Properties

        public PowerPoint.Tags Tags { get { return this.myTags; } }
        public bool AllowLower { get { return this.myAllowLower; } }

        #endregion

        #region Methods

        public static string AsKey(string rawKey) { return CheckOrMakeUpper(rawKey, true); }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a dictionary wrapping a tags object. Defaults to allow lower-case. Name defaults to "".
        /// </summary>
        /// <param name="tags">the tags object to wrap</param>
        /// <remarks>if keys are not allowed to use lower-case, lower-case letters will cause an exception; otherwise, they will be transformed to upper-case</remarks>
        public TagDictionary(PowerPoint.Tags tags) : this(tags, true) { }

        /// <summary>
        /// Creates a dictionary wrapping a tags object. Name defaults to "".
        /// </summary>
        /// <param name="tags">the tags object to wrap</param>
        /// <param name="allowLower">should keys be allowed to use lower-case letters</param>
        /// <remarks>if keys are not allowed to use lower-case, lower-case letters will cause an exception; otherwise, they will be transformed to upper-case</remarks>
        public TagDictionary(PowerPoint.Tags tags, bool allowLower) : this(tags, allowLower, "") { }

        /// <summary>
        /// Creates a dictionary wrapping a tags object
        /// </summary>
        /// <param name="tags">the tags object to wrap</param>
        /// <param name="allowLower">should keys be allowed to use lower-case letters</param>
        /// <param name="name">a name to use for this tag dictionary; should be all uppercase (will cause
        /// an exception if allowLower is false and otherwise will be uppercased); only
        /// entries under this name will be affected by operations on this tag dictionary</param>
        /// <remarks>if keys are not allowed to use lower-case, lower-case letters will cause an exception; otherwise, they will be transformed to upper-case</remarks>
        public TagDictionary(PowerPoint.Tags tags, bool allowLower, string name) {
            this.myTags = tags;
            this.myAllowLower = allowLower;
            this.myName = this.CheckOrMakeUpper(name);
        }

        #endregion

        #region Members

        private const char SPECIAL_CHAR = '_';
        /// <summary>
        /// MUST be different from SPECIAL_CHAR.
        /// </summary>
        private const char SPECIAL_CHAR2 = '|';
        private static readonly string SPECIAL_STR = new string(SPECIAL_CHAR, 1);

        private string myName;
        private PowerPoint.Tags myTags;
        private bool myAllowLower;

        #endregion

        #region Internal methods

        /// <summary>
        /// Returns the external value for a value in the tag dictionary.
        /// </summary>
        private string DecodeValue(string s) {
            if (!IsEncodedValue(s))
                throw new ArgumentException("empty string has no decoded value", "s");

            if (s[0] == SPECIAL_CHAR)
                return s.Substring(1, s.Length - 1);
            else
                return s;
        }

        /// <summary>
        /// Returns the internal value for a value in the tag dictionary.
        /// </summary>
        private string EncodeValue(string s) {
            // Prepend a SPECIAL_CHAR if the string is empty or starts with
            // a special char. (The latter condition is just to ensure faithful
            // reproduction of strings starting with the SPECIAL_CHAR.)
            if (s.Length == 0 || s[0] == SPECIAL_CHAR)
                return SPECIAL_STR + s;
            else
                return s;
        }

        private bool IsEncodedValue(string s) {
            return (s.Length != 0);
        }

        private string EscapeKey(string key) {
            // Escape both special characters. Order here is important (only
            // escape each char once!).
            string result = key.Replace(SPECIAL_STR, SPECIAL_STR + SPECIAL_STR);
            return result.Replace(
                string.Format("{0}", SPECIAL_CHAR2),
                string.Format("{0}{1}", SPECIAL_CHAR, SPECIAL_CHAR2));
        }

        /// <summary>
        /// Breaks an encoded key into its name and key parts.
        /// </summary>
        private void BreakEncodedKey(string encodedKey, out string name, out string key) {
            if (encodedKey.Length == 0)
                throw new ArgumentException("not an encoded key: empty", "encodedKey");
            if (encodedKey[0] != SPECIAL_CHAR)
                throw new ArgumentException("not an encoded key: invalid starting character", "encodedKey");

            // Skip the first character.
            encodedKey = encodedKey.Substring(1, encodedKey.Length - 1);

            // Unescape the string, finding the unescaped SPECIAL_CHAR2
            name = "";
            key = "";
            bool escaped = false;
            bool foundBreak = false;
            foreach (char c in encodedKey) {
                if (!escaped && c == SPECIAL_CHAR2) {
                    if (foundBreak)
                        throw new ArgumentException("not an encoded key: too many breaks", "encodedKey");
                    foundBreak = true;
                }
                else if (!escaped && c == SPECIAL_CHAR)
                    escaped = true;
                else {
                    if (foundBreak)
                        key += c;
                    else
                        name += c;
                    escaped = false;
                }
            }
            if (escaped)
                throw new ArgumentException("not an encoded key: dangling escape character", "encodedKey");
            if (!foundBreak)
                throw new ArgumentException("not an encoded key: no break", "encodedKey");
        }

        /// <summary>
        /// Returns the external value for a key in the tag dictionary.
        /// </summary>
        private string DecodeKey(string s) {
            string name, key;
            try {
                this.BreakEncodedKey(s, out name, out key);
            }
            catch (ArgumentException e) {
                throw new ArgumentException("not a valid key", "s", e);
            }

            if (name != this.myName)
                throw new ArgumentException("not a key of this dictionary", "s");

            return key;
        }

        /// <summary>
        /// Returns the internal value for a key in the tag dictionary.
        /// </summary>
        private string EncodeKey(string s) {
            // New key (assuming SPECIAL_CHAR is _ and SPECIAL_CHAR2 is |):
            // _name|key
            // where all existing _ or | in name and key have been replaced by
            // double __ and _|.

            return string.Format("{0}{1}{2}{3}",
                SPECIAL_CHAR, this.EscapeKey(myName), SPECIAL_CHAR2, this.EscapeKey(s));
        }

        private bool IsEncodedKey(string s) {
            try {
                this.DecodeKey(s);
                return true;
            }
            catch (ArgumentException) {
                return false;
            }
        }

        private string CheckOrMakeUpper(string s) { return CheckOrMakeUpper(s, this.AllowLower); }

        private static string CheckOrMakeUpper(string s, bool allowLower) {
            // Check if the string is already non-lower-case.
            bool safe = true;
            foreach (char c in s)
                if (char.IsLower(c))
                    safe = false;
            if (safe)
                return s; // no lower-case, just return

            // If lower-case is allowed, fix it to be upper..
            if (allowLower)
                return s.ToUpper();
                // Otherwise, throw an exception.
            else
                throw new ArgumentException("may not contain lower-case letters", "s");
        }

        #endregion

        #region Test Drivers

        // Dump the contents of a TagDictionary.
        static void TestTagsDump(TagDictionary d1) {
            Console.WriteLine("Count: " + d1.Count);

            Console.WriteLine("Existing contents.");
            foreach (string s in d1.Keys)
                Console.WriteLine(s + ": " + d1[s]);

            Console.WriteLine("Existing contents as IDictionary.");
            IDictionary d = d1;
            foreach (string s in d.Keys)
                Console.WriteLine(s + ": " + d[s]);
        }

        // Test various operations on the dictionary.
        static bool TestTags(PowerPoint.Tags tags) {
            bool passed = true;
            TagDictionary d1 = new TagDictionary(tags, true, "|Bob_");

            TestTagsDump(d1);

            Console.WriteLine("Test clear...");
            d1.Add("test", "");
            TestTagsDump(d1);
            d1.Clear();

            if (d1.Count == 0)
                Console.WriteLine("Passed.");
            else {
                passed = false;
                Console.WriteLine("Failed.");
            }

            TestTagsDump(d1);

            Console.WriteLine("Testing access to missing element.");
            try {
                string s = d1["test"];
                if (s == "")
                    Console.WriteLine("Passed.");
                else {
                    passed = false;
                    Console.WriteLine("Failed.");
                }
            }
            catch (ArgumentException) {
                passed = false;
                Console.WriteLine("Failed.");
            }

            Console.WriteLine("Testing adding and accessing elements...");
            // Alternate entries are keys. Must be at least two pairs.
            string[] strings = new string[] { "hello", "Yes!", "FOO BAR!", "BaZ", "no", "D'oh!", "Farnsworth", "Zeke" };
            bool passedStrings = true;

            for (int i = 0; i < strings.Length; i += 2)
                d1.Add(strings[i], strings[i+1]);

            TestTagsDump(d1);

            for (int i = strings.Length - 2; i >= 0; i -= 2)
                if (d1[strings[i]] != strings[i+1])
                    passedStrings = false;
            if (passedStrings)
                Console.WriteLine("Passed.");
            else {
                passed = false;
                Console.WriteLine("Failed.");
            }

            Console.WriteLine("Testing removal...");
            d1.Remove(strings[2]);
            if (d1.Contains(strings[2])) {
                passed = false;
                Console.WriteLine("Failed.");
            }
            else
                Console.WriteLine("Passed.");

            Console.WriteLine("Testing overwrite...");
            d1[strings[0]] = strings[3];
            if (d1[strings[0]] == strings[3])
                Console.WriteLine("Passed.");
            else {
                passed = false;
                Console.WriteLine("Failed.");
            }

            Console.WriteLine("Testing null key...");
            try {
                d1.Add(null, "five");
                passed = false;
                Console.WriteLine("Failed.");
            }
            catch (ArgumentNullException) {
                Console.WriteLine("Passed.");
            }

            Console.WriteLine("Testing null value...");
            try {
                d1.Add("five", null);
                passed = false;
                Console.WriteLine("Failed.");
            }
            catch (ArgumentNullException) {
                Console.WriteLine("Passed.");
            }

            Console.WriteLine("Testing empty value...");
            try {
                d1.Add("five", "");
                if (d1["five"] == "") {
                    Console.WriteLine("Passed.");
                }
                else {
                    passed = false;
                    Console.WriteLine("Failed.");
                }
            }
            catch (ArgumentOutOfRangeException) {
                passed = false;
                Console.WriteLine("Failed.");
            }

            Console.WriteLine("Testing coexisting dictionaries separation...");
            TagDictionary d2 = new TagDictionary(tags, true, "");
            TagDictionary d3 = new TagDictionary(tags, true, "sam");
            d1["test"] = "one";
            d2["test"] = "two";
            d3["test"] = "three";

            if (d1["test"] != "one" || d2["test"] != "two" || d3["test"] != "three") {
                Console.WriteLine("Failed.");
                passed = false;
            }
            else
                Console.WriteLine("Passed.");

            Console.WriteLine("Testing coexisting dictionaries clearing...");
            d1.Clear();
            if (d1.Contains("test") || d2["test"] != "two" || d3["test"] != "three") {
                Console.WriteLine("Failed.");
                passed = false;
            }
            else
                Console.WriteLine("Passed.");

            Console.WriteLine("d1:");
            TestTagsDump(d1);
            Console.WriteLine("d2:");
            TestTagsDump(d2);
            Console.WriteLine("d3:");
            TestTagsDump(d3);

            return passed;
        }

        // Test driver.
        public static bool Test() {
            bool passed = true;
            PowerPoint.Application app = new PowerPoint.Application();
            //PowerPoint.Application app = new PowerPoint.ApplicationClass();
            app.Visible = Microsoft.Office.Core.MsoTriState.msoTrue;

            PowerPoint.Presentation presentation = app.Presentations.Add(Core.MsoTriState.msoFalse);
            PowerPoint.Slide slide = presentation.Slides.Add(1, PowerPoint.PpSlideLayout.ppLayoutBlank);
            PowerPoint.Shape shape = slide.Shapes.AddLine(0, 0, 100, 100);

            Console.WriteLine("Test on presentation.");
            passed = passed && TestTags(presentation.Tags);

            Console.WriteLine("Test on slide.");
            passed = passed && TestTags(slide.Tags);

            Console.WriteLine("Test on shape.");
            passed = passed && TestTags(shape.Tags);

            app = null;

            return passed;
        }

        #endregion
    }
}
