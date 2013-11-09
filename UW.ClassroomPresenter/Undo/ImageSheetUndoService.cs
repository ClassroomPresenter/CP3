// $Id: ImageSheetUndoService.cs 737 2005-09-09 00:07:19Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {
    public class ImageSheetUndoService : SheetUndoService {
        public ImageSheetUndoService(EventQueue dispatcher, UndoModel undo, DeckModel deck, SlideModel slide, ImageSheetModel sheet) : base(undo, deck, slide, sheet) {
            // There are currently no published properties of ImageSheetModel, other than those handled by the base class.
        }
    }
}
