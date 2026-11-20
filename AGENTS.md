# Moonscraper Chart Editor (Geomitron fork)

Geo's fork of FireFox2000000/Moonscraper-Chart-Editor. Its releases come from the branch `geomitron`. A fork build must never look official or replace an official install.

## Boundaries

- Keep `master` an exact copy of upstream `master`: update it with GitHub's Sync fork, and start every upstream PR branch from it. Never use Sync fork on `geomitron`, whose history is linear.
- Keep the fork's label (`Globals.applicationBranchName = "Geomitron"`, shown after the version in the window title and the program info), its own Open with entry (`FileTypeRegistration_Windows`, ProgIDs `MoonscraperChartEditorGeomitron.<ext>`), and the zip package: the official installer's `AppId` would replace an official install.
- Commit no test hook or batch test script: copy it into `Assets/Editor` (a build hook into `Assets/Scripts`) for one run, then delete it.
- Discard the change Unity makes to `ProjectSettings/UnityConnectSettings.asset` when it opens the project.
- A run of a built Moonscraper uses the real user's settings: the folder `%USERPROFILE%\AppData\LocalLow\FireFox Dev\Moonscraper Chart Editor` (config.ini, controls.json, autosave and audio cache files), the registry key `HKCU\Software\FireFox Dev\Moonscraper Chart Editor` (window placement), and the Open with entries under `HKCU\Software\Classes`. Back them up right before each run and restore them right after. The Unity batch build touches none of them.

## Branch layout

`geomitron` is upstream `master`, then each open upstream PR's commits as a group (cherry-picks of the PR branch, in its order), then the fork's own commits, with a version commit for each release.

- Before each build, `git fetch origin` and `git fetch fork`. If an open PR's branch changed, replace its group: branch at the commit below the group, `git cherry-pick <upstream base>..fork/<pr-branch>`, then `git cherry-pick <group's last commit>..geomitron`; check that `git diff geomitron <new>` shows only the PR's change, then move `geomitron` there and force-push.
- Rebase onto upstream: `git rebase origin/master` on `geomitron`, then force-push. A PR upstream merged unchanged drops out by itself; drop a squash-merged PR's group first (`git rebase --onto <parent of its first commit> <its last commit>`). When upstream's version changes, keep upstream's `bundleVersion`.
- A fork-only feature: a branch off `geomitron`, rebased onto it when done and merged with `--ff-only`.

## Commands

- Unity 2018.4.23f1. Activate Unity Personal in the editor's own sign-in window: Unity Hub's license doesn't work in 2018.4.
- Build (about 2 minutes): `env '7-Zip=C:\Program Files\7-Zip\7z.exe' Unity.exe -batchmode -quit -logFile <log> -projectPath "<checkout>/Moonscraper Chart Editor" -executeMethod BuildManager.BuildWindows64 -moonscraperBuildPath <dir>`. The zip step reads 7-Zip's path from the variable `7-Zip`, which Bash can't `export`; without it the build succeeds and logs an assertion.
- Batch test: copy the script into `Assets/Editor`, run Unity as above with `-executeMethod <Class>.Run <its arguments>` in place of the build method, then delete the script.

## Versions and releases

- A version is upstream's plus a fourth part: `1.5.13.1`, `1.5.13.2`, then `1.5.14.1` on upstream 1.5.14. The update checker (`ApplicationUpdateManager`) compares `bundleVersion` with the latest non-prerelease tag on Geomitron/Moonscraper-Chart-Editor as a `System.Version`, so a tag is the plain version, without `v`.
- Release: one commit setting `bundleVersion` in `ProjectSettings/ProjectSettings.asset` (edit it with a file-edit tool: `sed -i` in Git Bash rewrites its CRLF line endings). Build from that commit, push `geomitron`, tag the commit with the plain version, then `gh release create <version> -R Geomitron/Moonscraper-Chart-Editor --verify-tag --title "Moonscraper Chart Editor v<version> Geomitron" --notes-file <notes> <zip>`, with the asset named `Moonscraper.Chart.Editor.v<version>.Geomitron.Win64.zip`. Then run the downloaded zip's build: its log says "Application is considered to be the same or newer than the latest release".

## Verify in a build

- Start the built exe with `-logFile <log> "<chart or .sng>"`: it opens the file, and the log says "Total load time".
- Moonscraper sets its window title only when its window has focus at start. Without focus, answer a message box by posting `WM_COMMAND` with the button's id (Yes 6, No 7, Cancel 2) to the process's visible `#32770` window, and close the editor by posting `WM_CLOSE` to its `UnityWndClass` window.
- Clone Hero 1.1 plays one song straight from its command line, started from its own folder: `--song "<folder>" --player <instrument>,<difficulty>` (guitar, bass, rhythm, guitarcoop, keys, drums, prodrums, and the 6fret variants; expert to easy). The folder must contain notes.mid or notes.chart, song.ini and audio: unpack a `.sng` first (each file's bytes XOR `mask[i & 15] ^ (i & 0xFF)`, with `i` counted from the file's start; the metadata becomes song.ini under `[song]`). Its Player.log says `Loading Song: <artist> - <name>` once the song loaded.

## Behavior Geo decided

- Song metadata lives only in song.ini (`Song.iniProperties`). `[Song]` contains only Resolution, and Length when the chart already has it. Saving writes the chart's original bytes when nothing after `[Song]` changed, so a metadata edit never changes the chart file's hash.
- song.ini is written in the spec's key order (`SongIniKeys`), and a key without a value is left out. Song Properties lists the keys from `SongPropertiesRows`, with the rare ones behind "Show all keys".
- Difficulty tags are added only for instruments with notes, and never as `-1`. A new chart has no difficulty, preview times or genre.
- A `.sng` is saved in place: a save that changes only the chart rewrites the file's end through `sng_save.journal`, and any other save writes a new copy and swaps it in with `File.Replace`. A `.sng` holding notes.mid is saved as a new `.sng` holding notes.chart. No song.ini beside a `.sng` is read or written.
- Discord status is on by default, with "Show Activity on Discord:" in Settings > Editor to turn it off. `DiscordStatus.ApplicationId` is Geo's Discord application.
