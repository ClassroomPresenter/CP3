using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Diagnostics;
using System.Collections;

namespace UW.ClassroomPresenter.Misc {
    /// <summary>
    /// Helper class to handle serialization and deserialization for new types
    /// </summary>
    /// Before serializing new types for a set of receivers which may include nodes which do not
    /// have the new types, wrap them in ExtensionWrapper.  On the receiver,
    /// check the value of ExtensionType to make sure it matches a known type before
    /// accessing ExtensionObject.
    /// 
    /// //Sample code for adding an extension to a message
    /// message.Extension = new ExtensionWrapper(new FooClass("Foo"),FooClass.ExtensionId);
    /// 
    /// // Sample code for reading an extension   
    /// ExtensionWrapper extension = message.Extension as ExtensionWrapper;
    /// if (extension != null) {
    ///    if (extension.ExtensionType.Equals(FooClass.ExtensionId)) {
    ///        FooClass foo = (FooClass)extension.ExtensionObject;
    ///        Trace.WriteLine("foo.Bar=" + foo.Bar);
    ///    }
    ///    else if (extension.ExtensionType.Equals(BarClass.ExtensionId)) {
    ///        BarClass bar = (BarClass)extension.ExtensionObject;
    ///        Trace.WriteLine("bar.Foo=" + bar.Foo);
    ///    }
    ///    else {
    ///        Trace.WriteLine("Unknown Extension id=" + extension.ExtensionType.ToString());
    ///    }
    /// }
    ///
    [Serializable]
    public class ExtensionWrapper {
        private byte[] m_SerializedObject;
        private Guid m_ExtensionType;

        private Hashtable m_MultiExtensionWrapper;

        public ExtensionWrapper(object extensionObject, Guid extensionType) {
            m_ExtensionType = extensionType;
            if (extensionObject != null) {
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                bf.Serialize(ms, extensionObject);
                m_SerializedObject = ms.ToArray();
            }
            this.m_MultiExtensionWrapper = new Hashtable();
        }

        public Guid ExtensionType {
            get { return m_ExtensionType; }
        }

        public object ExtensionObject {
            get {
                if (m_SerializedObject == null) {
                    return null;
                }
                try {
                    BinaryFormatter bf = new BinaryFormatter();
                    MemoryStream ms = new MemoryStream(m_SerializedObject);
                    return bf.Deserialize(ms);
                }
                catch (Exception e) {
                    Trace.WriteLine("ExtensionObject deserialization failed.  Be sure to check the ExtensionType property " +
                        "prior to accessing the ExtensionObject.  " + e.ToString());
                }
                return null;
            }
        }

        public void AddExtension(ExtensionWrapper extension) {
            if (extension != null) {
                m_MultiExtensionWrapper.Add(extension.ExtensionType, extension);
            }
        }

        public ExtensionWrapper GetExtension(Guid extensionType) {
            return (ExtensionWrapper)this.m_MultiExtensionWrapper[extensionType];
        }

        public bool ContainsKey(Guid extensionType) {
            if (this.m_MultiExtensionWrapper.ContainsKey(extensionType))
                return true;
            else return false;
        }
    }

    ///// <summary>
    ///// Dummy classes for testing extensions
    ///// </summary>
    //[Serializable]
    //class FooClass {
    //    public static Guid ExtensionId = new Guid("{256B8677-82F0-41b1-AE76-2C52656E072B}");
    //    private string foo;
    //    public FooClass(String foo) {
    //        this.foo = foo;
    //    }
    //    public String Bar {
    //        get { return foo; }
    //    }
    //}
    //[Serializable]
    //class BarClass {
    //    public static Guid ExtensionId = new Guid("{9603025A-636B-4018-A33D-190FBB8C50B9}");
    //    private string bar;
    //    public BarClass(String bar) {
    //        this.bar = bar;
    //    }
    //    public String Foo {
    //        get { return bar; }
    //    }
    //}

}
