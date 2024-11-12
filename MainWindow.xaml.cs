using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Windows.Documents.Serialization;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Windows.Input;
using LibVLCSharp.Shared;

namespace BeatSaberMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // --- Song List and Song Utilities -----------------------------------------------------------------------------------

        readonly SongList songList = new SongList();

        private void LoadSongs()
        {
            foreach (Song song in songList.songs)
                lbSongs.Items.Add(song);
        }

        private bool ReloadSong(Song pSong)
        {
            Song song = songList.Reload(pSong);
            int index = FindMyListBoxItem(pSong.SongPath).index;
            lbSongs.Items.RemoveAt(index);
            lbSongs.Items.Insert(index, song);
            return !song.HasErrors();

        }

        public Button StopPlaying()
        {
            Button tmp = lastPlayed;
            songList.StopPlaying();
            if (lastPlayed != null)
            {
                lastPlayed.Content = new Image() { Source = (ImageSource)FindResource("PlayIcon") };
                lastPlayed = null;
            }
            return tmp;
        }

        private bool ResetSongCustomOrder(string songPath)
        {
            string folderName = Path.GetFileName(songPath);
            if (new Regex(SongList.CustomOrderRegex).IsMatch(folderName))
            {
                string newSongPath = songPath.Replace(folderName, folderName[SongList.CustomOrderPrefixLength..]);
                if (!Directory.Exists(newSongPath))
                    Directory.Move(songPath, newSongPath);
                return true;
            }
            return false;
        }

        private bool UndoCustomSongOrder(bool not_a_test)
        {
            bool change = false;
            Regex regex = new Regex(SongList.CustomOrderRegex);
            foreach (Song item in lbSongs.Items)
            {
                string songPath = item.SongPath;
                string folderName = Path.GetFileName(songPath);
                if (regex.IsMatch(folderName))
                {
                    change = true;
                    if (not_a_test)
                    {
                        songList.ReplaceSongIdInPlayerData(folderName, folderName[SongList.CustomOrderPrefixLength..]);
                        songList.ReplaceSongHashId(folderName, folderName[SongList.CustomOrderPrefixLength..]);
                        ResetSongCustomOrder(songPath);
                    }
                }
            }
            return change;
        }

        // --- MainWindow Managment -------------------------------------------------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();
            RefreshListBox();
        } 

        private void UpdateTopBar()
        {
            tbSongCount.Text = songList.Count().ToString() + " SONGS";
            tbErroneousSongs.Text = "";
            if (songList.ErroneousSongCount > 0)
            {
                tbSongCount.Text += ",";
                tbErroneousSongs.Text = songList.ErroneousSongCount.ToString() + " WITH BEATMAP ERRORS";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            songList.Dispose();
        }

        // --- Control Functional Utilities -----------------------------------------------------------------------------------

        private static Button lastPlayed = null;

        private (Song song, int index) FindMyListBoxItem(string dir)
        {
            foreach (Song item in lbSongs.Items)
            {
                if (lbSongs.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                    if (container.DataContext is Song dat)
                        if (dat.SongPath == dir)
                            return (item, lbSongs.Items.IndexOf(item));
            }
            return (null, -1);
        }

        private void DisableSongList()
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            lbSongs.IsEnabled = false;
            lbSongs.Opacity = 0.1;
        }

        private void EnableSongList()
        {
            lbSongs.IsEnabled = true;
            lbSongs.Opacity = 1;
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void RefreshListBox()
        {
            DisableSongList();
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => {
                songList.Clear();
                songList.Load();
                lbSongs.Items.Clear();
                LoadSongs();
            }));
            EnableSongList();
            lbSongs.IsEnabled = true;
            lbSongs.Opacity = 1;
            UpdateTopBar();
            ResetSortByMenu();
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void ResetSortByMenu()
        {
            foreach (MenuItem item in SortByMenu.Items)
                item.IsChecked = item.Tag.ToString() == "";
            lbSongs.Items.SortDescriptions.Clear();
        }

        // --- Controls Event Handling ----------------------------------------------------------------------------------------

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            if (StopPlaying() == clicked) return;
            string dir = clicked.Tag.ToString();
            songList.PlayByDir(dir);
            clicked.Content = new Image() { Source = (ImageSource)FindResource("StopIcon") };
            lastPlayed = clicked;
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            Process.Start("explorer.exe", dir);
        }

        private void FixButton_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            Song song = songList.FindSongByDir(dir);

            if (MessageBox.Show("Are you sure you want to fix the map files for \""
                + song.SongName + "\"?  This will overwrite its suspicious .dat files!",
                "BeatSaberMan",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Exclamation) != MessageBoxResult.OK) return;

            StopPlaying();
            foreach (dynamic mapsets in song.DifficultyBeatmapSets)
            {
                foreach (dynamic map in mapsets._difficultyBeatmaps)
                {
                    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    if (!song.TestBeatmapFile(map._beatmapFilename.ToString()))
                    {
                        if (!song.FixBeatmap(map._beatmapFilename.ToString()))
                            MessageBox.Show("\"" + map._beatmapFilename.ToString() + "\" NOT fixed!",
                                "BeatSaberMan",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                    }
                }
            }
            ReloadSong(song);
            songList.UpdateErrorCount();
            UpdateTopBar();
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            Button clicked = (Button)sender;
            string dir = clicked.Tag.ToString();
            Song song = songList.FindSongByDir(dir);
            if (MessageBox.Show(
                "Are you sure you want to delete \"" + song.SongName + "\"?  This will trash its folder and its contents!",
                "BeatSaberMan",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Exclamation) == MessageBoxResult.OK)
            {
                lbSongs.Items.Remove(FindMyListBoxItem(dir).song);
                Directory.Delete(dir, true);
                RefreshListBox();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            RefreshListBox();
        }

        private void SaveCustomSongOrder_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "This will modify your custom song folder names and your player data files. " +
                "Do you want to continue?",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                MessageBoxButton.YesNo
            ) == MessageBoxResult.No) return;

            StopPlaying();
            DisableSongList();
            UndoCustomSongOrder(true);
            ulong index = 0;
            foreach (Song item in lbSongs.Items)
            {
                string oldSongPath = item.SongPath;
                string oldDirName = Path.GetFileName(oldSongPath);
                string newDirName = string.Format(@"_{0:D6}_{1}", index, oldDirName);
                string newSongPath = Path.GetDirectoryName(oldSongPath) + @"\" + newDirName;
                songList.ReplaceSongIdInPlayerData(oldDirName, newDirName);
                songList.ReplaceSongHashId(oldSongPath, newSongPath);
                Directory.Move(oldSongPath, newSongPath);
                index++;
            }
            EnableSongList();
            songList.GetSongAppData();
            RefreshListBox();
        }

        private void UndoCleanCustomSongOrder_Click(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            if (!UndoCustomSongOrder(false))
            {
                string prompt = "No songs are set into a custom order. Doing a cleanup pass.";
                MessageBox.Show(
                    prompt,
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation
                );

                bool change = false;
                DisableSongList();
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => {
                    if (songList.CleanSongIdInPlayerData()) change = true;
                    if (songList.CleanSongHashId()) change = true;
                }));
                EnableSongList();
                if (change)
                {
                    songList.GetSongAppData();
                    RefreshListBox();
                }
                return;
            }

            if (MessageBox.Show(
                "This will change the song folder names, putting them back to their original. " + 
                "Do you want to continue?",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                MessageBoxButton.YesNo
            ) == MessageBoxResult.No) return;

            DisableSongList();
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => {
                UndoCustomSongOrder(true);
                songList.CleanSongIdInPlayerData();
                songList.CleanSongHashId();
            }));
            EnableSongList();
            songList.GetSongAppData();
            RefreshListBox();
        }

        private void SortByMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem sortBy = (MenuItem)sender;
            string tag = sortBy.Tag.ToString();
            System.ComponentModel.ListSortDirection direction = System.ComponentModel.ListSortDirection.Ascending;

            // the first character of the Tag is either a '+' or '-', telling us the default sort direction 
            if (tag != "")
            {
                direction = tag[..1] == "+" ?
                    System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending;
                tag = tag[1..]; // dispose of the direction character
            }
            
            if (lbSongs.Items.SortDescriptions.Count > 0)
            {
                bool repeat = lbSongs.Items.SortDescriptions[0].PropertyName == tag;
                if (repeat) // toggle direction
                    direction = lbSongs.Items.SortDescriptions[0].Direction == System.ComponentModel.ListSortDirection.Ascending ?
                        System.ComponentModel.ListSortDirection.Descending : System.ComponentModel.ListSortDirection.Ascending;
            }

            sortBy.IsChecked = true;
            foreach (MenuItem item in ((MenuItem)sortBy.Parent).Items)
            {
                if (item == sortBy) continue;
                item.IsChecked = false;
            }
            lbSongs.Items.SortDescriptions.Clear();
            if (tag != "")
                lbSongs.Items.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(tag, direction));
            lbSongs.ScrollIntoView(lbSongs.Items[0]);
        }
    }
}
