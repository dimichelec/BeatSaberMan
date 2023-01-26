using LibVLCSharp.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BeatSaberMan
{
    internal class SongList
    {
        private LibVLC libvlc;
        private MediaPlayer vlcPlayer;

        const string BeatSaberRootPath = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels";

        public int ErroneousSongCount { get; set; }

        public List<Song> songs = new List<Song>();

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
            // get song info from info.dat file in the song's folder
            string filename = dir + @"\info.dat";
            if (File.Exists(filename))
            {
                StreamReader infoFile = new StreamReader(filename);
                dynamic info = JsonConvert.DeserializeObject(infoFile.ReadToEnd());
                infoFile.Close();
                return info;
            }
            else return null;
        }

        public Dictionary<string, int[]> GetPlaysData(string dir, dynamic info)
        {
            int[] plays = { 0, 0, 0, 0, 0 };
            int[] highScores = { -1, -1, -1, -1, -1 };
            string levelDirName = "custom_level_" + dir.Split(@"\")[^1];
            string songHash = SongHashes.ContainsKey(dir) ? SongHashes[dir]["songHash"].ToString() : "";
            foreach (dynamic dat in LocalPlayerData)
            {
                if ((dat["levelId"] == info._songName.ToString()) || (dat["levelId"] == levelDirName) || (dat["levelId"] == songHash))
                {
                    plays[(int)dat["difficulty"]] += (int)dat["playCount"];
                    highScores[(int)dat["difficulty"]] = (int)dat["highScore"];
                }
            }
            var dict = new Dictionary<string, int[]>();
            dict.Add("plays", plays);
            dict.Add("highScores", highScores);
            return dict;
        }

        private void InitVLC()
        {
            libvlc = new LibVLC(enableDebugLogs: true);
            vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(libvlc);
        }

        public int Count()
        {
            return songs.Count;
        }

        public void PlayByDir(string dir)
        {
            foreach (Song song in songs)
            {
                if (song.SongDir.Equals(dir))
                {
                    vlcPlayer.Play(new Media(libvlc, new Uri(dir + @"\" + song.Filename)));
                    return;
                }
            }
        }

        public void StopPlaying()
        {
            if (vlcPlayer.IsPlaying) vlcPlayer.Stop();
        }

        public Song FindSongByDir(string dir)
        {
            foreach (Song song in songs)
            {
                if (song.SongDir.Equals(dir)) return song;
            }
            return null;
        }

        public void Clear()
        {
            songs.Clear();
        }

        public void Dispose()
        {
            if (vlcPlayer.IsPlaying) vlcPlayer.Stop();
            vlcPlayer.Dispose();
            songs.Clear();
        }


        // -------------------------------------------------------------------------------------------------------------------------------

        public string GetMediaDuration(string path)
        {
            Media media = new Media(libvlc, new Uri(path));
            media.Parse();
            while (media.ParsedStatus != MediaParsedStatus.Done) Thread.Sleep(1);
            return (media.Duration / 60000).ToString("D1") + ":" + (media.Duration / 1000 % 60).ToString("D2");
        }

        public void Load()
        {
            ErroneousSongCount = 0;
            string[] dirs = Directory.GetDirectories(BeatSaberRootPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string dir in dirs)
            {
                dynamic info = GetSongInfo(dir);
                if (info == null) continue;
                var data = GetPlaysData(dir, info);
                Song song = new Song(dir, data["plays"], data["highScores"], info);
                song.TrackTime = GetMediaDuration(dir + @"\" + song.Filename);
                if (song.HasErrors()) ErroneousSongCount++;
                songs.Add(song);
            }
        }

        public Song Reload(Song pSong)
        {
            int index = songs.IndexOf(pSong);
            dynamic info = GetSongInfo(pSong.SongDir);
            var data = GetPlaysData(pSong.SongDir, info);
            Song song = new Song(pSong.SongDir, data["plays"], data["highScores"], info);
            song.TrackTime = GetMediaDuration(song.SongDir + @"\" + song.Filename);
            songs.RemoveAt(index);
            songs.Insert(index, song);
            UpdateErrorCount();
            return song;
        }

        public void UpdateErrorCount()
        {
            ErroneousSongCount = 0;
            foreach (Song song in songs)
                if (song.HasErrors()) ErroneousSongCount++;
        }

        public SongList()
        {
            InitVLC();
            GetSongAppData();
        }
    }
}
