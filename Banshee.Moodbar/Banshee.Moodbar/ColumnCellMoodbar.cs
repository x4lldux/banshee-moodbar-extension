// 
// ColumnCellMood.cs
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
using Hyena.Data.Gui;

namespace Banshee.Moodbar
{

    public class ColumnCellMoodbar : ColumnCell
    {
        public ColumnCellMoodbar (string property, bool expand) : base(property, expand)
        {
        
        }

        public override void Render (CellContext context, Gtk.StateType state, double cellWidth, double cellHeight)
        {
            Base.SafeUri uri = BoundObject as Base.SafeUri;
            if (uri == null || !uri.IsLocalPath)
                return;
            
            var mood_service = ServiceStack.ServiceManager.Get<MoodbarService> ();
            var moodbar = mood_service.GetMoodbarQuick (uri.LocalPath);
            if (moodbar == null)
                return;
            
            Cairo.Context cr = context.Context;
            cr.Rectangle (0, 1, cellWidth, cellHeight-2);
            cr.Clip ();
            moodbar.Render (cr, 0, 0, ((int)cellWidth), ((int)cellHeight));
            cr.ResetClip ();
        }
    }
}
