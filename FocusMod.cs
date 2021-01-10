﻿using FocusMod.HarmonyPatches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace FocusMod {
	class FocusMod : IInitializable, ITickable {

		static int HiddenHudLayer = 23;
		
		IEnumerable<GameObject> elementsToDisable;
		IEnumerable<GameObject> elementsToHide;

		[Inject] private AudioTimeSyncController audioTimeSyncController = null;

		Type ReplayPlayer = null;
		PropertyInfo ReplayPlayer_playbackEnabled = null;

		struct SafeTimespan {
			public float start;
			public float end;

			public SafeTimespan(float start, float end) {
				this.start = start;
				this.end = end;
			}
		}

		List<SafeTimespan> visibleTimespans = new List<SafeTimespan>(128);

		void getElements() {

			/*
			 * Unforunately, when using Counters+ it is not possible to change the layer of specific counter objects
			 * because, apparently...
			 * 
			 * Kyle 1413 : it's cause the canvas is the only thing actually being rendered i assume
			 * Kyle 1413 : so changing the layer of just the text doesn't do anything since it's not being rendered in the first place
			 * 
			 * so... When using Counters+, having HideAll disabled and HideOnlyInHMD enabled is not possible and it will instead
			 * resort to disabling the score object entirely
			 */

			var counterHud = Resources.FindObjectsOfTypeAll<Canvas>().Where(xd => xd.transform.childCount != 0 && xd.name.StartsWith("Counters+ | ") && xd.isActiveAndEnabled).LastOrDefault();

			elementsToDisable = elementsToHide = new GameObject[] { };

			if(!Configuration.PluginConfig.Instance.HideAll) {
				var _scoreElement =
					Resources.FindObjectsOfTypeAll<ImmediateRankUIPanel>().LastOrDefault()?.gameObject ??
					Resources.FindObjectsOfTypeAll<ScoreUIController>().LastOrDefault()?.gameObject;

				if(counterHud == null) {
					elementsToHide = new GameObject[] { _scoreElement };
				} else {
					elementsToDisable = new GameObject[] {
						_scoreElement,
						counterHud?.transform.Cast<Transform>().Where(x => x.gameObject?.name == "ScoreText").LastOrDefault()?.gameObject
					};
				}
				return;
			} else {
				elementsToHide = new GameObject[] {
					counterHud?.gameObject,
					//_scoreElement,
					Resources.FindObjectsOfTypeAll<ComboUIController>().LastOrDefault()?.gameObject,
					Resources.FindObjectsOfTypeAll<ScoreMultiplierUIController>().LastOrDefault()?.gameObject
				};
			}

			elementsToHide = elementsToHide.Where(x => x?.activeSelf == true);
			elementsToDisable = elementsToDisable.Where(x => x?.activeSelf == true);

			// Hard to read but compact code is nice

			//elementsToHide = new IEnumerable<GameObject>[] {
			//	//idk?.transform.Cast<Transform>().Select(x => x.gameObject) ?? new GameObject[]{ },
			//	new GameObject[] {
			//		counterHud?.gameObject,
			//		_scoreElement,
			//		Resources.FindObjectsOfTypeAll<ComboUIController>().LastOrDefault()?.gameObject,
			//		Resources.FindObjectsOfTypeAll<ScoreMultiplierUIController>().LastOrDefault()?.gameObject
			//	}.Where(x => x != null)
			//}.SelectMany(x => x).Where(x => x.activeSelf);

			/*
			 * I was going to exclude things like the song progress but with Counters+ that isnt really 
			 * possible in an easy fashion because almost everything created by C+ is just a textmesh and
			 * theres no nesting so I'll just hide EVERYTHING for now except the health. I'd probably have
			 * to find a better way to look up the C+ objects
			 */
			//string[] elementsToNotHide = {
			//	"SongProgressCanvas"
			//};

			//elementsToHide = elementsToHide.Where(x => !elementsToNotHide.Any(y => y == x.name));
		}

		void parseSong(IReadOnlyList<IReadonlyBeatmapLineData> beatmapLinesData) {
			float lastObjectTime = 0f;

			var sortedObjects = beatmapLinesData.SelectMany(x => x.beatmapObjectsData).OrderBy(x => x.time);

			void checkAndAdd(float objectTime, bool isLast = false) {
				if(isLast || (objectTime - lastObjectTime - Configuration.PluginConfig.Instance.LeadTime >= Configuration.PluginConfig.Instance.MinimumDisplaytime))
					visibleTimespans.Add(new SafeTimespan(
						lastObjectTime,
						isLast ? objectTime : objectTime - Configuration.PluginConfig.Instance.LeadTime
					));

				lastObjectTime = objectTime;
			}

			foreach(var beatmapObject in sortedObjects) {
				if(beatmapObject.beatmapObjectType == BeatmapObjectType.Obstacle) {
					if(Configuration.PluginConfig.Instance.IgnoreWalls)
						continue;

					var obstacle = beatmapObject as ObstacleData;

					if(obstacle.width == 1 && (obstacle.lineIndex == 0 || obstacle.lineIndex == 3))
						continue;
				} else if(beatmapObject.beatmapObjectType == BeatmapObjectType.Note) {
					if(Configuration.PluginConfig.Instance.IgnoreBombs) {
						var note = beatmapObject as NoteData;

						if(note.colorType == ColorType.None && note.cutDirection == NoteCutDirection.None)
							continue;
					}
				} else {
					continue;
				}

				if(lastObjectTime == beatmapObject.time)
					continue;

				checkAndAdd(beatmapObject.time);
			}

			checkAndAdd(audioTimeSyncController.songLength, true);

#if DEBUG
			Plugin.Log.Notice("Safe timespans in this song:");

			foreach(var x in visibleTimespans)
				Plugin.Log.Notice(String.Format("{0} - {1}", x.start, x.end));
			
			Plugin.Log.Notice(String.Format("Elements to hide: {0}", elementsToHide.Join(x => x.name, ", ")));
			Plugin.Log.Notice(String.Format("Elements to disable: {0}", elementsToDisable.Join(x => x.name, ", ")));
#endif
		}


		public void Initialize() {
			if(Configuration.PluginConfig.Instance.LeadTime == 0f)
				return;

			if(Leveldatahook.difficultyBeatmap.noteJumpMovementSpeed > 0 && 
				Leveldatahook.difficultyBeatmap.noteJumpMovementSpeed < Configuration.PluginConfig.Instance.MinimumNjs)
				return;

			ReplayPlayer = AccessTools.TypeByName("ScoreSaber.ReplayPlayer");
			ReplayPlayer_playbackEnabled = ReplayPlayer?.GetProperty("playbackEnabled", BindingFlags.Public | BindingFlags.Instance);
			if(ReplayPlayer != null && ReplayPlayer_playbackEnabled != null) {
				var x = ((MonoBehaviour)Resources.FindObjectsOfTypeAll(ReplayPlayer).LastOrDefault());

				if(x?.isActiveAndEnabled == true && (bool)ReplayPlayer_playbackEnabled.GetValue(x))
					return;
			}

			getElements();

			parseSong(Leveldatahook.difficultyBeatmap.beatmapData.beatmapLinesData);

			//foreach(var cam in Camera.allCameras) {
			//	if(cam.name == "MainCamera") // This' the VR cam, we do not want to unhide the hud on it
			//		continue;

			if(!Configuration.PluginConfig.Instance.HideOnlyInHMD) {
				Camera.main.cullingMask &= ~(1 << HiddenHudLayer);
			} else {
				Camera.main.cullingMask |= 1 << HiddenHudLayer;
			}
			//}
		}

		byte checkInterval = 0;
		bool isVisible = true;

		private void setHudVisibility(bool visible) {
			foreach(var elem in elementsToHide)
				elem.layer = visible ? 5 : HiddenHudLayer;

			foreach(var elem in elementsToDisable)
				elem.SetActive(visible);
		}

		public void Tick() {
			if(elementsToHide == null || elementsToDisable == null || audioTimeSyncController == null)
				return;

			// No need to do the check every frame
			if(!(!isVisible && Pausehook.isPaused) && checkInterval++ % 3 != 0)
				return;

			if(isVisible && Pausehook.isPaused)
				return;

			foreach(var x in visibleTimespans) {
				if(Pausehook.isPaused || x.start <= audioTimeSyncController.songTime && x.end >= audioTimeSyncController.songTime) {
					if(!isVisible) {
						setHudVisibility(true);

						isVisible = true;
					}
					return;
				}
			}

			if(isVisible) {
				setHudVisibility(false);

				isVisible = false;
			}
		}
	}
}
