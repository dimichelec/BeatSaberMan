﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace BeatSaberMan
{
    internal class Song
    {
        public string Filename { get; set; }
        public string SongDir { get; set; }
        public BitmapImage CoverImage { get; set; }
        public string SongName { get; set; }
        public string Artist { get; set; }
        public string MapAuthor { get; set; }
        public string TrackTime { get; set; }
        public int BPM { get; set; }
        public string[] LevelForegrounds { get; set; }
        public string[] LevelBackgrounds { get; set; }
        public string[] Plays { get; set; }
        public dynamic DifficultyBeatmapSets {  get; set; }
        public int TotalPlays { get; set; }
        public string LevelErrors { get; set; }
        public int ErrorCount { get; set; }
        public string FixEnabled { get; set; }

        // --- Map Managing Functions -----------------------------------------------------------------------------------------

        public bool HasErrors()
        {
            return ErrorCount > 0;
        }

        public bool TestBeatmapFile(string mapFilename)
        {
            StreamReader infoFile = new StreamReader(SongDir + @"\" + mapFilename);
            string stored = infoFile.ReadToEnd();
            infoFile.Close();

            // the stored map file has to match this or Beat Saber will not load it (spinning infinitely)
            return new Regex(@"{\s*""_?version"":""\d").IsMatch(stored);
        }

        public Dictionary<string, int> TestBeatmaps(dynamic difficultyBeatmapSets)
        {
            Dictionary<string, int> maps = new Dictionary<string, int>();
            foreach (dynamic mapsets in difficultyBeatmapSets)
            {
                foreach (dynamic map in mapsets._difficultyBeatmaps)
                {
                    maps[map._difficulty.ToString()] = TestBeatmapFile(map._beatmapFilename.ToString()) ? 1 : -1;
                }
            }
            return maps;
        }

        public bool FixBeatmap(string mapFilename)
        {
            string path = SongDir + @"\" + mapFilename;
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

        public string GetLevelBackground(float plays)
        {
            return plays > 0.8 ? "GreenYellow" : plays > 0.5 ? "Yellow" : plays > 0.0 ? "Orange" : "Transparent";
        }

        public dynamic GetMapData(dynamic difficultyBeatmapSets, int[] playCounts)
        {
            int totalPlays = playCounts.Sum();
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["TotalPlays"] = totalPlays
            };

            // get beatmap error test results
            Dictionary<string, int> maps = TestBeatmaps(difficultyBeatmapSets);

            string errors = "";
            int errorCount = 0;
            string[] levels = new[] { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };
            string[] foregrounds = new[] { "", "", "", "", "" };
            string[] backgrounds = new[] { "", "", "", "", "" };
            string[] plays = new[] { "", "", "", "", "" };
            foreach ((string level, int index) in levels.Select((level, index) => (level, index)))
            {
                if (maps.ContainsKey(level))
                {
                    foregrounds[index] = maps[level] == 1 ? "Black" : "Red";
                    backgrounds[index] = "Transparent";
                    plays[index] = playCounts[index].ToString();
                    errors += maps[level] == 1 ? "0" : "1";
                    errorCount += maps[level] == 1 ? 0 : 1;
                }
                else
                {
                    foregrounds[index] = "LightGray";
                    backgrounds[index] = "Transparent";
                    plays[index] = "";
                    errors += "-";
                }
            }

            // get background colors
            string[] tmp = plays.Where(x => x != "" && x != "0").ToArray();
            if (tmp.Length > 0)
            {
                var min = tmp.Min(x => int.Parse(x));
                var max = tmp.Max(x => int.Parse(x));
                foreach ((string level, int index) in levels.Select((level, index) => (level, index)))
                {
                    if (plays[index] != "" && plays[index] != "0")
                    {
                        if (int.Parse(plays[index]) == max) backgrounds[index] = GetLevelBackground(0.9f);
                        else if (int.Parse(plays[index]) == min) backgrounds[index] = GetLevelBackground(0.1f);
                        else backgrounds[index] = GetLevelBackground(0.6f);
                    }
                }
            }

            data["LevelErrors"] = errors;
            data["ErrorCount"] = errorCount;
            data["Foregrounds"] = foregrounds;
            data["Backgrounds"] = backgrounds;
            data["Plays"] = plays;
            return data;
        }

        // --- Constructor ----------------------------------------------------------------------------------------------------

        public Song(string dir, int[] plays, dynamic info)
        {
            SongDir = dir;

            // create the cover image in the song object
            CoverImage = new BitmapImage();
            CoverImage.BeginInit();
            CoverImage.CacheOption = BitmapCacheOption.OnLoad;
            CoverImage.UriSource = new Uri(dir + @"\" + info._coverImageFilename);
            CoverImage.EndInit();

            // copy top-level song data
            Filename = info._songFilename;
            SongName = info._songName;
            Artist = info._songAuthorName;
            MapAuthor = info._levelAuthorName;
            BPM = (int)info._beatsPerMinute;
            DifficultyBeatmapSets = info._difficultyBeatmapSets;

            // copy map data values
            dynamic mapData = GetMapData(info._difficultyBeatmapSets, plays);
            LevelForegrounds = mapData["Foregrounds"];
            LevelBackgrounds = mapData["Backgrounds"];
            Plays = mapData["Plays"];
            TotalPlays = mapData["TotalPlays"];
            LevelErrors = mapData["LevelErrors"];
            ErrorCount = mapData["ErrorCount"];

            FixEnabled = ErrorCount > 0 ? "True" : "False";
        }
    }

}
