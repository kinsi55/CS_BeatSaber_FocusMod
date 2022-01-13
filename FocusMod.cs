using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zenject;

namespace FocusMod {
	class FocusMod : IInitializable, ITickable {
		const int HiddenHudLayer = 23;
		const int NormalHudLayer = 5;

		GameObject[] elementsToHide;

		readonly AudioTimeSyncController audioTimeSyncController = null;
		readonly IDifficultyBeatmap difficultyBeatmap = null;

		public FocusMod(AudioTimeSyncController audioTimeSyncController, IDifficultyBeatmap difficultyBeatmap) {
			this.audioTimeSyncController = audioTimeSyncController;
			this.difficultyBeatmap = difficultyBeatmap;
		}

		static MethodBase ScoreSaber_playbackEnabled =
			IPA.Loader.PluginManager.GetPluginFromId("ScoreSaber")?
			.Assembly.GetType("ScoreSaber.Core.ReplaySystem.HarmonyPatches.PatchHandleHMDUnmounted")?
			.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);

		struct SafeTimespan {
			public float start;
			public float end;

			public SafeTimespan(float start, float end) {
				this.start = start;
				this.end = end;
			}
		}

		int lastTimespanIndex = 0;
		SafeTimespan[] visibleTimespans;

		void getElements() {
			var counterHuds = UnityEngine.Object.FindObjectsOfType<Canvas>()
				.Where(xd => xd.transform.childCount != 0 && xd.name.StartsWith("Counters+ | ", StringComparison.Ordinal))
				.Select(x => x.gameObject)
				.ToArray();

			var _scoreElement =
				UnityEngine.Object.FindObjectOfType<ImmediateRankUIPanel>()?.gameObject ??
				UnityEngine.Object.FindObjectOfType<ScoreUIController>()?.gameObject;

			if(!PluginConfig.Instance.HideAll) {
				if(!_scoreElement.GetComponent<Canvas>())
					_scoreElement.AddComponent<Canvas>();

				elementsToHide = new GameObject[] { _scoreElement };
			} else {
				elementsToHide = new GameObject[] {
					_scoreElement,
					UnityEngine.Object.FindObjectOfType<ScoreMultiplierUIController>()?.gameObject
				}
				.Concat(counterHuds)
				.Concat(
					// Combo panel has sub canvas' which would not hide otherwise
					UnityEngine.Object.FindObjectOfType<ComboUIController>()?.GetComponentsInChildren<Canvas>().Select(x => x.gameObject)
				).ToArray();
			}

			elementsToHide = elementsToHide.Where(x => x?.activeSelf == true).ToArray();
		}

		void ParseMap(IReadOnlyList<IReadonlyBeatmapLineData> beatmapLinesData) {
			float lastObjectTime = 0f;

			var sortedObjects = beatmapLinesData.SelectMany(x => x.beatmapObjectsData).OrderBy(x => x.time);

			var visibleTimespans = new List<SafeTimespan>();

			void CheckAndAdd(float objectTime, bool isLast = false) {
				if(isLast || (objectTime - lastObjectTime - PluginConfig.Instance.LeadTime >= PluginConfig.Instance.MinimumDisplaytime))
					visibleTimespans.Add(new SafeTimespan(
						lastObjectTime,
						isLast ? objectTime : objectTime - PluginConfig.Instance.LeadTime
					));

				lastObjectTime = objectTime;
			}

			foreach(var beatmapObject in sortedObjects) {
				if(lastObjectTime == beatmapObject.time)
					continue;

				if(beatmapObject.beatmapObjectType == BeatmapObjectType.Obstacle) {
					if(PluginConfig.Instance.IgnoreWalls)
						continue;

					var obstacle = (ObstacleData)beatmapObject;

					if(obstacle.width == 1 && (obstacle.lineIndex == 0 || obstacle.lineIndex == 3))
						continue;
				} else if(beatmapObject.beatmapObjectType == BeatmapObjectType.Note) {
					if(PluginConfig.Instance.IgnoreBombs) {
						var note = (NoteData)beatmapObject;

						if(note.colorType == ColorType.None && note.cutDirection == NoteCutDirection.None)
							continue;
					}
				} else {
					continue;
				}

				CheckAndAdd(beatmapObject.time);
			}

			CheckAndAdd(audioTimeSyncController.songLength, true);

			this.visibleTimespans = visibleTimespans.ToArray();
		}


