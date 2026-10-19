// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.Threading.Tasks;
using MoonscraperEngine;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

public class AutoSaveSystem : SystemManagerState.System
{
    const float AUTOSAVE_RUN_INTERVAL = 60; // Once a minute
    float autosaveTimer = 0;
    Song autosaveSong = null;
    Task currentAutosaveTask;

    public override void SystemUpdate()
    {
        // Count towards the next autosave only while none is running
        if (currentAutosaveTask == null || currentAutosaveTask.IsCompleted)
        {
            autosaveTimer += Time.deltaTime;

            if (autosaveTimer > AUTOSAVE_RUN_INTERVAL)
            {
                currentAutosaveTask = Autosave();
            }
        }
        else
        {
            autosaveTimer = 0;
        }
    }

    async Task Autosave()
    {
        ChartEditor editor = ChartEditor.Instance;

        autosaveSong = new Song(editor.currentSong);
        string sngAutosave = editor.currentSngSong != null ? JsonUtility.ToJson(editor.currentSngSong.CreateAutosave(editor.currentSong)) : null;

        await Task.Run(() =>
        {
            autosaveTimer = 0;
            Debug.Log("Autosaving...");

            try
            {
                // Written first, so an autosaved chart is never paired with another song's .sng
                Globals.WriteSngAutosave(sngAutosave);

                ChartWriter.ErrorReport errorReport;
                ExportOptions exportOptions = editor.currentSong.defaultExportOptions;
                exportOptions.writeAudioLocations = true;   // The autosave isn't in the song's directory, recovering from a crash needs these to find the audio
                new ChartWriter(Globals.autosaveLocation).Write(autosaveSong, exportOptions, out errorReport);

                if (errorReport.HasErrors && errorReport.hasNonErrorFileTypeRelatedErrors)
                {
                    string saveErrorMessage = errorReport.GetFullReport();
                    string errorMessage = "Autosave completed with the following errors: " + System.Environment.NewLine + saveErrorMessage;
                    ChartEditor.Instance.errorManager.QueueErrorMessage(errorMessage);
                    Debug.LogError(errorMessage);
                }
            }
            catch (System.Exception e)
            {
                ChartEditor.Instance.errorManager.QueueErrorMessage("Autosave failed!" + System.Environment.NewLine + e.Message);
                Logger.LogException(e, "Autosave failed!");
            }

            Debug.Log("Autosave complete!");
            autosaveTimer = 0;
        });
    }
}
