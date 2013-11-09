// $Id: SlideDisposition.cs 1026 2006-07-14 03:05:11Z pediddle $

using System;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Flags]
    public enum SlideDisposition : long {
        Empty = 0,
        Remote = 1,
        StudentSubmission = 2,
    }
}
