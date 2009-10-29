// 
// MoodSeekSlider.cs
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
using Banshee.Widgets;

namespace Banshee.Moodbar
{
    public class MoodbarSeekSlider : SeekSlider
    {
        MoodbarService mood_service;

        public MoodbarSeekSlider () : base()
        {
        }


        protected override bool OnExposeEvent (Gdk.EventExpose evnt)
        {
            if (!Visible || !IsMapped) {
                return true;
            }
                        
            if (ServiceStack.ServiceManager.PlaybackController.CurrentTrack != null && ServiceStack.ServiceManager.PlaybackController.CurrentTrack.Uri.IsLocalPath) {
                int slider_width = (int)this.StyleGetProperty ("slider-length");
                slider_width += 2;
                int slider_height = (int)(Allocation.Height * 0.45);
                var uri = ServiceStack.ServiceManager.PlaybackController.CurrentTrack.Uri;

                mood_service = ServiceStack.ServiceManager.Get<MoodbarService> ();
                var moodbar = mood_service.GetMoodbar (uri);
                
                if(moodbar == null)
                    return base.OnExposeEvent (evnt);
                        
                Cairo.Context cr = Gdk.CairoHelper.Create (evnt.Window);
                foreach (Gdk.Rectangle damage in evnt.Region.GetRectangles ()) {
                    cr.Rectangle (damage.X, damage.Y, damage.Width, damage.Height);
                    cr.Clip ();
                
                    moodbar.Render (cr, (int)(Allocation.Left + slider_width / 2.0), 0, Allocation.Width - slider_width, Allocation.Height);
                    
                    // darker area indicating what's been listened'
                    double handle_position = (Value - this.Adjustment.Lower) * (Allocation.Width - slider_width) / (this.Adjustment.Upper - this.Adjustment.Lower);
                    if (handle_position < 0 || Double.IsNaN (handle_position) || Double.IsInfinity (handle_position)) {
                        handle_position = 0;
                    }
                    cr.SetSourceRGBA (0, 0, 0, 0.4);
                    
                    cr.Rectangle (Allocation.Left + slider_width / 2.0, Allocation.Top, handle_position, Allocation.Height);
                    cr.Fill ();
                    
//                    cr.SetSourceRGBA (0, 0, 0, 1.0);
//                    cr.Rectangle (Allocation.Left + slider_width / 2.0 + handle_position - 1, Allocation.Top, 1, Allocation.Height);
//                    cr.Fill ();
                    
                    cr.ResetClip ();
                    
                    // painting slider
                    Gtk.Style.PaintSlider (Style, GdkWindow, State, Gtk.ShadowType.In, damage, this, "hscale",
                            (int)(Allocation.Left + handle_position),
                            Allocation.Top + (Allocation.Height - slider_height) / 2, slider_width,
                    slider_height, Gtk.Orientation.Horizontal);
                }
                
                ((IDisposable)cr).Dispose ();
                
                return true;
            }
            else
               return base.OnExposeEvent (evnt);
        }
    }
}
