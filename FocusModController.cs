using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Settings;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace FocusMod {
	/// <summary>
	/// Monobehaviours (scripts) are added to GameObjects.
	/// For a full list of Messages a Monobehaviour can receive from the game, see https://docs.unity3d.com/ScriptReference/MonoBehaviour.html.
	/// </summary>
	public class FocusModController : MonoBehaviour {
		public static FocusModController Instance { get; private set; }
		
		public AudioTimeSyncController audioTimeSyncController;
		ScoreUIController scoreUIController;

		Type ReplayPlayer = null;
		PropertyInfo ReplayPlayer_playbackEnabled = null;

		struct SafeTimespan {
			public float start;
			public float end;
			//public float duration;

			public SafeTimespan(float start, float end) {
				this.start = start;
				this.end = end;
				//this.duration = end - start;
			}
		}
		List<SafeTimespan> safeTimespans = new List<SafeTimespan>(128);

		public void reset() {
			scoreUIController = null;
			isVisible = true;
			checkInterval = 0;

			safeTimespans.Clear();
		}

		public void prepareSong(IReadOnlyList<IReadonlyBeatmapLineData> beatmapLinesData) {
			if(Configuration.PluginConfig.Instance.MinimumDowntime == 0f) {
				scoreUIController = null;
				return;
			}

			if(ReplayPlayer != null && ReplayPlayer_playbackEnabled != null) {
				var x = ((MonoBehaviour)Resources.FindObjectsOfTypeAll(ReplayPlayer).LastOrDefault());

				if(x?.isActiveAndEnabled == true && (bool)ReplayPlayer_playbackEnabled.GetValue(x) == true)
					return;
			}

			audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().LastOrDefault();
			scoreUIController = Resources.FindObjectsOfTypeAll<ScoreUIController>().LastOrDefault();


			float lastObjectTime = 0f;

			var sortedObjects = beatmapLinesData.SelectMany(x => x.beatmapObjectsData).OrderBy(x => x.time);

			void checkAndAdd(float objectTime, bool force = false) {
				if(force || objectTime - lastObjectTime > Configuration.PluginConfig.Instance.MinimumDowntime)
					safeTimespans.Add(new SafeTimespan(
						lastObjectTime,
						force ? objectTime : objectTime - Math.Min(Configuration.PluginConfig.Instance.MinimumDowntime, Configuration.PluginConfig.Instance.LeadTime)
					));

				lastObjectTime = objectTime;
			};

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

			foreach(var x in safeTimespans)
				Plugin.Log.Notice(String.Format("{0} - {1}", x.start, x.end));
#endif
		}

		// These methods are automatically called by Unity, you should remove any you aren't using.
		#region Monobehaviour Messages
		/// <summary>
		/// Only ever called once, mainly used to initialize variables.
		/// </summary>
		private void Awake() {
			// For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
			//   and destroy any that are created while one already exists.
			if(Instance != null) {
				Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
				GameObject.DestroyImmediate(this);
				return;
			}
			GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
			Instance = this;
			Plugin.Log?.Debug($"{name}: Awake()");

			// Doing it via reflection so I dont need to ref SS
			ReplayPlayer = AccessTools.TypeByName("ScoreSaber.ReplayPlayer");
			ReplayPlayer_playbackEnabled = ReplayPlayer.GetProperty("playbackEnabled", BindingFlags.Public | BindingFlags.Instance);
		}
		/// <summary>
		/// Only ever called once on the first frame the script is Enabled. Start is called after any other script's Awake() and before Update().
		/// </summary>
		//private void Start() {

		//}

		/// <summary>
		/// Called every frame if the script is enabled.
		/// </summary>
		//private void Update() {

		//}

		/// <summary>
		/// Called every frame after every other enabled script's Update().
		/// </summary>
		byte checkInterval = 0;
		bool isVisible = false;
		private void LateUpdate() {
			if(scoreUIController == null || audioTimeSyncController == null)
				return;
			// No need to do the check every frame
			if(checkInterval++ % 3 != 0)
				return;

			foreach(var x in safeTimespans) {
				if(x.start <= audioTimeSyncController.songTime && x.end >= audioTimeSyncController.songTime) {
					if(!isVisible)
						scoreUIController.gameObject.SetActive(isVisible = true);
					return;
				}
			}

			if(isVisible)
				scoreUIController.gameObject.SetActive(isVisible = false);
		}

		/// <summary>
		/// Called when the script becomes enabled and active
		/// </summary>
		private void OnEnable() {
			BSMLSettings.instance.AddSettingsMenu("Focus Mod", "FocusMod.Views.settings.bsml", Configuration.PluginConfig.Instance);
		}

		/// <summary>
		/// Called when the script becomes disabled or when it is being destroyed.
		/// </summary>
		//private void OnDisable() {

		//}

		/// <summary>
		/// Called when the script is being destroyed.
		/// </summary>
		private void OnDestroy() {
			Plugin.Log?.Debug($"{name}: OnDestroy()");
			if(Instance == this)
				Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.

		}
		#endregion
	}

	[HarmonyPatch(typeof(BeatmapObjectCallbackController))]
	[HarmonyPatch("SetNewBeatmapData")]
	class PatchBeatmapObjectCallbackController {
		static void Prefix(IReadonlyBeatmapData beatmapData) {
			if(FocusModController.Instance == null)
				return;

			FocusModController.Instance.prepareSong(beatmapData.beatmapLinesData);
		}
	}
}
