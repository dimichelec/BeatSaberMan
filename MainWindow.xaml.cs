using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

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
            int index = FindMyListBoxItem(pSong.SongDir).index;
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
                songList.Clear();
                songList.Load();
                lbSongs.Items.Clear();
                LoadSongs();
            }));
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
                lbSongs.Items.Remove(FindMyListBoxItem(dir));
                Directory.Delete(dir, true);
                RefreshListBox();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlaying();
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
        }
    }
}
