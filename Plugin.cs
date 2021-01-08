﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine.SceneManagement;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using System.Reflection;
using HarmonyLib;

namespace FocusMod {

	[Plugin(RuntimeOptions.SingleStartInit)]
	public class Plugin {
		internal static Plugin Instance { get; private set; }
		internal static IPALogger Log { get; private set; }

		public static Harmony harmony;

		[Init]
		/// <summary>
		/// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
		/// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
		/// Only use [Init] with one Constructor.
		/// </summary>
		public void Init(IPALogger logger) {
			Instance = this;
			Log = logger;
			Log.Info("FocusMod initialized.");
		}

		#region BSIPA Config
		[Init]
		public void InitWithConfig(Config conf)
		{
				Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
				Log.Debug("Config loaded");
		}
		#endregion

		[OnStart]
		public void OnApplicationStart() {
			Log.Debug("OnApplicationStart");
			new GameObject("FocusModController").AddComponent<FocusModController>();

			harmony = new Harmony("Kinsi55.BeatSaber.FocusMod");
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			SceneManager.activeSceneChanged += OnActiveSceneChanged;
		}
		public void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
			if(newScene.name != "GameCore")
				FocusModController.Instance.reset();
		}

		[OnExit]
		public void OnApplicationQuit() {
			Log.Debug("OnApplicationQuit");

		}
	}
}
