using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Configuration;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.RTP {
    class RTPVenueManager {

        /// <summary>
        /// Compose a list of venues to use, combining default and optional custom venues from app.config.
        /// </summary>
        /// <returns></returns>
        public static List<Venue> GetVenues() {
            //Get custom venues from the config file, if any
            List<Venue> list = GetCustomVenues();
                
            if (list==null)
                 list = new List<Venue>();

            //Add the default venues
            list.Add(new Venue(Strings.Classroom + " 1", new IPEndPoint(IPAddress.Parse("234.3.0.1"), 5004)));
            list.Add(new Venue(Strings.Classroom + " 2", new IPEndPoint(IPAddress.Parse("234.3.0.2"), 5004)));
            //list.Add(new Venue(Strings.Classroom + " 3", new IPEndPoint(IPAddress.Parse("234.3.0.3"), 5004)));
            //list.Add(new Venue(Strings.Classroom + " 4", new IPEndPoint(IPAddress.Parse("234.3.0.4"), 5004)));
            //list.Add(new Venue(Strings.Classroom + " 5", new IPEndPoint(IPAddress.Parse("234.3.0.5"), 5004)));

            return list;
        }

        private static List<Venue> GetCustomVenues() {
            List<Venue> list = new List<Venue>();
            try {
                CustomVenuesSection cvSection = ConfigurationManager.GetSection("CustomVenues") as CustomVenuesSection;

                if (cvSection == null)
                    return null;

                CustomVenuesCollection cvCollection = cvSection.Venues;

                foreach (CustomVenueElement cvElement in cvCollection) {
                    Venue v = Venue.Parse(cvElement.Name, cvElement.Address, "5004");
                    if (v != null) {
                        list.Add(v);
                    }
                    else { 
                        Trace.WriteLine("Failed to parse venue address: " + cvElement.Address, "RTPVenueManager.GetCustomVenues");  
                    }
                }

            }
            catch (Exception e) {
                Trace.WriteLine("Failed to load custom venues: " + e.Message, "RTPVenueManager.GetCustomVenues");
                return null;
            }
            return list;
        }
    }

    /// <summary>
    /// A Venue is a multicast group address and a name.  This is also called a "Classroom" in some places in CP.
    /// </summary>
    public class Venue {
        public IPEndPoint VenueEndPoint;
        public string VenueName;

        public Venue(string venueName, IPEndPoint venueEndPoint) {
            this.VenueEndPoint = venueEndPoint;
            this.VenueName = venueName;
        }

        public static Venue Parse(string venueName, string venueAddress, string venuePort) { 
            int port;
            IPAddress address;
            if (Int32.TryParse(venuePort, out port)) {
                if (IPAddress.TryParse(venueAddress, out address)) {
                    return new Venue(venueName, new IPEndPoint(address, port));
                }
            }
            return null;
        }
    }

    #region Custom Config

    /// <summary>
    /// The following is used to support a custom config file section.
    /// </summary>
    /// Example:
    /// <?xml version="1.0" encoding="utf-8" ?>
    /// <configuration>
    ///  <configSections>
    ///     <section name="CustomVenues" type="UW.ClassroomPresenter.Network.RTP.CustomVenuesSection, UW.ClassroomPresenter" />
    ///  </configSections>
    ///  <CustomVenues>
    ///     <CustomVenue name="Custom venue 1" address="234.1.3.4"/>
    ///     <CustomVenue name="Custom venue 2" address="234.5.4.3"/>   
    ///  </CustomVenues>
    /// </configuration>

    public class CustomVenuesSection : ConfigurationSection {
        public CustomVenuesSection() {
        }
        [ConfigurationProperty("", IsDefaultCollection = true)]
        public CustomVenuesCollection Venues {
            get {
                return (CustomVenuesCollection)base[""];
            }
        }
    }
    public class CustomVenuesCollection : ConfigurationElementCollection {
        protected override ConfigurationElement CreateNewElement() {
            return new CustomVenueElement();
        }
        protected override object GetElementKey(ConfigurationElement element) {
            return ((CustomVenueElement)element).Name;
        }
        public override ConfigurationElementCollectionType CollectionType {
            get {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }
        protected override string ElementName {
            get {
                return "CustomVenue";
            }
        }
    }
    public class CustomVenueElement : ConfigurationElement {
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name {
            get {
                return (string)base["name"];
            }
            set {
                base["name"] = value;
            }
        }
        [ConfigurationProperty("address", IsRequired = true)]
        public string Address {
            get {
                return (string)base["address"];
            }
            set {
                base["address"] = value;
            }
        }

        public override string ToString() {
            return "CustomVenueElement: Name=" + Name + "; Address=" + Address;
        }
    }
    #endregion Custom Config
}
