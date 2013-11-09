using System;
using System.Drawing;

namespace UW.ClassroomPresenter.Model.Background
{
    /// <summary>
    /// Interface that defines functions that all primitives implement
    /// </summary>
    public interface IBackgroundPrimitive
    {
        IBackgroundPrimitive Clone();
    }
}
