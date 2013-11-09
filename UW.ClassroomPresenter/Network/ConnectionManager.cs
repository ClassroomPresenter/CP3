// $Id: ConnectionManager.cs 877 2006-02-07 23:24:48Z pediddle $

using System;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network {
    /// <summary>
    /// A <c>ConnectionManager</c> initializes the back-end behind a <see cref="ProtocolModel"/>.
    /// <para>
    /// The <c>ConnectionManager</c> is responsible for discovering or populating the protocol's
    /// <see cref="ProtocolModel.Classrooms"><c>Classrooms</c> collection</see>.
    /// </para>
    /// <para>
    /// When a <see cref="ClassroomModel"/>'s <see cref="ClassroomModel.Connected">Connected</see>
    /// property is affirmed, the <c>ConnectionManager</c> initializes the classroom's network connection.
    /// The <c>ConnectionManager</c> proceeds to populate the classroom's
    /// <see cref="ClassroomModel.Presentations"><c>Presentations</c> collection</see>.
    /// </para>
    /// <para>
    /// Finally, the <c>ConnectionManager</c> listens to the <see cref="PresenterModel.Presentation"/> property.
    /// When the user joins or starts a presentation (ie., when the <c>Presentation</c> property is set to a non-null value),
    /// and if the presentation belongs to a classroom which belongs to this <c>ConnectionManager</c>,
    /// the <c>ConnectionManager</c> registers event listeners on all pertinent properties of the presentation's model hierarchy
    /// and broadcasts changes to slides, navigation, etc. to interested participants of the classroom.
    /// </para>
    /// </summary>
    public abstract class ConnectionManager : IDisposable {
        protected ConnectionManager() {
        }

        /// <summary>
        /// Gets the <see cref="ProtocolModel"/> that is supported by this <see cref="ConnectionManager"/>.
        /// </summary>
        public abstract ProtocolModel Protocol { get; }

        ~ConnectionManager() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }
}
