using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Model.Network {
    /// <summary>
    /// Top level model object for managing exchange of version information between nodes.
    /// </summary>
    public class VersionExchangeModel: PropertyPublisher {
        /// <summary>
        /// Where to point the user for detailed compatibility information
        /// </summary>
        public static string CompatiblityInfoURL = "http://classroompresenter.cs.washington.edu/compatibility/";
        
        /// <summary>
        /// Local Node Version
        /// </summary>
        public static Version LocalVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        
        /// <summary>
        /// Collection of Version Exchanges underway or completed.
        /// </summary>
        private readonly VersionExchangeCollection m_VersionExchanges;
        private readonly Dictionary<Guid, VersionExchange> m_VersionExchangeDictionary;

        /// <summary>
        /// Set this property to cause the UI to present a MessageBox warning to the user.
        /// </summary>
        [Published]
        public String PopUpWarningMessage {
            get { return this.GetPublishedProperty("PopUpWarningMessage", ref this.m_PopUpWarningMessage); }
        }
        private string m_PopUpWarningMessage;
        
        public VersionExchangeModel() {
            m_VersionExchanges = new VersionExchangeCollection(this, "VersionExchanges");
            m_VersionExchangeDictionary = new Dictionary<Guid, VersionExchange>();
        }

        /// <summary>
        /// If the participant is not identical to the local node, does not have a Guid.Empty identifier, and
        /// and if a VersionExchange has not already been created, create one here.  In some cases 
        /// (notably if the local node is an instructor) this will cause the exchange to begin.
        /// </summary>
        /// <param name="participant"></param>
        public void CreateVersionExchange(ParticipantModel participant) {
            if ((participant.Guid.Equals(Guid.Empty)) || 
                (participant.Guid.Equals(PresenterModel.ParticipantId)) ||
                (m_VersionExchangeDictionary.ContainsKey(participant.Guid))) {
                return;
            }
            using (Synchronizer.Lock(this.SyncRoot)) {
                if (!m_VersionExchangeDictionary.ContainsKey(participant.Guid)) {
                    VersionExchange ve = new VersionExchange(participant);
                    m_VersionExchangeDictionary.Add(participant.Guid, ve);
                    //Adding to the collection here triggers an event in the corresponding Network Service
                    m_VersionExchanges.Add(ve);
                }
            }
        }

        /// <summary>
        /// When a participant is removed from a classroom, clean up the version exchange
        /// </summary>
        /// <param name="value"></param>
        internal void RemoveVersionExchange(ParticipantModel participant) {
            if ((participant.Guid.Equals(Guid.Empty)) ||
                (participant.Guid.Equals(PresenterModel.ParticipantId)) ||
                (!m_VersionExchangeDictionary.ContainsKey(participant.Guid))) {
                return;
            }

            using (Synchronizer.Lock(this.SyncRoot)) {
                Trace.WriteLine("Removing VersionExchange for participant: " + participant.Guid.ToString());
                VersionExchange ve = m_VersionExchangeDictionary[participant.Guid];
                m_VersionExchanges.Remove(ve);
                m_VersionExchangeDictionary.Remove(participant.Guid);
            }
        }


        /// <summary>
        /// Called when a VersionRequestMessage is received
        /// </summary>
        /// <param name="remoteParticipant"></param>
        /// <param name="version"></param>
        public void ReceiveVersionRequest(ParticipantModel remoteParticipant, VersionRequestMessage requestMessage) {
            Trace.WriteLine("VersionExchangeModel.ReceiveVersionRequest started. remoteParticipant=" + 
                remoteParticipant.Guid.ToString() + "; requesterID= " + requestMessage.RequesterId.ToString());
            VersionExchange ve;
            //Make a VersionExchange object if needed.
            using (Synchronizer.Lock(this.SyncRoot)) {
                if (m_VersionExchangeDictionary.ContainsKey(remoteParticipant.Guid)) {
                    ve = m_VersionExchangeDictionary[remoteParticipant.Guid];
                }
                else {
                    //This probably should not happen, but if it does we are covered.
                    ve = new VersionExchange(remoteParticipant);
                    m_VersionExchangeDictionary.Add(remoteParticipant.Guid, ve);
                    m_VersionExchanges.Add(ve);
                    Trace.WriteLine("Created a VersionExchange in response to Version Request from participant: " + remoteParticipant.Guid.ToString());
                }
            }

            //Let the VersionExchange object process the inbound message
            ve.ReceiveRequest(requestMessage.RequesterVersion);
        }

        /// <summary>
        /// Called when a VersionResponseMessage is received
        /// </summary>
        /// <param name="participantModel"></param>
        /// <param name="versionResponseMessage"></param>
        internal void ReceiveVersionResponse(ParticipantModel remoteParticipant, VersionResponseMessage versionResponseMessage) {
            Trace.WriteLine("VersionExchangeModel.ReceiveVersionResponse started. remoteParticipant=" + 
                remoteParticipant.Guid.ToString() + ";responderID=" + versionResponseMessage.ResponderId.ToString());

            VersionExchange ve = null;
            //Look up the VersionExchange for this remote participant
            using (Synchronizer.Lock(this.SyncRoot)) { 
                if (m_VersionExchangeDictionary.ContainsKey(versionResponseMessage.ResponderId)) {
                    ve = m_VersionExchangeDictionary[versionResponseMessage.ResponderId];
                }  
            }
            //Let the VersionExchange handle the inbound message.
            if (ve != null) {
                ve.ReceiveResponse(versionResponseMessage);
            }
            else {
                Trace.WriteLine("Warning: Failed to find a pending version exchange to match inbound response message from participant: " + remoteParticipant.Guid.ToString());
            }
        }

        /// <summary>
        /// Present a pop-up warning to the user.
        /// </summary>
        /// <param name="message"></param>
        public void SetPopUpWarning(string message) {
            string msgWithURL = message + "\r\n\r\n" + "For more information, see " + VersionExchangeModel.CompatiblityInfoURL;
            using (Synchronizer.Lock(this.SyncRoot)) {
                this.SetPublishedProperty("PopUpWarningMessage", ref this.m_PopUpWarningMessage, msgWithURL);
            }
        }

        #region VersionExchangeCollection Published Property

        [Published] 
        public VersionExchangeCollection VersionExchanges {
            get { return this.m_VersionExchanges; }
        }

        public class VersionExchangeCollection : PropertyCollectionBase {
            internal VersionExchangeCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            public VersionExchange this[int index] {
                get { return ((VersionExchange) List[index]); }
                set { List[index] = value; }
            }

            public int Add(VersionExchange value) {
                return List.Add(value);
            }

            public int IndexOf(VersionExchange value) {
                return List.IndexOf(value);
            }

            public void Insert(int index, VersionExchange value) {
                List.Insert(index, value);
            }

            public void Remove(VersionExchange value) {
                List.Remove(value);
            }

            public bool Contains(VersionExchange value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if(!typeof(VersionExchange).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type VersionExchange.", "value");
            }
        }

        #endregion VersionExchangeCollection


    }

    /// <summary>
    /// Each VersionExchange object contains the data, logic and state for version exchange with one remote participant.  
    /// It can raise events to cause messages to be sent, or to issue warnings.  
    /// * Both locally initiated and remotely initiated exchanges can occur. 
    /// * Instructors initiate exchanges with VersionRequestMessage when a new ParticipantModel is created.
    /// * The receiver of a request message always replies with VersionResponseMessage
    /// * The receiver of a response message may reply with one additional VersionResponseMessage.  This occurs in the
    ///   case where the receiver is the instructor node that issued the request, and where the receiver has a later
    ///   version.
    /// * Versions of CP that predate the version exchange functionality ignore requests and are idtentified by 
    ///   version exchange-capable nodes as 'unknown'.
    /// The end result is that the properties of both local and remote VersionExchange objects should be populated with the
    /// information from the newer of the two Compatibility Matrices, regardless of whether the newer matrix is local
    /// or remote.
    /// </summary>
    public class VersionExchange : PropertyPublisher {
        private ParticipantModel m_RemoteParticipant;
        private bool m_SendRequest = false;
        private bool m_LocalNodeIsInstructor = false;
        private bool m_RemoteNodeIsInstructor = false;
        private Version m_RemoteVersion = null;
        private string m_RemoteHumanName = "";
        private VersionCompatibility m_RemoteCompatibility = VersionCompatibility.Unknown;
        private VersionIncompatibilityRecommendedAction m_RecommendedAction = VersionIncompatibilityRecommendedAction.NoAction;
        private string m_WarningMessage = "";
        private VersionResponseMessage m_SendVersionResponse;

        #region Public Properties

        public ParticipantModel RemoteParticipant {
            get { return m_RemoteParticipant; }
        }

        public String RemoteHumanName {
            get { return m_RemoteHumanName; }
        }

        /// <summary>
        /// Version of the remote node.  Could be null if we have not received any version messages.
        /// </summary>
        public Version RemoteVersion {
            get { return m_RemoteVersion; }
        }

        /// <summary>
        /// Simple assesment of the compatibility of the local and remote nodes based on the newer of the two
        /// compatibility matrices.  This will be Unknown if we have not received the version messages.
        /// </summary>
        public VersionCompatibility RemoteCompatibility {
            get { return m_RemoteCompatibility; }
        }

        /// <summary>
        /// A message to display to the user about compatiblity between local and remote nodes.  
        /// This will be an empty string if no information has been received, or there is no applicable warning.
        /// </summary>
        public String WarningMessage {
            get { return m_WarningMessage; }
        }

        /// <summary>
        /// Indicates whether the object creation should trigger a VersionRequestMessage send.
        /// </summary>
        public bool SendRequest {
            get { return m_SendRequest;  }
        }

        /// <summary>
        /// Setting this property causes the VersionResponseMessage to be sent.  
        /// Note that the message lacks Tags and Group properties at this point
        /// </summary>
        [Published]
        public VersionResponseMessage SendVersionResponse {
            get { return this.GetPublishedProperty("SendVersionResponse", ref this.m_SendVersionResponse); }
        }

        /// <summary>
        /// Non-instructors should not display version information about other non-instructors since
        /// no exchange happens in this case.  This comes up in RTP scenarios.
        /// </summary>
        public bool DisplayVersionInfo {
            get { return (m_LocalNodeIsInstructor || m_RemoteNodeIsInstructor); }
        }

        #endregion Public Properties

        //Construct
        public VersionExchange(ParticipantModel participant) {
            m_RemoteParticipant = participant;

            //Check local role
            using (Synchronizer.Lock(PresenterModel.TheInstance.Participant.SyncRoot)) {
                if (PresenterModel.TheInstance.Participant.Role is InstructorModel) {
                    this.m_LocalNodeIsInstructor = true;
                }
            }

            //Check and subscribe for changes to remote role
            using (Synchronizer.Lock(m_RemoteParticipant.SyncRoot)) {
                m_RemoteHumanName = m_RemoteParticipant.HumanName;
                if (m_RemoteParticipant.Role is InstructorModel) {
                    m_RemoteNodeIsInstructor = true;
                }
                m_RemoteParticipant.Changed["Role"].Add(new PropertyEventHandler(RoleChangedHandler));
            }


            //Flag instructor nodes to initiate the exchange by sending the request message.
            if (m_LocalNodeIsInstructor) {
                m_SendRequest = true;
            }
        }

        private void RoleChangedHandler(object sender, PropertyEventArgs args) {
            PropertyChangeEventArgs pcea = (PropertyChangeEventArgs)args;
            if (pcea.NewValue is InstructorModel) {
                m_RemoteNodeIsInstructor = true;
            }
            else {
                m_RemoteNodeIsInstructor = false;
            }
        }


        /// <summary>
        /// Handle inbound VersionRequestMessage from this participant.  Always respond.
        /// </summary>
        /// <param name="version"></param>
        internal void ReceiveRequest(Version version) {
            m_RemoteVersion = version;
            VersionResponseMessage vrm = CompatiblityMatrix.GetVersionResponseMessage(m_RemoteVersion);

            //If local is newer or equal, populate compatibility information from the local matrix
            if (m_RemoteVersion <= VersionExchangeModel.LocalVersion) {
                m_RemoteCompatibility = vrm.Compatibility;
                m_RecommendedAction = vrm.Action;
                m_WarningMessage = vrm.LocalWarningMessage;
            }

            //Set the property to cause the Network Service to send the response.
            using (Synchronizer.Lock(this.SyncRoot)) {
                this.SetPublishedProperty("SendVersionResponse", ref this.m_SendVersionResponse, vrm);
            }
        }

 
        /// <summary>
        /// Handle inbound VersionResponseMessage.  
        /// </summary>
        /// <param name="responseMessage"></param>
        public void ReceiveResponse(VersionResponseMessage responseMessage) {
            Trace.WriteLine("VersionResponseMessage received from " + m_RemoteParticipant.Guid.ToString());
            m_RemoteVersion = responseMessage.ResponderVersion;

            if (m_RemoteVersion > VersionExchangeModel.LocalVersion) {
                m_RemoteCompatibility = responseMessage.Compatibility;
                m_RecommendedAction = responseMessage.Action;
                m_WarningMessage = responseMessage.WarningMessage;
                if (!this.m_LocalNodeIsInstructor) {
                    if ((m_RecommendedAction == VersionIncompatibilityRecommendedAction.IssueWarning) &&
                        (m_WarningMessage != null) && (m_WarningMessage != "")) {
                        //Cause a local pop-up warning to be displayed.
                        PresenterModel.TheInstance.VersionExchange.SetPopUpWarning(m_WarningMessage);
                    }
                }
                else {
                    //Here we might want to show warning, but aggregate across all participants and show one messagebox.
                }
            }
            else if (m_RemoteVersion < VersionExchangeModel.LocalVersion) {
                //This node is newer than the remote node, so use the local compatibility matrix
                // to generate compatibility information.
                VersionResponseMessage vrm = CompatiblityMatrix.GetVersionResponseMessage(m_RemoteVersion);
                m_RemoteCompatibility = vrm.Compatibility;
                m_RecommendedAction = vrm.Action;
                m_WarningMessage = vrm.LocalWarningMessage;

                //If the local node is an instructor and if the local node has a later version, we send one more
                // response back to the client since our verison of the compatibility matrix may have information
                // that the client's matrix doesn't have.
                if (this.m_LocalNodeIsInstructor) {
                    using (Synchronizer.Lock(this.SyncRoot)) {
                        this.SetPublishedProperty("SendVersionResponse", ref this.m_SendVersionResponse, vrm);
                    }
                }

                //Are there any cases where we want to raise events?  If so, the code can be added later as needed.
            }
            else { 
                //Versions are equal.  We fully expect compatiblity in this case.
                m_RemoteCompatibility = responseMessage.Compatibility;
                m_RecommendedAction = responseMessage.Action;
                m_WarningMessage = responseMessage.WarningMessage;
            }
        }

        /// <summary>
        /// This class check if the remote version is older than local version.
        /// Only used for student role.
        /// </summary>
        public bool IsRemoteVersionOlder() {
            if (!this.m_LocalNodeIsInstructor) {
                try {
                    return (this.m_RemoteVersion < VersionExchangeModel.LocalVersion) ? true : false;
                }
                catch (ArgumentNullException e) {
                    Trace.WriteLine("Remote version unknown:" + e.ToString());
                    return true;
                }
            }
            else throw new NotSupportedException("Not support for instructor role.");
        }
    }

    /// <summary>
    /// This class encapsulates the set of code that is expected to need to be changed every time there is a new version of CP
    /// that introduces new compatibility issues.
    /// </summary>
    public static class CompatiblityMatrix {
        /// <summary>
        /// Generate the outbound VersionResponseMessage
        /// </summary>
        /// <param name="localVersion"></param>
        /// <param name="remoteVersion"></param>
        /// <returns></returns>
        public static VersionResponseMessage GetVersionResponseMessage(Version remoteVersion) {
            VersionResponseMessage vrm = new VersionResponseMessage();

            ///If the remote version is newer, we always send the default message which claims full compatibility.
            ///If this is not the case, the remote node's compatibility matrix will be used to override the properties.
            ///If the remote node's version is older, the information we provide here will be meaningful.
           
            if (remoteVersion < VersionExchangeModel.LocalVersion) {
                /// Note: When new CP3 versions are created, add code here to populate the 
                /// VersionResponseMessage properties with compatibility warnings and actions as appropriate.
                /// Note that the pop-up and dialog always show the compatibility info URL, so do not include it
                /// in the warning message.
                 
                ///This is only displayed in the Version Compatibility Info dialog:
                //vrm.Compatibility = VersionCompatibility.Partial;

                ///IssueWarning means that a pop-up appears on student or public nodes with earlier version when connection is made.
                //vrm.Action = VersionIncompatibilityRecommendedAction.IssueWarning;
                
                ///The WarningMessage is displayed on the remote node with earlier version.  It appears in the pop-up if
                /// the remote node is public or student, and it appears in the Compatiblity Info dialog available from the 
                /// menu for all roles.
                //vrm.WarningMessage = "The local Classroom Presenter version has known compatibility problems affecting feature X when connected to this remote node. Upgrade of the local Classroom Presenter is recommended.";
                
                ///The LocalWarningMessage is not part of the serialized message. It is only displayed locally in the 
                /// Compatibility Info dialog on the node with the later version.
                //vrm.LocalWarningMessage = "The remote node has known problems with feature X. Upgrade of the remote node is recommended.";

                #region QuickPoll
                Version qpVersion = new Version(3, 0, 1654);
                if (remoteVersion < qpVersion) {
                    vrm.Compatibility = VersionCompatibility.Partial;
                    vrm.Action = VersionIncompatibilityRecommendedAction.NoAction;
                    vrm.WarningMessage = Strings.QuickPollVersionWarning;
                    vrm.LocalWarningMessage = Strings.QuickPollLocalVersionWarning;
                }
                #endregion QuickPoll
            }

            return vrm;
        }

        
    }    
    
}
