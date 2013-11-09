// $Id: ClassroomModel.cs 1499 2007-11-30 19:58:18Z fred $

using System;

using UW.ClassroomPresenter.Model.Presentation;
using System.Net;

namespace UW.ClassroomPresenter.Model.Network {

    /// <summary>
    /// Models the state of a "classroom", a.k.a. "venue".
    /// </summary>
    /// <remarks>
    /// At the network level, a classroom represents a connection to a server or RTP connection.
    /// A <c>ClassroomModel</c> is therefore instantiated and managed by a <see cref="ConnectionManager"/>.
    /// <para>
    /// The classroom is host to a set of <see cref="ParticipantModel">participants</see>, each of which may host a presentation.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ClassroomModel : PropertyPublisher {
        private readonly ProtocolModel m_Protocol;
        private readonly ParticipantCollection m_Participants;
        private readonly PresentationCollection m_Presentations;
        private IPEndPoint m_RtpEndPoint;
        private readonly ClassroomModelType m_ClassroomModelType;

        // Published properties:
        private string m_HumanName;
        private bool m_Connected;

        /// <summary>
        /// Creates a new <see cref="ClassroomModel"/> as a member of the given <see cref="ProtocolModel"/>,
        /// with the given Human Name.
        /// </summary>
        public ClassroomModel(ProtocolModel protocol, string humanName, ClassroomModelType classroomModelType) {
            this.m_Protocol = protocol;
            this.m_ClassroomModelType = classroomModelType;
            this.m_Participants = new ParticipantCollection(this, "Participants");
            this.m_Presentations = new PresentationCollection(this, "Presentations");
            this.m_HumanName = humanName;
            this.m_Connected = false;
            this.m_RtpEndPoint = null;
        }

        /// <summary>
        /// Gets the <see cref="ProtocolModel"/> to which this <see cref="ClassroomModel"/> belongs.
        /// </summary>
        /// <remarks>
        /// The <see cref="ProtocolModel"/> returned is the same that was passed to the
        /// <see cref="ClassroomModel(ProtocolModel)"/> constructor.
        /// </remarks>
        public ProtocolModel Protocol {
            get { return this.m_Protocol; }
        }

        /// <summary>
        /// Indicates whether classroom is static or dynamic (advertised), and if static, indicates multicast/unicast.
        /// </summary>
        public ClassroomModelType ClassroomModelType {
            get { return this.m_ClassroomModelType; }
        }

        /// <summary>
        /// The multicast IPEndPoint for this classroom.  This is only relevant for RTP classrooms.
        /// </summary>
        public IPEndPoint RtpEndPoint {
            get { return this.m_RtpEndPoint; }
            set { this.m_RtpEndPoint = value; }
        }

        /// <summary>
        /// Gets or sets the human-intelligible name for this classroom.
        /// </summary>
        /// <remarks>
        /// The <c>HumanName</c> should be either a textual name, an IP address,
        /// or some other text that can uniquely and intelligibly identify the classroom.
        /// </remarks>
        [Published] public string HumanName {
            get { return this.GetPublishedProperty("HumanName", ref this.m_HumanName); }
            set { this.SetPublishedProperty("HumanName", ref this.m_HumanName, value); }
        }



        /// <summary>
        /// Gets or sets the connected state of this <see cref="ClassroomModel"/>.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> and the classroom was not previously connected,
        /// a connection will be initiated by the <see cref="ConnectionManager"/>
        /// which created this <see cref="ClassroomModel"/>.
        /// <para>
        /// Similarly, setting to <c>false</c> will cause the <see cref="ConnectionManager"/>
        /// to disconnect the connection.
        /// </para>
        /// <para>
        /// FIXME: It has not yet been determined how connection errors will be communicated to the caller.
        /// </para>
        /// </remarks>
        [Published] public bool Connected {
            get { return this.GetPublishedProperty("Connected", ref this.m_Connected); }
            set { this.SetPublishedProperty("Connected", ref this.m_Connected, value); }
        }


        #region Participants

        /// <summary>
        /// Gets the set of Classroom Presenter clients that are connected to this classroom.
        /// </summary>
        /// <remarks>
        /// The list of participants is populated by a <see cref="ConnectionManager"/>.
        /// </remarks>
        [Published] public ParticipantCollection Participants {
            get { return this.m_Participants; }
        }

        /// <summary>
        /// A published list of <see cref="ParticipantModel"><c>ParticipantModel</c>s</see>.
        /// </summary>
        public class ParticipantCollection : PropertyCollectionBase {
            internal ParticipantCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            /// <summary>
            /// Gets or sets the participant at the given index.
            /// </summary>
            public ParticipantModel this[int index] {
                get { return ((ParticipantModel) List[index]); }
                set { List[index] = value; }
            }

