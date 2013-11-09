// $Id: SheetDisposition.cs 1051 2006-07-21 17:42:48Z julenka $

using System;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Flags]
    public enum SheetDisposition : long {
        Background = 1,
        Instructor = 2,
        Student = 4,
        Public = 8,
        All = 16,
        Remote = 32,
        SecondMonitor = 64,
    }
}
