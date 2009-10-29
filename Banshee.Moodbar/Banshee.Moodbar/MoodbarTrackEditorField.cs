// 
// MoodbarEntry.cs
//  
// Author:
//       Paweł "X4lldux" Drygas <x4lldux@jabster.pl>
// 
// Copyright (c) 2009 Paweł "X4lldux" Drygas
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Gtk;

using Hyena;
using Hyena.Gui;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Collection.Database;

using Banshee.Gui;
using Banshee.Gui.TrackEditor;
using Banshee.I18n;

namespace Banshee.Moodbar
{

    public class MoodbarTrackEditorField : VBox
    {
        Button detect_button;
        DrawingArea drawing_area;
        Alignment drawing_area_alignment;
        SafeUri uri;
        MoodbarService mood_service;
        Moodbar moodbar = null;
        
        public MoodbarTrackEditorField ()
        {
            mood_service = ServiceManager.Get<MoodbarService> ();
            
            BuildWidgets ();
        }
        
        private void BuildWidgets ()
        {
            drawing_area = new DrawingArea ();
            drawing_area.ExposeEvent += HandleDrawingAreaExposeEvent;
            drawing_area.Show ();

            drawing_area_alignment = new Alignment (0, 0, 1, 1);
            drawing_area_alignment.SetSizeRequest (-1, 32);
            drawing_area_alignment.Add (drawing_area);
            PackStart (drawing_area_alignment);
            
            detect_button = new Button (Catalog.GetString ("D_etect"));
            detect_button.Clicked += HandleDetectButtonClicked;
            detect_button.Show ();
            PackEnd (detect_button);
            
            ShowAll ();
        }

        void HandleDetectButtonClicked (object sender, EventArgs e)
        {
            detect_button.Sensitive = false;
            var uri_when_clicked = uri;
            
            mood_service.DetectMood (uri_when_clicked, delegate(Moodbar detected_moodbar) {
                Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                    moodbar = detected_moodbar;
                    if (moodbar != null && uri == uri_when_clicked) {
                        detect_button.Hide ();
                        drawing_area_alignment.Show ();
                    } else {
                        detect_button.Sensitive = true;
                    }
                });
            });
        }
        
        void HandleDrawingAreaExposeEvent (object o, ExposeEventArgs args)
        {
            if (moodbar == null)
                return;
            
            Cairo.Context cr = Gdk.CairoHelper.Create (args.Event.Window);
            foreach (Gdk.Rectangle damage in args.Event.Region.GetRectangles ()) {
                cr.Rectangle (damage.X, damage.Y, damage.Width, damage.Height);
                cr.Clip ();
                
                moodbar.Render (cr, 0, 0, Allocation.Width, Allocation.Height);
                
                cr.ResetClip ();
            }
            
            ((IDisposable)cr.Target).Dispose ();
            ((IDisposable)cr).Dispose ();
            
            args.RetVal = true;

        }
        
        public SafeUri Uri {
            get { return this.uri; }
            set {
                if (value == null) {
                    moodbar = null;
                    this.uri = null;
                    return;
                }
                
                if (value.IsLocalPath && (uri == null || value.LocalPath != uri.LocalPath)) {
                    this.uri = value;
                    moodbar = mood_service.GetMoodbar (uri.LocalPath);
                    
                    if (moodbar == null) {
                        detect_button.Sensitive = true;
                        detect_button.Show ();
                        drawing_area_alignment.Hide ();
                    } else {
                        detect_button.Hide ();
                        drawing_area_alignment.Show ();
                        drawing_area.QueueDraw ();
                    }
                }
            }
        }
    }
}
