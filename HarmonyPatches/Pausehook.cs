using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusMod.HarmonyPatches {
	[HarmonyPatch(typeof(PauseAnimationController))]
	[HarmonyPatch("StartEnterPauseAnimation")]
	class Pausehook {
		public static bool isPaused = false;

		static void Postfix() {
			isPaused = true;
		}

		[HarmonyPatch(typeof(PauseAnimationController))]
		[HarmonyPatch("StartResumeFromPauseAnimation")]
		private class Unpausehook {
			static void Postfix() {
				isPaused = false;
			}
		}

		[HarmonyPatch(typeof(PauseAnimationController))]
		[HarmonyPatch("Awake")]
		private class Awakehook {
			static void Postfix() {
				isPaused = false;
			}
		}
	}
}
