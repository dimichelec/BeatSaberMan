using Newtonsoft.Json;
using LibVLCSharp.Shared;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace BeatSaberMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string STOP_CHAR = "■";
        private const string PLAY_CHAR = "►";

        private class SongCell
        {
            public string SongDir { get; set; }
            public BitmapImage CoverImage { get; set; }
            public string SongName { get; set; }
            public string Artist { get; set; }
            public string LevelAuthor { get; set; }
            public string TrackTime { get; set; }
            public string MapVersion { get; set; }
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
        }

        public string GetLevelBackground(float plays)
        {
            return plays > 0.8 ? "GreenYellow" : plays > 0.5 ? "Yellow" : plays > 0.0 ? "Orange" : "Transparent";
        }

        private Dictionary<string, Dictionary<string, object>> SongHashes;
        private Dictionary<string, object> PlayerData;
        private List<Dictionary<string, object>> LocalPlayerData;
        public void GetSongAppData()
        {
            string dataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\..\LocalLow\Hyperbolic Magnetism\Beat Saber\";
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

            //-Play count data is in PlayerData.dat in this folder
            //- hashes in SongHashData.dat link to SongCoreExtraData.dat and PlayerData.dat data
            //-note: PlayerData.dat uses hashes AND "custom_level_<song folder name>"

            int totalPlays = 0;
            int[] plays = { 0, 0, 0, 0, 0 };
            string songName = info._songName.ToString();
            string levelDirName = "custom_level_" + dir.Split(@"\")[^1];
            string songHash = "";
            if (SongHashes.ContainsKey(dir))
            {
                songHash = SongHashes[dir]["songHash"].ToString();
            }
            foreach (dynamic dat in LocalPlayerData)
            {
                if ((dat["levelId"] == songName) || (dat["levelId"] == levelDirName) || (dat["levelId"] == songHash))
                {
                    plays[(int)dat["difficulty"]] += (int)dat["playCount"];
                    totalPlays += (int)dat["playCount"];
                }
            }

            // process map levels
            DateTime lastAccess = new DateTime(0);
            Dictionary<string, int> maps = new Dictionary<string, int>();
            foreach (dynamic mapsets in info._difficultyBeatmapSets)
            {
                foreach (dynamic map in mapsets._difficultyBeatmaps)
                {
                    string level = map._difficulty;
                    maps[level] = 1;
                }
            }
            string sPlays = "";
            string[] levels = new[] { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };
            foreach ((string level, int index) in levels.Select((level, index) => (level, index)))
            {
                if (maps.ContainsKey(level))
                {
                    info[level + "Foreground"] = "Black";
                    info[level + "Background"] = GetLevelBackground((float)plays[index] / (float)totalPlays);
                    sPlays += plays[index].ToString() + " ";
                }
                else
                {
                    info[level + "Foreground"] = "LightGray";
                    info[level + "Background"] = "Transparent";
                    sPlays += "- ";
                }
            }
            info["Plays"] = "Plays: " + totalPlays.ToString() + " [" + sPlays.Trim() + "]";

            // add the audio file's duration to the info object before returning it
            Media media = new Media(libvlc, new Uri(dir + @"\" + info._songFilename));
            _ = media.Parse();
            while (media.ParsedStatus != MediaParsedStatus.Done)
            {
                Thread.Sleep(1);
            }
            info["Duration"] = (media.Duration / 60000).ToString("D2") + ":" + (media.Duration / 1000 % 60).ToString("D2");

            return info;
        }

        private LibVLC libvlc;
        private MediaPlayer vlcPlayer;

        public MainWindow()
        {
            InitVLC();
            InitializeComponent();
            LoadSongs();
        }

        private void InitVLC()
        {
            libvlc = new LibVLC(enableDebugLogs: true);
            vlcPlayer = new MediaPlayer(libvlc);
        }

        private static Button lastPlayed = null;
        private Button StopPlaying()
        {
            Button tmp = lastPlayed;
            if (vlcPlayer.IsPlaying)
            {
                vlcPlayer.Stop();
            }
            if (lastPlayed != null)
            {
                lastPlayed.Content = PLAY_CHAR;
                lastPlayed = null;
            }
            return tmp;
        }

        private void OnClickPlay(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            if (StopPlaying() == clicked)
            {
                return;
            }

            string dir = clicked.Tag.ToString();
            dynamic array = GetSongInfo(dir);
            string songFilename = dir + @"\" + array._songFilename;

            _ = vlcPlayer.Play(new Media(libvlc, new Uri(songFilename)));
            clicked.Content = STOP_CHAR;
            lastPlayed = clicked;
        }

        private void OnClickDelete(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            dynamic info = GetSongInfo(dir);
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to delete " + info._songName + "?", "Confirmation", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                foreach (var item in lbSongs.Items)
                {
                    if (lbSongs.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                    {
                        if (container.DataContext is SongCell dat)
                        {
                            if (dat.SongDir == dir)
                            {
                                lbSongs.Items.Remove(item);
                                Directory.Delete(dir, true);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void OnClickFolder(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            Process.Start("explorer.exe", dir);
        }

        private void LoadSongs()
        {
            GetSongAppData();

            string rootPath = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels";
            string[] dirs = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly);

            foreach (string dir in dirs)
            {
                dynamic info = GetSongInfo(dir);
                
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(dir + @"\" + info._coverImageFilename);
                img.EndInit();

                _ = lbSongs.Items.Add(new SongCell()
                {
                    SongDir = dir,
                    CoverImage = img,
                    SongName = info._songName,
                    Artist = info._songAuthorName,
                    LevelAuthor = "Map by " + info._levelAuthorName,
                    TrackTime = info.Duration,
                    MapVersion = "Map v" + info._version,
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
                });
            }

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (vlcPlayer.IsPlaying)
            {
                vlcPlayer.Stop();
            }
            vlcPlayer.Dispose();
        }

    }
}
