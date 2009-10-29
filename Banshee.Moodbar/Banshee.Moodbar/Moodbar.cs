// 
// Moodbar.cs
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
using System.IO;
using Cairo;

namespace Banshee.Moodbar
{
    public class Moodbar : IDisposable
    {
        static string mood_files_storage = System.IO.Path.Combine(Banshee.Base.Paths.ApplicationData, "moods");
        
        byte[,] moodbar_data;
        Cairo.ImageSurface mood_surface;
        string mood_file_path;

        private Moodbar (string mood_file_path)
        {
            this.mood_file_path = mood_file_path;
//            mood_file_path = GetMoodFilePath(audio_file_path);
            var file = new FileStream (mood_file_path, FileMode.Open);
            moodbar_data = new byte[file.Length / 3, 3];
            
            for (int i = 0; i < file.Length; i += 3) {
                moodbar_data[i / 3, 0] = (byte)file.ReadByte ();
                moodbar_data[i / 3, 1] = (byte)file.ReadByte ();
                moodbar_data[i / 3, 2] = (byte)file.ReadByte ();
            }
            file.Close ();
            
            mood_surface = new Cairo.ImageSurface (Cairo.Format.RGB24, 1000, 1);
            using (Cairo.Context cr = new Cairo.Context (mood_surface)) {
                for (int i = 0; i < moodbar_data.GetLength (0); i++) {
                    cr.SetSourceRGBA (moodbar_data[i, 0] / 255.0, moodbar_data[i, 1] / 255.0, moodbar_data[i, 2] / 255.0, 1);
                    cr.Rectangle (i, 0, 1, 1);
                    cr.Fill ();
                }
            }

        }
        
        ~Moodbar ()
        {
            if (mood_surface != null)
                mood_surface.Dispose ();
        }
        
        static public Moodbar LoadMoodbar (string mood_file_name) {
//            var mood_file_path = GetMoodFilePath(audio_file_path);
            if(mood_file_name == null)
                return null;
            
            var mood_file_path = System.IO.Path.Combine(mood_files_storage, mood_file_name);
            if (System.IO.File.Exists(mood_file_path))
                return new Moodbar (mood_file_path);
            else
                return null;
        }
        
        static public string GetMoodFileName (string audio_file_path)
        {
            audio_file_path = audio_file_path.Replace (System.IO.Path.DirectorySeparatorChar, ',');
            return audio_file_path+".mood";
        }
        
        static public string GetMoodFilePath (string audio_file_path)
        {
            audio_file_path = audio_file_path.Replace (System.IO.Path.DirectorySeparatorChar, ',');
            return System.IO.Path.Combine(mood_files_storage, audio_file_path+".mood");
        }
        
        public void Render (Cairo.Context context, int x, int y, int width, int height)
        {
            // scale and draw moodbar
            double scaled_width = (width) / 1000.0;
            context.Save ();
            context.Scale (scaled_width, height * 2);
            context.SetSourceSurface (Surface, (int)(x / scaled_width), y);
            context.Paint ();
            context.Restore ();

        }
        
        #region IDisposable implementation
        public void Dispose ()
        {
            mood_surface.Dispose ();
            GC.SuppressFinalize(this);
        }
        #endregion

        static public string MoodFilesStorage {
            get { return mood_files_storage; }
        }
        
        public Cairo.ImageSurface Surface {
            get { return this.mood_surface; }
        }

        public string FilePath {
            get { return this.mood_file_path; }
        }
    }
}
