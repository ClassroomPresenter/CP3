// $Id: PresenterModel.cs 1577 2008-03-28 01:16:28Z cmprince $

using System;

using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Model.Undo;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Workspace;

namespace UW.ClassroomPresenter.Model {
    public class PresenterModel : PropertyPublisher {
        private readonly NetworkModel m_Network;
        private readonly ParticipantModel m_Participant;
        private readonly WorkspaceModel m_Workspace;
        private readonly UndoModel m_Undo;
        private readonly ViewerStateModel m_ViewerState;
        private readonly VersionExchangeModel m_VersionExchange;

        //The ID of the local participant.
        public static Guid ParticipantId = Guid.Empty;

        public static PresenterModel TheInstance = null;

        // Published properties:
        private StylusModel m_Stylus;
        private QuickPollResultModel m_CurrentResult;
        private PenStateModel m_PenState;

        public PresenterModel() {
            this.m_Stylus = null;
            this.m_CurrentResult = null;
            this.m_Network = new NetworkModel();
            this.m_VersionExchange = new VersionExchangeModel();
            /// Note: We currently assume that the ParticipantModel Guid will be different for each application invocation.  
            /// (In particular TCP reconnection relies on this assumption.)  If we need an identifer that persists across 
            /// sessions, we'd need to create a new identifier for this.
            ParticipantId = Guid.NewGuid();
            this.m_Participant = new ParticipantModel(ParticipantId, System.Windows.Forms.SystemInformation.UserName);
            this.m_Workspace = new WorkspaceModel();
            this.m_Undo = new UndoModel();
            this.m_ViewerState = new ViewerStateModel();
            this.m_PenState = new PenStateModel();
            TheInstance = this;
        }

        [Published] public StylusModel Stylus {
            get { return this.GetPublishedProperty("Stylus", ref this.m_Stylus); }
            set { this.SetPublishedProperty("Stylus", ref this.m_Stylus, value); }
        }

        /// <summary>
        /// Reference to the current student QuickPollResultModel, this is what 
        /// the UI listens to to stay updated 
        /// 
        /// NOTE: This is unused on the instructor side
        /// </summary>
        [Published] public QuickPollResultModel CurrentStudentQuickPollResult {
            get { return this.GetPublishedProperty( "CurrentStudentQuickPollResult", ref this.m_CurrentResult ); }
            set { this.SetPublishedProperty( "CurrentStudentQuickPollResult", ref this.m_CurrentResult, value ); }
        }

        public NetworkModel Network {
            get { return this.m_Network; }
        }

        public ParticipantModel Participant {
            get { return this.m_Participant; }
        }

        [Published] public WorkspaceModel Workspace {
            get { return this.m_Workspace; }
        }

        public UndoModel Undo {
            get { return this.m_Undo; }
        }

        public ViewerStateModel ViewerState {
            get { return this.m_ViewerState; }
        }

        public PenStateModel PenState {
            get { return this.m_PenState; }
        }

        public VersionExchangeModel VersionExchange {
            get { return this.m_VersionExchange; }
        }
    }
}
