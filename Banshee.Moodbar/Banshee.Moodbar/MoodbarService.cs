// 
// MyClass.cs
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
using Hyena.Data.Gui;
using Banshee.Base;
using Banshee.Collection.Database;
using Banshee.Widgets;
using Banshee.I18n;
using Banshee.Gui;
using Banshee.Gui.Widgets;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Preferences;
using Banshee.Configuration;
using System.Collections.Generic;

namespace Banshee.Moodbar
{
    public class MoodbarService : IExtensionService
    {
        private bool disposed;
        private object job_sync = new object ();
        private object sync = new object ();
        private MoodbarDetectJob job;
        private GtkElementsService elements_service;
        private ToolItem moodbar_toolitem;
        private ToolItem  connected_toolitem;
        private Toolbar pwin_toolbar;
        private Column moodbar_column;
        private Dictionary<string, Moodbar> loaded_moodbars = new Dictionary<string, Moodbar> ();
        private static Random rnd = new Random ();
        private List<int> loaded_sources = new List<int> ();
        
        
        #region IExtensionService implementation
        void IExtensionService.Initialize ()
        {
            if (!ServiceManager.DbConnection.TableExists ("MoodPaths")) {
                ServiceManager.DbConnection.Execute (@"
                    CREATE TABLE MoodPaths (
                        TrackID     INTEGER UNIQUE,
                        FileName  STRING,
                        LastAttempt INTEGER NOT NULL
                    )");
            }
            
            elements_service = ServiceManager.Get<GtkElementsService> ();

            if (!ServiceStartup ()) {
                ServiceManager.ServiceStarted += OnServiceStarted;
                ServiceManager.SourceManager.SourceAdded += OnSourceAdded;
            }
        }
        #endregion
        
        private void OnServiceStarted (ServiceStartedArgs args)
        {
            if (args.Service is GtkElementsService)
                elements_service = (GtkElementsService)args.Service;
            
            ServiceStartup ();
        }

        private void OnSourceAdded (SourceAddedArgs args)
        {
            ServiceStartup ();
        }

        private bool ServiceStartup ()
        {
            if (elements_service == null || ServiceManager.SourceManager.MusicLibrary == null)
                return false;
            ServiceManager.ServiceStarted -= OnServiceStarted;
            ServiceManager.SourceManager.SourceAdded -= OnSourceAdded;
            
            InstallGui ();
            InstallPreferences ();
            ServiceManager.SourceManager.MusicLibrary.TracksAdded += OnTracksChangedOrAdded;
            ServiceManager.SourceManager.MusicLibrary.TracksChanged += OnTracksChangedOrAdded;
            
            Banshee.ServiceStack.Application.RunTimeout (10000, delegate {
                TriggerDetectorJob ();
                return false;
            });

            return true;
        }
        
        private void OnTracksChangedOrAdded (Source sender, TrackEventArgs args)
        {
            TriggerDetectorJob ();
        }

        private void TriggerDetectorJob ()
        {
            if (!Enabled) {
                return;
            }
            
            lock (job_sync) {
                if (job != null) {
                    return;
                } else {
                    job = new MoodbarDetectJob ();
                }
            }
            
            job.Finished += delegate { job = null; };
        }

        private void InstallGui ()
        {
            // find sources and add mood column
            InitMoodbarColumn ();
            SwapSeekSlider ();
        }
        
        private void SwapSeekSlider ()
        {
            // ripped off of Moblin extension
            
            // First grab the type and instance of the primary window
            // and make sure we're only hacking the Nereid UI
            var pwin = elements_service.PrimaryWindow;
            var pwin_type = pwin.GetType ();
            if (pwin_type.FullName != "Nereid.PlayerInterface") {
                return;
            }
            
            // Find ConnectedSeekSlider
            pwin_toolbar = (Toolbar)pwin_type.GetProperty ("HeaderToolbar").GetValue (pwin, null);
            foreach (var child in pwin_toolbar.Children) {
                if (child.GetType ().FullName.StartsWith ("Banshee.Widgets.GenericToolItem")) {
                    var c = child as Container;
                    if (c != null && c.Children[0] is ConnectedSeekSlider) {
                        connected_toolitem = child as ToolItem;
                        break;
                    }
                }
            }
            
            // swap original ConnectedSeekSlider with new ConnectedMoodSeekSlider
            var pos = pwin_toolbar.GetItemIndex (connected_toolitem as ToolItem);
            pwin_toolbar.Remove (connected_toolitem as Gtk.ToolItem);
            
            ConnectedMoodbarSeekSlider connected_moodbar_seek_slider;
            connected_moodbar_seek_slider = new ConnectedMoodbarSeekSlider ();
            connected_moodbar_seek_slider.Show ();
            
            moodbar_toolitem = new GenericToolItem<ConnectedMoodbarSeekSlider> (connected_moodbar_seek_slider);
            moodbar_toolitem.Show ();
            pwin_toolbar.Insert (moodbar_toolitem, pos);

        }
        
        private void InitMoodbarColumn ()
        {
            ServiceManager.SourceManager.ActiveSourceChanged += delegate(SourceEventArgs args) {
                if (args.Source is Banshee.Library.MusicLibrarySource || args.Source.Parent is Banshee.Library.MusicLibrarySource) {
                    AddMoodbarColumn (args.Source);

                    // load moodbars for the enire source
                    var source = args.Source as Banshee.Library.MusicLibrarySource;
                    if (source != null) {
                        LoadAllMoodbarsFromSource (source);
                    }
                }
            };
            
            ServiceManager.SourceManager.SourceAdded += delegate(SourceAddedArgs args) {
                if (args.Source is Banshee.Library.MusicLibrarySource || args.Source.Parent is Banshee.Library.MusicLibrarySource) {
                    AddMoodbarColumn (args.Source);
                    
                    // load moodbars for the enire source
                    var source = args.Source as Banshee.Library.MusicLibrarySource;
                    if (source != null) {
                        LoadAllMoodbarsFromSource (source);
                    }
                }
            };
            
            foreach (Source src in ServiceManager.SourceManager.Sources) {
                if (src is Library.MusicLibrarySource || src.Parent is Banshee.Library.MusicLibrarySource) {
                    AddMoodbarColumn (src);
                    
                    // load moodbars for the enire source
                    var source = src as Banshee.Library.MusicLibrarySource;
                    if (source != null) {
                        LoadAllMoodbarsFromSource (source);
                    }
                }
            }
        }
        
        private void LoadAllMoodbarsFromSource (PrimarySource source)
        {
            if (loaded_sources.Contains (source.DbId))
                return;
         
            source.DatabaseTrackModel.Reloaded += delegate {
                LoadAllMoodbarsFromSource (source);
            };
         
            if (source.TrackModel.Count == 0)
                return;

            //TODO: maybe use a threadpool ?!
            Banshee.Base.ThreadAssist.SpawnFromMain (delegate {
                CachedList<DatabaseTrackInfo> cached_list = CachedList<DatabaseTrackInfo>.CreateFromModel (source.DatabaseTrackModel);
                for (int i = 0; i < cached_list.Count; i++) {
                        GetMoodbar (cached_list[i].Uri);
                }
            });
            loaded_sources.Add (source.DbId);
            
        }
        
        private void AddMoodbarColumn (Source source)
        {
            ListView<Banshee.Collection.TrackInfo> track_view = source.Properties.Get<ListView<Banshee.Collection.TrackInfo>> ("Track.IListView");
            if (track_view == null)
                return;
            
            ColumnController controller = track_view.ColumnController;
            if (controller == null)
                return;
            
            foreach (var col in controller)
                if (col.Id == "moodbar")
                    return;

            moodbar_column = new Column ("Moodbar", new ColumnCellMoodbar (Banshee.Query.BansheeQuery.UriField.PropertyName, true), 0.33);
            moodbar_column.Id = "moodbar";
            controller.Add (moodbar_column);
        }
        
        

        public Moodbar GetMoodbarQuick (SafeUri audio_file_uri)
        {
            if (audio_file_uri == null)
                throw new ArgumentNullException ("Uri cannot be null.");
            if (!audio_file_uri.IsLocalPath)
                throw new ArgumentException ("Uri must point to a local file.");
            
            return GetMoodbar (audio_file_uri.LocalPath);
        }

        public Moodbar GetMoodbarQuick (string audio_file_path)
        {
            if (string.IsNullOrEmpty (audio_file_path))
                throw new ArgumentNullException ("Path to file cannot be empty.");
            
            Moodbar moodbar = null;
            lock (sync) {
                loaded_moodbars.TryGetValue (audio_file_path, out moodbar);
            }
            
            return moodbar;
        }
        
        public Moodbar GetMoodbar (SafeUri audio_file_uri)
        {
            if (audio_file_uri == null)
                throw new ArgumentNullException ("Uri cannot be null.");
            if (!audio_file_uri.IsLocalPath)
                throw new ArgumentException ("Uri must point to a local file.");
            
            return GetMoodbar (audio_file_uri.LocalPath);
        }

        public Moodbar GetMoodbar (string audio_file_path)
        {
            if (string.IsNullOrEmpty (audio_file_path))
                throw new ArgumentNullException ("Path to file cannot be empty.");
            
            Moodbar moodbar = null;
            lock (sync) {
                loaded_moodbars.TryGetValue (audio_file_path, out moodbar);
            }
            
            if (moodbar == null) {
                string mood_file_name = null;
                
                // try to look up mood's file name in DB
                int track_id = DatabaseTrackInfo.GetTrackIdForUri (SafeUri.FilenameToUri (audio_file_path));
                mood_file_name = ServiceManager.DbConnection.Query<string> ("SELECT FileName FROM MoodPaths WHERE TrackID = ? LIMIT 1", track_id);
                
                // if not found, generate it's name
                if (string.IsNullOrEmpty (mood_file_name)) {
                    mood_file_name = Moodbar.GetMoodFileName (audio_file_path);
                }
                
                moodbar = Moodbar.LoadMoodbar (mood_file_name);
                
                if (moodbar != null) {
                    lock (sync) {
                        loaded_moodbars.Add (audio_file_path, moodbar);
                    }
                }
            }
            
            return moodbar;
        }
        
        public void DetectMood (SafeUri audio_file_uri, Action<Moodbar> when_finished_closure)
        {
            if (audio_file_uri == null)
                throw new ArgumentNullException ("Uri cannot be null.");
            if (!audio_file_uri.IsLocalPath)
                throw new ArgumentException ("Uri must point to a local file.");

            if (!Banshee.IO.Directory.Exists (Moodbar.MoodFilesStorage))
                Banshee.IO.Directory.Create (Moodbar.MoodFilesStorage);
            
            // using a temp file - nothing will try to load it during it's generation'
            var mood_file_path = Moodbar.GetMoodFilePath (audio_file_uri.LocalPath);
            var rnd_int = rnd.Next ();
            var temp_mood_file_path = Moodbar.GetMoodFilePath (rnd_int.ToString () + ".tmp");
            
            var args = string.Format ("\"{0}\" -o \"{1}\"", audio_file_uri.LocalPath, temp_mood_file_path);
            var proc_info = new System.Diagnostics.ProcessStartInfo ("moodbar", args);
            var proc = new System.Diagnostics.Process ();
            proc.StartInfo = proc_info;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.EnableRaisingEvents = true;
            proc.Exited += delegate {
                // add entry to DB
                int track_id = DatabaseTrackInfo.GetTrackIdForUri (audio_file_uri.AbsoluteUri);
                string mood_file_name = Moodbar.GetMoodFileName (audio_file_uri.LocalPath);
                ServiceManager.DbConnection.Execute (
                    "INSERT OR REPLACE INTO MoodPaths (TrackID, FileName, LastAttempt) VALUES (?, ?, ?)",
                    track_id, mood_file_name, DateTime.Now);
                
                if (proc.ExitCode == 0) {
                    // rename temp file to original name
                    Banshee.IO.File.Move (new SafeUri (SafeUri.FilenameToUri (temp_mood_file_path)),
                        new SafeUri (SafeUri.FilenameToUri (mood_file_path)));
                    
                    var moodbar = Moodbar.LoadMoodbar (mood_file_name);
                    lock (sync) {
                        loaded_moodbars.Add (audio_file_uri.LocalPath, moodbar);
                    }
                    
                    when_finished_closure (moodbar);
                } else {
                    // when i used Banshe.IO.File.Delete it freezed - why ?!
                    System.IO.File.Delete (temp_mood_file_path);
                    Hyena.Log.ErrorFormat ("Error while detecting mood: program exited with exit code: {0}\n{1}", proc.ExitCode, proc.StandardOutput.ReadToEnd ());
                    when_finished_closure (null);
                }
            };
            
            try {
                proc.Start ();
            } catch (Exception e) {
                Hyena.Log.ErrorFormat ("Error while detecting mood {0}", e);
                when_finished_closure (null);
            }
        }

        #region IDisposable implementation
        public void Dispose ()
        {
            if (disposed) {
                return;
            }
            
            if (job != null) {
                ServiceManager.JobScheduler.Cancel (job);
            }

            UninstallPreferences ();
            ServiceManager.SourceManager.MusicLibrary.TracksAdded -= OnTracksChangedOrAdded;
            ServiceManager.SourceManager.MusicLibrary.TracksChanged -= OnTracksChangedOrAdded;
            
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                // swap ConnectedMoodSeekSlider with original ConnectedSeekSlider
                var pos = pwin_toolbar.GetItemIndex (moodbar_toolitem);
                pwin_toolbar.Remove (moodbar_toolitem);
                pwin_toolbar.Insert (connected_toolitem, pos);
                
                // remove moodbar column
                var source = ServiceManager.SourceManager.MusicLibrary;
                ListView<Banshee.Collection.TrackInfo> track_view = source.Properties.Get<ListView<Banshee.Collection.TrackInfo>> ("Track.IListView");
                if (track_view == null)
                    return;
                ColumnController controller = track_view.ColumnController;
                if (controller == null)
                    return;
                controller.Remove (moodbar_column);
                
                disposed = true;
            });
        }
        #endregion
        
