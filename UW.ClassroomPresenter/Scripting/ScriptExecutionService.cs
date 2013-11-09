using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Scripting {
    public class ScriptExecutionService {
        protected PresenterModel m_Model;
        protected Form m_UI;

        public ScriptExecutionService( PresenterModel model, Form ui ) {
            this.m_Model = model;
            this.m_UI = ui;
        }

        public void ExecuteScriptThreadEntry( object filename ) {
            if( filename is string ) {
                this.ExecuteScript( (string)filename );
            } else if( filename is Script ) {
                this.ExecuteScript( (Script)filename );
            }
        }

        public void ExecuteScript( string filename ) {
            Script s = new Script( filename );
            this.ExecuteScript( s );
        }

        public void ExecuteScript( Script s ) {
            foreach( IScriptEvent e in s.Events ) {
                e.Execute( this );
            }
        }

        public System.Drawing.Drawing2D.Matrix GetInkTransform() {
            foreach( Control c in this.m_UI.Controls ) {
                if( c is Viewer.ViewerPresentationLayout ) {
                    foreach( Control d in c.Controls ) {
                        if( d is UW.ClassroomPresenter.Viewer.Slides.MainSlideViewer ) {
                            using( Synchronizer.Lock( ((Viewer.Slides.MainSlideViewer)d).SlideDisplay.SyncRoot ) )
                                return ((Viewer.Slides.MainSlideViewer)d).SlideDisplay.InkTransform;
                        }
                    }
                }
            }
            return null;
        }

        public Control GetControl( string[] controlTree ) {
            Control result = this.m_UI;
            foreach( string s in controlTree ) {
                if( result.Controls.ContainsKey(s) ) {
                    result = result.Controls[s];
                } else
                    return null;
            }
            return result;
        }

        public Menu GetMenuItem( string[] menuTree ) {
            Menu result = this.m_UI.Menu;
            foreach( string s in menuTree ) {
                if( result.MenuItems.ContainsKey( s ) ) {
                    result = result.MenuItems[s];
                } else
                    return null;
            }
            return result;
        }

    }
}
