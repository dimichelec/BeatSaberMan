using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace BeatSaberMan
{
    internal class SongList
    {
        private LibVLC libvlc;
        private MediaPlayer vlcPlayer;

        readonly string BeatSaberRootPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            + @"\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels";

        readonly string BeatSaberDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + @"\..\LocalLow\Hyperbolic Magnetism\Beat Saber\";

        string BeatSaberSongHashPath = null;
        string BeatSaberPlayerDataPath = null;

        public const int CustomOrderPrefixLength = 8;
        public const string CustomOrderRegex = @"^_\d{6}_";

        public int ErroneousSongCount { get; set; }

        public List<Song> songs = new List<Song>();

        private Dictionary<string, object> PlayerData;
        private List<Dictionary<string, object>> LocalPlayerData;
        private Dictionary<string, Dictionary<string, object>> SongHashes;

        // -------------------------------------------------------------------------------------------------------------------------------

        public void GetSongAppData()
        {
            SongHashes = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(
                new StreamReader(BeatSaberSongHashPath).ReadToEnd());
            PlayerData = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                new StreamReader(BeatSaberPlayerDataPath).ReadToEnd());
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
            int[] maxCombos = { -1, -1, -1, -1, -1 };
            string levelDirName = "custom_level_" + dir.Split(@"\")[^1];
            string songHash = SongHashes.ContainsKey(dir) ? SongHashes[dir]["songHash"].ToString() : "";
            foreach (dynamic dat in LocalPlayerData)
            {
                if ((dat["levelId"] == info._songName.ToString()) || (dat["levelId"] == levelDirName) || (dat["levelId"] == songHash))
                {
                    plays[(int)dat["difficulty"]] += (int)dat["playCount"];
                    highScores[(int)dat["difficulty"]] = (int)dat["highScore"];
                    int maxcombo = (int)dat["maxCombo"];
                    maxCombos[(int)dat["difficulty"]] = (bool)dat["fullCombo"] ? -maxcombo : maxcombo;
                }
            }
            var dict = new Dictionary<string, int[]>
            {
                { "plays", plays },
                { "highScores", highScores },
                { "maxCombos", maxCombos }
            };
            return dict;
        }

        public bool CleanSongIdInPlayerData()
        {
            StreamReader reader = new StreamReader(BeatSaberPlayerDataPath);
            JObject jsonPlayerData = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            bool change = false;
            bool keep_searching = true;
            Regex regex = new Regex(CustomOrderRegex);
            while (keep_searching)
            {
                keep_searching = false;
                foreach (var player in jsonPlayerData["localPlayers"])
                {
                    // change element names in "localPlayers"."favoritesLevelIds" array
                    foreach (var id in player["favoritesLevelIds"])
                    {
                        string folderName = Path.GetFileName((string)id);
                        if ((folderName.Length > (13 + CustomOrderPrefixLength)) &&
                            (folderName[..13] == "custom_level_") && 
                            (regex.IsMatch(folderName[13..])))
                        {
                            id.Replace("custom_level_" + folderName[13..][CustomOrderPrefixLength..]);
                            change = keep_searching = true;
                            break;
                        }
                    }
                    if (keep_searching) break;

                    // change element names in all relevant "localPlayers"."levelsStatsData"[].{ "levelId" }
                    foreach (var stats in player["levelsStatsData"])
                    {
                        string folderName = Path.GetFileName((string)stats["levelId"]);
                        if ((folderName.Length > (13 + CustomOrderPrefixLength)) &&
                            (folderName[..13] == "custom_level_") &&
                            (regex.IsMatch(folderName[13..])))
                        {
                            stats["levelId"].Replace("custom_level_" + folderName[13..][CustomOrderPrefixLength..]);
                            change = keep_searching = true;
                            break;
                        }
                    }
                    if (keep_searching) break;
                }
            }

            if (change)
            {
                StreamWriter writer = new StreamWriter(BeatSaberPlayerDataPath);
                writer.WriteLine(JsonConvert.SerializeObject(jsonPlayerData, Formatting.None));
                writer.Close();
            }
            return change;
        }

        public bool CleanSongHashId()
        {
            StreamReader reader = new StreamReader(BeatSaberSongHashPath);
            JObject jsonSongHashes = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            bool change = false;
            bool keep_searching = true;
            Regex regex = new Regex(CustomOrderRegex);
            while (keep_searching)
            {
                keep_searching = false;
                foreach (var hash in jsonSongHashes)
                {
                    string folderName = Path.GetFileName(hash.Key);
                    if (regex.IsMatch(folderName))
                    {
                        string newKey = Path.GetDirectoryName(hash.Key) + @"\" + folderName[CustomOrderPrefixLength..];
                        string node = "{"
                            + "\"directoryHash\":" + (string)hash.Value["directoryHash"] + ","
                            + "\"songHash\":\"" + (string)hash.Value["songHash"] + "\""
                            + "}";
                        jsonSongHashes.Remove(hash.Key);
                        jsonSongHashes.Add(newKey, JObject.Parse(node));
                        change = keep_searching = true;
                    }
                    if (keep_searching) break;
                }
            }
            if (change)
            {
                StreamWriter writer = new StreamWriter(BeatSaberSongHashPath);
                writer.WriteLine(JsonConvert.SerializeObject(jsonSongHashes, Formatting.None));
                writer.Close();
                return true;
            }
            return false;
        }

        public bool ReplaceSongIdInPlayerData(string oldID, string newID)
        {
            StreamReader reader = new StreamReader(BeatSaberPlayerDataPath);
            JObject jsonPlayerData = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            bool change = false;
            bool keep_searching = true;
            while (keep_searching) {
                keep_searching = false;
                foreach (var player in jsonPlayerData["localPlayers"])
                {
                    // change element names in "localPlayers"."favoritesLevelIds" array
                    foreach (var id in player["favoritesLevelIds"])
                    {
                        if ((string)id == "custom_level_" + oldID)
                        {
                            id.Replace("custom_level_" + newID);
                            change = keep_searching = true;
                            break;
                        }
                    }
                    if (keep_searching) break;

                    // change element names in all relevant "localPlayers"."levelsStatsData"[].{ "levelId" }
                    foreach (var stats in player["levelsStatsData"])
                    {
                        if ((string)stats["levelId"] == "custom_level_" + oldID)
                        {
                            stats["levelId"].Replace("custom_level_" + newID);
                            change = keep_searching = true;
                            break;
                        }
                    }
                    if (keep_searching) break;
                }
            }

            if (change)
            {
                StreamWriter writer = new StreamWriter(BeatSaberPlayerDataPath);
                writer.WriteLine(JsonConvert.SerializeObject(jsonPlayerData, Formatting.None));
                writer.Close();
            }
            return change;
        }

        public bool ReplaceSongHashId(string oldPath, string newPath)
        {
            StreamReader reader = new StreamReader(BeatSaberSongHashPath);
            JObject jsonSongHashes = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            if (jsonSongHashes.Property(oldPath) != null)
            {
                string node = "{"
                    + "\"directoryHash\":" + (string)jsonSongHashes[oldPath]["directoryHash"] + ","
                    + "\"songHash\":\"" + (string)jsonSongHashes[oldPath]["songHash"] + "\""
                    + "}";
                jsonSongHashes.Remove(oldPath);
                jsonSongHashes.Add(newPath, JObject.Parse(node));

                StreamWriter writer = new StreamWriter(BeatSaberSongHashPath);
                writer.WriteLine(JsonConvert.SerializeObject(jsonSongHashes, Formatting.None));
                writer.Close();
                return true;
            }
            return false;
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
                if (song.SongPath.Equals(dir))
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
                if (song.SongPath.Equals(dir)) return song;
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
                Song song = new Song(dir, data["plays"], data["highScores"], data["maxCombos"], info);
                song.TrackTime = GetMediaDuration(dir + @"\" + song.Filename);
                if (song.HasErrors()) ErroneousSongCount++;
                songs.Add(song);
            }
        }

        public Song Reload(Song pSong)
        {
            int index = songs.IndexOf(pSong);
            dynamic info = GetSongInfo(pSong.SongPath);
            var data = GetPlaysData(pSong.SongPath, info);
            Song song = new Song(pSong.SongPath, data["plays"], data["highScores"], data["maxCombos"], info);
            song.TrackTime = GetMediaDuration(song.SongPath + @"\" + song.Filename);
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
            BeatSaberSongHashPath = BeatSaberDataPath + @"\SongHashData.dat";
            BeatSaberPlayerDataPath = BeatSaberDataPath + @"\PlayerData.dat";
            InitVLC();
            GetSongAppData();
        }
    }
}
