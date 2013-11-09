using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Network.Groups {
    /// <summary>
    /// A token representing a group of participants (usually with a specific set of 
    /// capabilities) that you want to communicate with. This allows us to specifically send
    /// network messages to a group of participants. Listeners should filter out messages 
    /// not meant for themselves and not re-request these messages.
    /// </summary>
    [Serializable]
    public class Group : IGenericSerializable {
        #region Static Members

        /// <summary>
        /// Represents the group of all participants, everyone should always be a part of this
        /// group
        /// Value: {9AD19089-6A58-41c7-ACD8-5096A7C2ACDF}
        /// </summary>
        public static readonly Guid AllParticipantGuid = new Guid( "{9AD19089-6A58-41c7-ACD8-5096A7C2ACDF}" );
        public static readonly Group AllParticipant = new Group( Group.AllParticipantGuid, "All Participants" );

        /// <summary>
        /// Represents the group of all instructor participants
        /// Value: {EC16EB3F-320C-4f25-A263-8029B9905358}
        /// </summary>
        public static readonly Guid AllInstructorGuid = new Guid( "{EC16EB3F-320C-4f25-A263-8029B9905358}" );
        public static readonly Group AllInstructor = new Group( Group.AllInstructorGuid, "All Instructors" );

        /// <summary>
        /// Represents the group of all public display participants
        /// Value: {CA68CCA8-1854-4ba5-8D7B-1F815910E620}
        /// </summary>
        public static readonly Guid AllPublicGuid = new Guid( "{CA68CCA8-1854-4ba5-8D7B-1F815910E620}" );
        public static readonly Group AllPublic = new Group( Group.AllPublicGuid, "All Public Displays" );

        /// <summary>
        /// Represents the group of all student participants
        /// Value: {4725E70C-7BB2-481f-B9B6-AA1C1A723302}
        /// </summary>
        public static readonly Guid AllStudentGuid = new Guid( "{4725E70C-7BB2-481f-B9B6-AA1C1A723302}" );
        public static readonly Group AllStudent = new Group( Group.AllStudentGuid, "All Students" );

        /// <summary>
        /// Represents the group of all participants the receive student submissions.
        /// The instructors and public displays should both be part of this group.
        /// Value: {740B2A17-F1FE-49ee-8E46-BB0B3A4D5D02}
        /// </summary>
        public static readonly Guid SubmissionsGuid = new Guid( "{740B2A17-F1FE-49ee-8E46-BB0B3A4D5D02}" );
        public static readonly Group Submissions = new Group( Group.SubmissionsGuid, "All Receivers of Student Submissions" );

        /// <summary>
        /// Represents the group of participants that can accept any image format
        /// group
        /// Value: {9AD19089-6A58-41c7-ACD8-5096A7C2ACDF}
        /// </summary>
        public static readonly Guid AllFormatsGuid = new Guid( "{C33561DC-987B-40b3-8A88-4D155F1E0B2F}" );
        public static readonly Group AllFormats = new Group( Group.AllFormatsGuid, "All Formats" );

        #endregion

        #region Public Members

        /// <summary>
        /// The guid presenting this group
        /// </summary>
        private Guid m_id;
        public Guid ID {
            get { return m_id; }
        }

        /// <summary>
        /// A friendly name for this 
        /// </summary>
        private string m_friendlyName;
        public String FriendlyName {
            get { return m_friendlyName; }
        }

        #endregion

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">The globally unique id of the group</param>
        /// <param name="name">The friendly name to give this group</param>
        public Group( Guid id, string name ) {
            this.m_id = id;
            this.m_friendlyName = name;
        }

        /// <summary>
        /// Construct a group from a set of image capabilities for your device
        /// </summary>
        /// <param name="width">The width of the device</param>
        /// <param name="height">The height of the device</param>
        /// <param name="depth">The depth of the device</param>
        /// <param name="fmt">The format of the device</param>
        public Group( int width, int height, int depth, System.Drawing.Imaging.ImageFormat fmt ) {
            byte[] data = new byte[16];
            data[0] = 0x0A;
            data[1] = 0x01;
            BitConverter.GetBytes( (short)depth.GetHashCode() ).CopyTo( data, 2 );
            BitConverter.GetBytes( width.GetHashCode() ).CopyTo( data, 4 );
            BitConverter.GetBytes( height.GetHashCode() ).CopyTo( data, 8 );
            BitConverter.GetBytes( fmt.GetHashCode() ).CopyTo( data, 12 );
            this.m_id = new Guid( data );
            this.m_friendlyName = fmt.ToString() + 
                " W:" + width +
                " H:" + height +
                " D:" + depth;
        }

        #endregion

        #region Comparison

        /// <summary>
        /// Determines if two different groups are equal
        /// </summary>
        /// <param name="obj">The group object to compare to</param>
        /// <returns>True if the objects are the same group, false otherwise</returns>
        public override bool Equals( object obj ) {
            if( (obj is Group) && ((Group)obj).ID == this.ID )
                return true;
            return false;
        }

        /// <summary>
        /// Get a hashcode for this group
        /// </summary>
        /// <returns>Returns the hashcode of the group ID</returns>
        public override int GetHashCode() {
            return this.ID.GetHashCode();
        }

        #endregion

        #region IGenericSerializable
        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( SerializedPacket.SerializeGuid( this.m_id ) );
            p.Add( SerializedPacket.SerializeString( this.m_friendlyName ) );
            return p;
        }

        public Group( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.m_id = SerializedPacket.DeserializeGuid( p.GetPart( 0 ) );
            this.m_friendlyName = SerializedPacket.DeserializeString( p.GetPart( 1 ) );
        }

        public int GetClassId() {
            return PacketTypes.GroupId;
        }

        #endregion

    }
}