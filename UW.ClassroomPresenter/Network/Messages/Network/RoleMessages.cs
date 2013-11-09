// $Id: RoleMessages.cs 1999 2009-08-07 00:35:56Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Misc;

namespace UW.ClassroomPresenter.Network.Messages.Network {

    [Serializable]
    public abstract class RoleMessage : Message {
        protected RoleMessage(RoleModel role) : base(role.Id) {
            this.Target = role;
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            using(Synchronizer.Lock(context.Participant.SyncRoot)) {
                context.Participant.Role = this.Target as RoleModel;
            }

            return false;
        }

        public static RoleMessage ForRole(RoleModel role) {
            if(role is InstructorModel) {
                return new InstructorMessage((InstructorModel) role);
            } else if(role is StudentModel) {
                return new StudentMessage((StudentModel) role);
            } else if(role is PublicModel) {
                return new PublicMessage((PublicModel) role);
            } else {
                return null;
            }
        }

        #region IGenericSerializable
        public RoleMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.RoleMessageId;
        }
        #endregion
    }

    [Serializable]
    public class InstructorMessage : RoleMessage {
        public readonly bool AcceptingStudentSubmissions;
        public readonly bool ForcingStudentNavigationLock;
        public readonly Int64 InstructorClockTicks;
        [NonSerialized]
        public bool AcceptingQuickPollSubmissions = false;
        public LinkedDeckTraversalModel.NavigationSelector StudentNavigationType = LinkedDeckTraversalModel.NavigationSelector.Full;

        public InstructorMessage(InstructorModel role) : base(role) {
            using(Synchronizer.Lock(role.SyncRoot)) {
                this.AcceptingStudentSubmissions = role.AcceptingStudentSubmissions;
                // Need to add as an extension to maintain backward compatability
                this.Extension = new ExtensionWrapper( role.AcceptingQuickPollSubmissions, new Guid("{65A946F4-D1C5-426b-96DB-7AF4863CE296}") );
                ((ExtensionWrapper)this.Extension).AddExtension(new ExtensionWrapper(role.AcceptingQuickPollSubmissions, new Guid("{65A946F4-D1C5-426b-96DB-7AF4863CE296}")));
                ((ExtensionWrapper)this.Extension).AddExtension(new ExtensionWrapper(role.StudentNavigationType, new Guid("{4795707C-9F85-CA1F-FEBA-5E851CF9E1DE}")));
                this.AcceptingQuickPollSubmissions = role.AcceptingQuickPollSubmissions;
                this.ForcingStudentNavigationLock = role.ForcingStudentNavigationLock;
                this.InstructorClockTicks = UW.ClassroomPresenter.Misc.AccurateTiming.Now;
                this.StudentNavigationType = role.StudentNavigationType;
            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            bool naviChanged = false;
            InstructorModel role = this.Target as InstructorModel;
            if(role == null)
                this.Target = role = new InstructorModel(((Guid) this.TargetId));

            using(Synchronizer.Lock(role.SyncRoot)) {
                role.AcceptingStudentSubmissions = this.AcceptingStudentSubmissions;
                // Deserialize the extension
                ExtensionWrapper extension = this.Extension as ExtensionWrapper;
                if (extension != null) {
                    try {
                        if (extension.ContainsKey(new Guid("{65A946F4-D1C5-426b-96DB-7AF4863CE296}")) || extension.ContainsKey(new Guid("{4795707C-9F85-CA1F-FEBA-5E851CF9E1DE}"))) {
                            bool acceptingQuickPollSubmissions = (bool)extension.GetExtension(new Guid("{65A946F4-D1C5-426b-96DB-7AF4863CE296}")).ExtensionObject;
                            this.AcceptingQuickPollSubmissions = acceptingQuickPollSubmissions;
                            LinkedDeckTraversalModel.NavigationSelector studentNavigationType = (LinkedDeckTraversalModel.NavigationSelector)extension.GetExtension(new Guid("{4795707C-9F85-CA1F-FEBA-5E851CF9E1DE}")).ExtensionObject;
                            this.StudentNavigationType = studentNavigationType;
                            using (Synchronizer.Lock(context.Model.ViewerState)) {
                                context.Model.ViewerState.StudentNavigationType = studentNavigationType;
                            }
                        }
                        else {
                            Trace.WriteLine("Unknown Extension id=" + extension.ExtensionType.ToString());
                        }
                    }
                    catch (Exception) {
                        if (extension.ExtensionType.Equals(new Guid("{65A946F4-D1C5-426b-96DB-7AF4863CE296}"))) {
                            bool acceptingQuickPollSubmissions = (bool)extension.ExtensionObject;
                                this.AcceptingQuickPollSubmissions = acceptingQuickPollSubmissions;
                         }
                         else {
                             Trace.WriteLine("Unknown Extension id=" + extension.ExtensionType.ToString());
                         }
                    }
                }                
                else {
                    Trace.WriteLine("Unknown Extension id=" + this.Extension.GetType().ToString());
                }
                role.AcceptingQuickPollSubmissions = this.AcceptingQuickPollSubmissions;
                role.ForcingStudentNavigationLock = this.ForcingStudentNavigationLock;
                if (this.StudentNavigationType == LinkedDeckTraversalModel.NavigationSelector.None)
                    role.ForcingStudentNavigationLock = true;
                if (role.StudentNavigationType != this.StudentNavigationType)
                    naviChanged = true;
                role.StudentNavigationType = this.StudentNavigationType;
            }

            //if StudentNavigationType changed, refresh FilmStrip region 
            if (naviChanged) {
                using (Synchronizer.Lock(context.Model.ViewerState.SyncRoot)) {
                    bool oldShowFilmStrip = context.Model.ViewerState.FilmStripEnabled;
                    context.Model.ViewerState.FilmStripEnabled = false;
                    context.Model.ViewerState.FilmStripEnabled = oldShowFilmStrip;
                }
            }

            // Update the clock skew from our current value.
            using( Synchronizer.Lock( context.Model.ViewerState.Diagnostic.SyncRoot ) ) {
                context.Model.ViewerState.Diagnostic.AddSkewEntry( this.InstructorClockTicks );
            }

            base.UpdateTarget(context);

            return true;
        }

        #region IGenericSerializable
        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeBool( this.AcceptingStudentSubmissions ) );
            p.Add( SerializedPacket.SerializeBool( this.ForcingStudentNavigationLock ) );
            p.Add( SerializedPacket.SerializeBool( this.AcceptingQuickPollSubmissions ) );
            p.Add( SerializedPacket.SerializeInt64( this.InstructorClockTicks ) );
            return p;
        }

        public InstructorMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.AcceptingStudentSubmissions = SerializedPacket.DeserializeBool( p.GetNextPart() );
            this.ForcingStudentNavigationLock = SerializedPacket.DeserializeBool( p.GetNextPart() );
            this.AcceptingQuickPollSubmissions = SerializedPacket.DeserializeBool( p.GetNextPart() );
            this.InstructorClockTicks = SerializedPacket.DeserializeInt64( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.InstructorMessageId;
        }
        #endregion

    }

    [Serializable]
    public class StudentMessage : RoleMessage {
        public StudentMessage(StudentModel role) : base(role) {
            // Currently StudentModel has no published properties.
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            StudentModel role = this.Target as StudentModel;
            if(role == null)
                this.Target = role = new StudentModel(((Guid) this.TargetId));

            base.UpdateTarget(context);

            return true;
        }

        #region IGenericSerializable
        public StudentMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.StudentMessageId;
        }
        #endregion
    }

    [Serializable]
    public class PublicMessage : RoleMessage {
        public PublicMessage(PublicModel role) : base(role) {
            // Currently PublicModel has no published properties.
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            PublicModel role = this.Target as PublicModel;
            if(role == null)
                this.Target = role = new PublicModel(((Guid) this.TargetId));

            base.UpdateTarget(context);

            return true;
        }

        #region IGenericSerializable
        public PublicMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.PublicMessageId;
        }
        #endregion

    }

    [Serializable]
    public class InstructorCurrentPresentationChangedMessage : PresentationMessage {
        public InstructorCurrentPresentationChangedMessage(PresentationModel presentation) : base(presentation) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            bool update = base.UpdateTarget(context);

            InstructorModel instructor = this.Parent != null ? this.Parent.Target as InstructorModel : null;
            if(instructor != null) {
                using(Synchronizer.Lock(instructor.SyncRoot)) {
                    instructor.CurrentPresentation = this.Target as PresentationModel;
                }
            }

            return update;
        }

        #region IGenericSerializable
        public InstructorCurrentPresentationChangedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.InstructorCurrentPresentationChangedMessageId;
        }
        #endregion
    }

    [Serializable]
    public class InstructorCurrentDeckTraversalChangedMessage : DeckTraversalMessage {
        public InstructorCurrentDeckTraversalChangedMessage(DeckTraversalModel traversal, bool addLocalRef) : base(traversal, addLocalRef) { }
        public InstructorCurrentDeckTraversalChangedMessage(DeckTraversalModel traversal) : this(traversal,true) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            bool update = base.UpdateTarget(context);

            InstructorModel instructor = this.Parent != null ? this.Parent.Target as InstructorModel : null;
            if(instructor != null) {
                using(Synchronizer.Lock(instructor.SyncRoot)) {
                    instructor.CurrentDeckTraversal = this.Target as DeckTraversalModel;
                }
            }

            return update;
        }

        #region IGenericSerializable
        public InstructorCurrentDeckTraversalChangedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.InstructorCurrentDeckTraversalChangedMessageId;
        }
        #endregion
    }
}
