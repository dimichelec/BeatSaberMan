using Newtonsoft.Json;
using LibVLCSharp.Shared;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Security.Policy;

namespace BeatSaberMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LibVLC libvlc;
        private LibVLCSharp.Shared.MediaPlayer vlcPlayer;

        private class SongCell
        {
            public string SongDir { get; set; }
            public BitmapImage CoverImage { get; set; }
            public string SongName { get; set; }
            public string Artist { get; set; }
            public string LevelAuthor { get; set; }
            public string TrackTime { get; set; }
            public string BPM { get; set; }
            public string EasyForeground { get; set; }
            public string EasyBackground { get; set; }
            public string NormalForeground { get; set; }
            public string NormalBackground { get; set; }
            public string HardForeground { get; set; }
            public string HardBackground { get; set; }
            public string ExpertForeground { get; set; }
            public string ExpertBackground { get; set; }
            public string ExpertPlusForeground { get; set; }
            public string ExpertPlusBackground { get; set; }
            public string Plays { get; set; }
            public string LevelErrors { get; set; }
            public string FixEnabled { get; set; }
        }

        private int ErroneousSongs = -1;

        const string BeatSaberRootPath = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels";

        private Dictionary<string, object> PlayerData;
        private List<Dictionary<string, object>> LocalPlayerData;
        private Dictionary<string, Dictionary<string, object>> SongHashes;

        // -------------------------------------------------------------------------------------------------------------------------------

        public void GetSongAppData()
        {
            string dataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) 
                + @"\..\LocalLow\Hyperbolic Magnetism\Beat Saber\";
            SongHashes = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(
                new StreamReader(dataDir + @"\SongHashData.dat").ReadToEnd());
            PlayerData = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                new StreamReader(dataDir + @"\PlayerData.dat").ReadToEnd());
            List<object> tmp = JsonConvert.DeserializeObject<List<object>>(PlayerData["localPlayers"].ToString());
            Dictionary<string, object> tmp2 = JsonConvert.DeserializeObject<Dictionary<string, object>>(tmp[0].ToString());
            LocalPlayerData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(tmp2["levelsStatsData"].ToString());
        }

        public dynamic GetSongInfo(string dir)
        {
            // get the info.dat json file for the song
            StreamReader infoFile = new StreamReader(dir + @"\info.dat");
            dynamic info = JsonConvert.DeserializeObject(infoFile.ReadToEnd());
            infoFile.Close();

            info["Duration"] = GetMediaDuration(dir + @"\" + info._songFilename);
            return info;
        }

        public string GetMediaDuration(string path)
        {
            Media media = new Media(libvlc, new Uri(path));
            media.Parse();
            while (media.ParsedStatus != MediaParsedStatus.Done) Thread.Sleep(1);
            return (media.Duration / 60000).ToString("D2") + ":" + (media.Duration / 1000 % 60).ToString("D2");
        }

        public dynamic AddLevelsData(string dir, dynamic info)
        {
            // get map levels numbers of plays
            int[] plays = GetPlaysData(dir, info);
            int playsTotal = plays.Sum();

            // get beatmap error test results
            Dictionary<string, int> maps = TestBeatmaps(dir, info);

            string sPlays = "";
            string errors = "";
            string[] levels = new[] { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };
            foreach ((string level, int index) in levels.Select((level, index) => (level, index)))
            {
                if (maps.ContainsKey(level))
                {
                    info[level + "Foreground"] = maps[level] == 1 ? "Black" : "Red";
                    info[level + "Background"] = GetLevelBackground(plays[index] / (float)playsTotal);
                    sPlays += plays[index].ToString() + " ";
                    errors += maps[level] == 1 ? "0" : "1";
                }
                else
                {
                    info[level + "Foreground"] = "LightGray";
                    info[level + "Background"] = "Transparent";
                    sPlays += "- ";
                    errors += "-";
                }
            }
            info["Plays"] = "Plays: " + playsTotal.ToString() + " [" + sPlays.Trim() + "]";
            info["LevelErrors"] = errors;
            return info;
        }

        public string GetLevelBackground(float plays)
        {
            return plays > 0.8 ? "GreenYellow" : plays > 0.5 ? "Yellow" : plays > 0.0 ? "Orange" : "Transparent";
        }

        public int[] GetPlaysData(string dir, dynamic info)
        {
            int[] plays = { 0, 0, 0, 0, 0 };
            string songName = info._songName.ToString();
            string levelDirName = "custom_level_" + dir.Split(@"\")[^1];
            string songHash = SongHashes.ContainsKey(dir) ? SongHashes[dir]["songHash"].ToString() : "";
            foreach (dynamic dat in LocalPlayerData)
            {
                if ((dat["levelId"] == songName) || (dat["levelId"] == levelDirName) || (dat["levelId"] == songHash))
                    plays[(int)dat["difficulty"]] += (int)dat["playCount"];
            }
            return plays;
        }

        public bool TestBeatmapFile(string path)
        {
            StreamReader infoFile = new StreamReader(path);
            string stored = infoFile.ReadToEnd();
            infoFile.Close();

            // the stored map file has to match this or Beat Saber will not load it (spinning infinitely)
            return new Regex(@"{\s*""_?version"":""\d").IsMatch(stored);
        }

        public bool FixBeatmap(string path)
        {
            StreamReader reader = new StreamReader(path);
            JObject json = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            string key = "_version";
            string version = "2.0.0";
            if (json.Property("_version") != null)
            {
                version = (string)json["_version"];
                json.Remove("_version");
            }
            if (json.Property("version") != null)
            {
                version = (string)json["version"];
                key = "version";
                json.Remove("version");
            }
            json.AddFirst(new JProperty(key, version));

            StreamWriter writer = new StreamWriter(path);
            writer.WriteLine(JsonConvert.SerializeObject(json, Formatting.None));
            writer.Close();
            return true;
        }

        public Dictionary<string, int> TestBeatmaps(string dir, dynamic info)
        {
            Dictionary<string, int> maps = new Dictionary<string, int>();
            foreach (dynamic mapsets in info._difficultyBeatmapSets)
            {
                foreach (dynamic map in mapsets._difficultyBeatmaps)
                {
                    maps[map._difficulty.ToString()] = TestBeatmapFile(dir + @"\" + map._beatmapFilename.ToString()) ? 1 : -1;
                }
            }
            return maps;
        }

        // -------------------------------------------------------------------------------------------------------------------------------

        public MainWindow()
        {
            InitVLC();
            InitializeComponent();
            GetSongAppData();
            LoadSongs();
        }

        private void InitVLC()
        {
            libvlc = new LibVLC(enableDebugLogs: true);
            vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(libvlc);
        }

        private void UpdateTopBar()
        {
            int songs = Directory.GetDirectories(BeatSaberRootPath, "*", SearchOption.TopDirectoryOnly).Length;
            tbSongCount.Text = songs.ToString() + " SONGS";
            tbErroneousSongs.Text = "";
            if (ErroneousSongs > 0)
            {
                tbSongCount.Text += ",";
                tbErroneousSongs.Text = ErroneousSongs.ToString() + " WITH BEATMAP ERRORS";
            }
        }

        private int LoadSong(string dir, int index = -1)
        {
            dynamic info = GetSongInfo(dir);
            info = AddLevelsData(dir, info);

            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(dir + @"\" + info._coverImageFilename);
            img.EndInit();

            SongCell song = new SongCell()
            {
                SongDir = dir,
                CoverImage = img,
                SongName = info._songName,
                Artist = info._songAuthorName,
                LevelAuthor = "Map by " + info._levelAuthorName,
                TrackTime = info.Duration,
                BPM = info._beatsPerMinute.ToString().Split(".")[0] + " BPM",
                EasyForeground = info.EasyForeground,
                EasyBackground = info.EasyBackground,
                NormalForeground = info.NormalForeground,
                NormalBackground = info.NormalBackground,
                HardForeground = info.HardForeground,
                HardBackground = info.HardBackground,
                ExpertForeground = info.ExpertForeground,
                ExpertBackground = info.ExpertBackground,
                ExpertPlusForeground = info.ExpertPlusForeground,
                ExpertPlusBackground = info.ExpertPlusBackground,
                Plays = info.Plays,
                LevelErrors = info.LevelErrors,
                FixEnabled = Regex.IsMatch(info.LevelErrors.ToString(), @"[^-0]") ? "True" : "False",
            };

            if (index == -1) lbSongs.Items.Add(song);
            else
            {
                lbSongs.Items.RemoveAt(index);
                lbSongs.Items.Insert(index, song);
            }
            return Regex.IsMatch(info.LevelErrors.ToString(), @"[^-0]") ? -1 : 0;
        }

        private void LoadSongs()
        {
            ErroneousSongs = 0;
            string[] dirs = Directory.GetDirectories(BeatSaberRootPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string dir in dirs)
                if (LoadSong(dir) == -1) ErroneousSongs++;
            UpdateTopBar();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (vlcPlayer.IsPlaying) vlcPlayer.Stop();
            vlcPlayer.Dispose();
        }

        // -------------------------------------------------------------------------------------------------------------------------------

        private static Button lastPlayed = null;

        private Button StopPlaying()
        {
            Button tmp = lastPlayed;
            if (vlcPlayer.IsPlaying) vlcPlayer.Stop();
            if (lastPlayed != null)
            {
                lastPlayed.Content = new Image() { Source = (ImageSource)FindResource("PlayIcon") };
                lastPlayed = null;
            }
            return tmp;
        }

        private (SongCell song, int index) FindMyListBoxItem(string dir)
        {
            foreach (SongCell item in lbSongs.Items)
            {
                if (lbSongs.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                    if (container.DataContext is SongCell dat)
                        if (dat.SongDir == dir)
                            return (item, lbSongs.Items.IndexOf(item));
            }
            return (null, -1);
        }

        private void RefreshListBox()
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            lbSongs.IsEnabled = false;
            lbSongs.Opacity = 0.1;
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => {
                lbSongs.Items.Clear();
                LoadSongs();
            }));
            lbSongs.IsEnabled = true;
            lbSongs.Opacity = 1;
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void OnClickPlay(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            if (StopPlaying() == clicked) return;
            string dir = clicked.Tag.ToString();
            dynamic array = GetSongInfo(dir);
            string songFilename = dir + @"\" + array._songFilename;

            vlcPlayer.Play(new Media(libvlc, new Uri(songFilename)));
            clicked.Content = new Image() { Source = (ImageSource)FindResource("StopIcon") };
            lastPlayed = clicked;
        }

        private void OnClickFolder(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            Process.Start("explorer.exe", dir);
        }

        private void OnClickFix(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            dynamic info = GetSongInfo(dir);
            if (MessageBox.Show("Are you sure you want to fix the level files for \""
                + info._songName + "\"?  This will overwrite its suspicious .dat files!",
                "BeatSaberMan",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Exclamation) != MessageBoxResult.OK) return;

            StopPlaying();
            foreach (dynamic mapsets in info._difficultyBeatmapSets)
            {
                foreach (dynamic map in mapsets._difficultyBeatmaps)
                {
                    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    if (!TestBeatmapFile(dir + @"\" + map._beatmapFilename.ToString()))
                    {
                        if (!FixBeatmap(dir + @"\" + map._beatmapFilename.ToString()))
                            MessageBox.Show("\"" + map._beatmapFilename.ToString() + "\" NOT fixed!",
                                "BeatSaberMan",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                    }
                }
            }
            if (LoadSong(dir, FindMyListBoxItem(dir).index) == 0) ErroneousSongs--;
            UpdateTopBar();
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void OnClickDelete(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            dynamic info = GetSongInfo(dir);
            if (MessageBox.Show(
                "Are you sure you want to delete \"" + info._songName + "\"?  This will trash its folder and its contents!",
                "BeatSaberMan",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Exclamation) == MessageBoxResult.OK)
            {
                lbSongs.Items.Remove(FindMyListBoxItem(dir));
                Directory.Delete(dir, true);
                RefreshListBox();
            }
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            RefreshListBox();
        }

    }
}