		public void Initialize() {
			if(PluginConfig.Instance.LeadTime == 0f)
				return;

			try {
				if(ScoreSaber_playbackEnabled != null && !(bool)ScoreSaber_playbackEnabled.Invoke(null, null))
					return;
			} catch { }

			var njs = difficultyBeatmap.noteJumpMovementSpeed;
			if(njs == 0)
				njs = BeatmapDifficultyMethods.NoteJumpMovementSpeed(difficultyBeatmap.difficulty);

			if(njs < PluginConfig.Instance.MinimumNjs)
				return;

			ParseMap(difficultyBeatmap.beatmapData.beatmapLinesData);

			SharedCoroutineStarter.instance.StartCoroutine(InitStuff());
		}

		IEnumerator InitStuff() {
			yield return null;

			getElements();

			int HudToggle(int flag, bool show = true) => show ? flag | 1 << HiddenHudLayer : flag & ~(1 << HiddenHudLayer);

			foreach(var cam in Resources.FindObjectsOfTypeAll<Camera>()) {
				if(!PluginConfig.Instance.HideOnlyInHMD || cam.name == "MainCamera") {
					cam.cullingMask = HudToggle(cam.cullingMask, false);

					if(cam.name != "MainCamera")
						continue;

					var x = cam.GetComponent<LIV.SDK.Unity.LIV>();

					if(x != null)
						x.SpectatorLayerMask = HudToggle(x.SpectatorLayerMask, PluginConfig.Instance.HideOnlyInHMD);
				} else {
					cam.cullingMask = HudToggle(cam.cullingMask, (cam.cullingMask & (1 << NormalHudLayer)) != 0);
				}
			}

#if DEBUG
			Plugin.Log.Notice("Safe timespans in this song:");

			foreach(var x in visibleTimespans)
				Plugin.Log.Notice(string.Format("{0} - {1}", x.start, x.end));

			Plugin.Log.Notice(string.Format("Elements to hide: {0}", elementsToHide.Join(x => x.name, ", ")));
#endif
		}

		bool isVisible = true;

		private void SetHudVisibility(bool visible) {
			if(isVisible == visible)
				return;

			isVisible = visible;

			foreach(var elem in elementsToHide)
				elem.layer = visible ? 5 : HiddenHudLayer;
		}

		public void Tick() {
			if(elementsToHide == null)
				return;

			var isPaused = audioTimeSyncController.state != AudioTimeSyncController.State.Playing;

			if(isPaused && isVisible)
				return;

			if(lastTimespanIndex >= visibleTimespans.Length)
				return;

			// If something rewound the time back we need to find a new start index
			if(lastTimespanIndex != 0 && audioTimeSyncController.songTime < visibleTimespans[lastTimespanIndex - 1].end)
				lastTimespanIndex = 0;

			var intendedVisibility = false;

			for(var i = lastTimespanIndex; i < visibleTimespans.Length; i++) {
				var ts = visibleTimespans[i];

				if(ts.start > audioTimeSyncController.songTime)
					break;

				if(ts.end < audioTimeSyncController.songTime) {
					lastTimespanIndex++;
					continue;
				}

				if(ts.start < audioTimeSyncController.songTime) {
					intendedVisibility = true;
					break;
				}
			}

			SetHudVisibility(intendedVisibility || (isPaused && PluginConfig.Instance.UnhideInPause));
		}
	}
}
