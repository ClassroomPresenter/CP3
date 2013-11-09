// $Id: DeckDisposition.cs 1776 2008-09-24 00:41:05Z anderson $

using System;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Flags]
    public enum DeckDisposition : long {
        Empty = 0,
        Remote = 1,
        Whiteboard = 2,
        StudentSubmission = 4,
        QuickPoll = 8,
        PublicSubmission = 16,
    }
}
