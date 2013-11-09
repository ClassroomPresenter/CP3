// $Id: WorkspaceModel.cs 975 2006-06-28 01:02:59Z pediddle $

using System;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Model.Workspace {
    public class WorkspaceModel : SyncRoot {
        public WorkspaceModel() {
            this.CurrentPresentation = new Property<PresentationModel>(this);
            this.CurrentDeckTraversal = new Property<DeckTraversalModel>(this);
            this.CurrentSlidePreviewDeckTraversal = new Property<DeckTraversalModel>(this);

            this.DeckTraversals = new Collection<DeckTraversalModel>(this);
            this.DeckMatches = new Collection<DeckPairModel>(this);
        }

        public readonly Property<PresentationModel> CurrentPresentation;
        public readonly Property<DeckTraversalModel> CurrentDeckTraversal;
        public readonly Property<DeckTraversalModel> CurrentSlidePreviewDeckTraversal;

        public readonly Collection<DeckTraversalModel> DeckTraversals;
        public readonly Collection<DeckPairModel> DeckMatches;
    }
}
