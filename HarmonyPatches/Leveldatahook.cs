using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusMod.HarmonyPatches {
	[HarmonyPatch(typeof(StandardLevelScenesTransitionSetupDataSO), "Init")]
	class Leveldatahook {
		public static IDifficultyBeatmap difficultyBeatmap;
		static void Prefix(IDifficultyBeatmap difficultyBeatmap) {
			Leveldatahook.difficultyBeatmap = difficultyBeatmap;
		}

		[HarmonyPatch(typeof(MissionLevelScenesTransitionSetupDataSO), "Init")]
		private class LeveldatahookM {
			static void Prefix(IDifficultyBeatmap difficultyBeatmap) {
				Leveldatahook.difficultyBeatmap = difficultyBeatmap;
			}
		}

		[HarmonyPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), "Init")]
		private class LeveldatahookMp {
			static void Prefix(IDifficultyBeatmap difficultyBeatmap) {
				Leveldatahook.difficultyBeatmap = difficultyBeatmap;
			}
		}
	}

}