            /// <summary>
            /// Adds a <see cref="ParticipantModel"/> object to the collection.
            /// </summary>
            /// <remarks>
            /// This method should not be used except by a <c>ConnectionManager</c>.
            /// </remarks>
            public int Add(ParticipantModel value) {
                return List.Add(value);
            }

            /// <summary>
            /// Retrieves the location of the specified <see cref="ParticipantModel"/> object in the collection.
            /// </summary>
            public int IndexOf(ParticipantModel value) {
                return List.IndexOf(value);
            }

            /// <summary>
            /// Inserts a <see cref="ParticipantModel"/> object into the collection at the specified index.
            /// </summary>
            public void Insert(int index, ParticipantModel value) {
                List.Insert(index, value);
            }

            /// <summary>
            /// Removes the specified <see cref="ParticipantModel"/> from the collection, if present.
            /// </summary>
            public void Remove(ParticipantModel value) {
                List.Remove(value);
                if (PresenterModel.TheInstance != null) {
                    PresenterModel.TheInstance.VersionExchange.RemoveVersionExchange(value);
                }
            }

            /// <summary>
            /// Determines if the specified <see cref="ParticipantModel"/> is in the collection.
            /// </summary>
            public bool Contains(ParticipantModel value) {
                return List.Contains(value);
            }

            /// <summary>
            /// Throws an <see cref="ArgumentException"/> if the given object is not of type <see cref="ParticipantModel"/>.
            /// </summary>
            protected override void OnValidate(Object value) {
                if(!typeof(ParticipantModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type ParticipantModel.", "value");
            }
        }

        #endregion Participants


        #region Presentations

        /// <summary>
        /// Gets the set of <see cref="PresentationModel">presentations</see>
        /// which are being broadcast by the participants of this <see cref="ClassroomModel"/>.
        /// </summary>
        /// <remarks>
        /// This list should be populated by the <see cref="ConnectionManager"/> which created
        /// this <see cref="ClassroomModel"/>.
        /// <para>
        /// It might not necessarily correspond exactly to
        /// the set of <see cref="InstructorModel.CurrentPresentation"/> properties, as some presentations
        /// published by individual participants might not be supported by the underlying
        /// <see cref="ConnectionManager"/>, for example if client versions differ incompatibly.
        /// </para>
        /// </remarks>
        [Published] public PresentationCollection Presentations {
            get { return this.m_Presentations; }
        }

        /// <summary>
        /// A published list of <see cref="ParticipantModel"><c>ParticipantModel</c>s</see>.
        /// </summary>
        public class PresentationCollection : PropertyCollectionBase {
            internal PresentationCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            /// <summary>
            /// Gets or sets the Presentation at the given index.
            /// </summary>
            public PresentationModel this[int index] {
                get { return ((PresentationModel) List[index]); }
                set { List[index] = value; }
            }

            /// <summary>
            /// Adds a <see cref="PresentationModel"/> object to the collection.
            /// </summary>
            /// <remarks>
            /// This method should not be used except by a <c>ConnectionManager</c>.
            /// </remarks>
            public int Add(PresentationModel value) {
                return List.Add(value);
            }

            /// <summary>
            /// Retrieves the location of the specified <see cref="PresentationModel"/> object in the collection.
            /// </summary>
            public int IndexOf(PresentationModel value) {
                return List.IndexOf(value);
            }

            /// <summary>
            /// Inserts a <see cref="PresentationModel"/> object into the collection at the specified index.
            /// </summary>
            public void Insert(int index, PresentationModel value) {
                List.Insert(index, value);
            }

            /// <summary>
            /// Removes the specified <see cref="PresentationModel"/> from the collection, if present.
            /// </summary>
            public void Remove(PresentationModel value) {
                List.Remove(value);
            }

            /// <summary>
            /// Determines if the specified <see cref="PresentationModel"/> is in the collection.
            /// </summary>
            public bool Contains(PresentationModel value) {
                return List.Contains(value);
            }

            /// <summary>
            /// Throws an <see cref="ArgumentException"/> if the given object is not of type <see cref="PresentationModel"/>.
            /// </summary>
            protected override void OnValidate(Object value) {
                if(!typeof(PresentationModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type PresentationModel.", "value");
            }
        }

        #endregion Presentations

 

    }

    #region ClassroomModelType enum

    public enum ClassroomModelType {
        /// <summary>
        /// TCP classroom, fixed at launch
        /// </summary>
        TCPStatic,
        /// <summary>
        /// RTP classroom, fixed at launch
        /// </summary>
        RTPStatic,
        /// <summary>
        /// Classroom is created and removed in response to instructor advertisements
        /// </summary>
        Dynamic,
        /// <summary>
        /// Special case type, eg. for "Disconnected" classroom.
        /// </summary>
        None,
        /// <summary>
        /// Classroom when running as a CXP Capability
        /// </summary>
        CXPCapability
    }

    #endregion ClassroomModelType enum

}