        #region Preferences
        private PreferenceBase enabled_pref;

        private void InstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }
            
            enabled_pref = ServiceManager.SourceManager.MusicLibrary.PreferencesPage["misc"].Add (
                new SchemaPreference<bool> (EnabledSchema, Catalog.GetString ("_Automatically detect Mood for all songs"), 
                    Catalog.GetString ("Detect Mood for all songs that don't already have it generated"), 
                    delegate { Enabled = EnabledSchema.Get (); }));
        }

        private void UninstallPreferences ()
        {
            ServiceManager.SourceManager.MusicLibrary.PreferencesPage["misc"].Remove (enabled_pref);
        }
        #endregion
        
        public bool Enabled {
            get { return EnabledSchema.Get (); }
            set {
                EnabledSchema.Set (value);
                if (value) {
                    TriggerDetectorJob ();
                } else {
                    if (job != null) {
                        ServiceManager.JobScheduler.Cancel (job);
                    }
                }
            }
        }

        private static readonly SchemaEntry<bool> EnabledSchema = new SchemaEntry<bool> (
            "plugins.moodbar", "auto_enabled", false, 
            "Automatically detect Mood on imported music", 
            "Automatically detect Mood on imported music");

        
        #region IService implementation
        string IService.ServiceName {
            get { return "MoodbarService"; }
        }
        #endregion
        
    }
}
