
# BeatSaberMan

[![GitHub](https://img.shields.io/badge/license-MIT-green)](#License)

BeatSaberMan is an open source Beat Saber custom song manager written by [Carmen DiMichele](https://dimichelec.wixsite.com/carmendimichele) 

This C# Windows WPF app makes it easy to review the list of custom songs you currently have installed
  on Beat Saber, see play stats, fix a song that has bad beatmaps that won't load, and delete a song folder and its contents.

##### To install BeatSaberMan on a Windows machine, run Installer/Debug/BeatSaberMan Windows x64 Installer.msi

## To use BeatSaberMan

Running BeatSaberMan (BSM) will show the list of your installed custom songs, which on a Windows machine
are installed at `%PROGRAMFILES(X86)%\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels`.

The `Sort` menu item lets you change the order of the list as viewed in BSM.
To let Beat Saber have the same custom song order, use the `Custom Order | Save Custom Song Order` menu
item.

> **BEWARE: Using the `Custom Order | Save Custom Song Order` command from the menu will change the
> folder names of your custom songs and modify your `PlayerData.dat` and `SongHashData.dat` files.**

Use the `Custom Order | Undo/Clean Custom Song Order` to undo the changes made by
`Custom Order | Save Custom Song Order`.

The recycle button under the menu reloads the list of custom songs.

The buttons along the right edge of each song cell, from top to bottom, let you play the song's audio,
open the folder where the song is installed, fix the song's data files
(only active if the song's data files are not working), or delete the song.


---
# License

> Copyright 2023 Carmen DiMichele
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.



<!-- --------------------------------------------------------------------

Coding Notes:


- https://github.com/dimichelec/BeatSaberMan
- https://www.reddit.com/r/beatsaber/comments/ts61vd/comment/jei0uc2/?context=3
- %PROGRAMFILES(X86)%\Steam\steamapps\common\Beat Saber\Beat Saber_Data\CustomLevels
- %APPDATA%\..\LocalLow\Hyperbolic Magnetism\Beat Saber\PlayerData.dat

* make backups of PlayerData.dat and SongHashData.dat before modifying for sort
* add top area stats and functions:
  - count of maps with errors, etc.
  - button to fix all erroneous maps

* allow edit of Title, Artist, Map Author

* select practice mode or something to signal a delete in BSM

* is there a way to setup a filter in BSM that will show-up in BS, or
  re-order list to show up in BS in a differnt order

* add a way to mark a song just in this UI

* clean-up code

* how can you make the title cell crop the end if the text is too long
  so a horizontal scrollbar doesn't appear when the window is at its smallest
  
-------------------------------------------------------------------- -->



