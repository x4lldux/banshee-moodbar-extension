// 
// MoodbarDetectJob.cs
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

using Banshee.I18n;
using System.Threading;

using Hyena;
using Hyena.Jobs;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Sources;
using Banshee.Metadata;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Library;

namespace Banshee.Moodbar
{
    public class MoodbarDetectJob : DbIteratorJob
    {
        private PrimarySource music_library;
        private MoodbarService moodbar_service;
        private ManualResetEvent result_ready_event = new ManualResetEvent (false);
        
        public MoodbarDetectJob () : base(Catalog.GetString ("Detecting Mood"))
        {
            IconNames = new string[] { "audio-x-generic" };
            IsBackground = true;
            SetResources (Resource.Cpu, Resource.Disk);
            PriorityHints = PriorityHints.LongRunning;
            CanCancel = true;
            DelayShow = true;


            music_library = ServiceManager.SourceManager.MusicLibrary;

            CountCommand = new HyenaSqliteCommand (@"
                SELECT Uri
                    FROM CoreTracks
                    WHERE 
                        PrimarySourceID = ? AND
                        TrackID NOT IN (
                            SELECT TrackID FROM MoodPaths
                            WHERE LastAttempt > ?
                        ) AND
                        TrackID NOT IN (
                            SELECT TrackID FROM MoodPaths
                            WHERE FileName IS NOT NULL
                        )
                        ",
                music_library.DbId, DateTime.Now - TimeSpan.FromDays (1));
        
            SelectCommand = new HyenaSqliteCommand (@"
                SELECT Uri
                    FROM CoreTracks
                    WHERE 
                        PrimarySourceID = ? AND
                        TrackID NOT IN (
                            SELECT TrackID FROM MoodPaths
                            WHERE LastAttempt > ?
                        ) AND
                        TrackID NOT IN (
                            SELECT TrackID FROM MoodPaths
                            WHERE FileName IS NOT NULL
                        )
                    LIMIT 1",
                music_library.DbId, DateTime.Now - TimeSpan.FromDays (1));

            Register ();
        }
        
        protected override void Init ()
        {
            moodbar_service = ServiceManager.Get<MoodbarService> ();
        }

        protected override void OnCancelled ()
        {
            Cleanup ();
            result_ready_event.Set ();
        }

        protected override void Cleanup ()
        {
            moodbar_service = null;

            base.Cleanup ();
        }
        
        protected override void IterateCore (HyenaDataReader reader)
        {
            SafeUri uri = new SafeUri (reader.Get<string> (0));
            
            Log.Debug ("Detecting mood for " + uri.LocalPath);
            result_ready_event.Reset ();
            
            if (IsCancelRequested) {
                Log.Debug ("Detection canceled");
                return;
            }
            
            if (moodbar_service == null) {
                Log.Debug ("Detection stopped - moodbar_service is null");
                return;
            }


            moodbar_service.DetectMood (uri, m =>
            {
                if (m != null) {
                    Log.Debug ("Mood detected and saved as " + m.FilePath);
                    Thread.Sleep (5000);
                } else {
                    Log.Debug ("Mood not detected");
                }
                result_ready_event.Set ();
            });
            result_ready_event.WaitOne ();
            
        }
    }
}
