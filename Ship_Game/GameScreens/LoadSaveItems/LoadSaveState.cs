using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.LoadGame;
using Ship_Game.Audio;
using System.IO;
using SDUtils;


namespace Ship_Game
{
    internal class LoadSaveState : GenericLoadSaveScreen
    {
        GameScreen Screen;

        public LoadSaveState(UniverseScreen screen)
            : base(screen, SLMode.Load, "", Localizer.Token(GameText.LoadSavedGame), "Saved Games", showSaveExport: true)
        {
            Screen = screen;
            Path = SavedGame.DefaultSaveGameFolder;
        }

        public LoadSaveState(MainMenuScreen screen)
            : base(screen, SLMode.Load, "", Localizer.Token(GameText.LoadSavedGame), "Saved Games", showSaveExport: true)
        {
            Screen = screen;
            Path = SavedGame.DefaultSaveGameFolder;
        }

        protected override void Load()
        {
            if (SelectedFile != null)
            {
                // if caller was UniverseScreen, this will Unload the previous universe
                Screen?.ExitScreen();
                ScreenManager.AddScreen(new LoadUniverseScreen(SelectedFile.FileLink));
            }
            else
            {
                GameAudio.NegativeClick();
            }
            ExitScreen();
        }

        // Set list of files to show
        protected override void InitSaveList()
        {
            FileInfo[] saveFiles = Dir.GetFiles(Path, "sav");
            var saves = new Array<FileData>();
            foreach (FileInfo saveFile in saveFiles)
            {
                try
                {
                    HeaderData header = LoadGame.PeekHeader(saveFile);

                    // GlobalStats.ModName is "" if no active mods
                    if (header is { Version: SavedGame.SaveGameVersion } // null if saveFile is not a valid binary save
                        && header.ModName == GlobalStats.ModName)
                    {
                        saves.Add(FileData.FromSaveHeader(saveFile, header));
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"Error parsing SaveGame header {saveFile.Name}: {e.Message}");
                }
            }

            AddItemsToSaveSL(saves.OrderByDescending(header => (header.Data as HeaderData)?.Time));
        }
    }
}
