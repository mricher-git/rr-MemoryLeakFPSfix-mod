using Enviro;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using Game.State;
using HarmonyLib;
using MemoryLeakFPSfix.UMM;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace MemoryLeakFPSfix
{

	public class MemoryLeakFPSfix : MonoBehaviour
	{
		public enum MapStates { MAINMENU, MAPLOADED, MAPUNLOADING }
		public static MapStates MapState { get; private set; } = MapStates.MAINMENU;
		internal Loader.MemoryLeakFPSfixSettings Settings;

		public static MemoryLeakFPSfix Instance
		{
			get { return Loader.Instance; }
		}

		void Start()
		{
			Messenger.Default.Register<MapDidLoadEvent>(this, new Action<MapDidLoadEvent>(this.OnMapDidLoad));
			Messenger.Default.Register<MapWillUnloadEvent>(this, new Action<MapWillUnloadEvent>(this.OnMapWillUnload));

			if (StateManager.Shared.Storage != null)
			{
				OnMapDidLoad(new MapDidLoadEvent());
			}
		}

		private void OnMapDidLoad(MapDidLoadEvent evt)
		{
			Loader.LogDebug("OnMapDidLoad");
			if (MapState == MapStates.MAPLOADED) return;

			MapState = MapStates.MAPLOADED;
			Messenger.Default.Register<WorldDidMoveEvent>(this, new Action<WorldDidMoveEvent>(this.WorldDidMove));

			if (Enviro.EnviroManager.instance) Enviro.EnviroManager.instance.Reflections.Settings.globalReflectionsUpdateOnPosition = false;
		}

		private void OnMapWillUnload(MapWillUnloadEvent evt)
		{
			Loader.LogDebug("OnMapWillUnload");

			MapState = MapStates.MAPUNLOADING;
			Messenger.Default.Unregister<WorldDidMoveEvent>(this);
		}

		private void WorldDidMove(WorldDidMoveEvent evt)
		{
			Loader.LogDebug("WorldDidMove");
			EnviroManager.instance?.Reflections.RenderGlobalReflectionProbe(true);
		}

		void OnDestroy()
		{
			Loader.LogDebug("OnDestroy");

			Messenger.Default.Unregister<MapDidLoadEvent>(this);
			Messenger.Default.Unregister<MapWillUnloadEvent>(this);

			if (MapState == MapStates.MAPLOADED)
			{
				OnMapWillUnload(new MapWillUnloadEvent());
			}

			MapState = MapStates.MAINMENU;
		}

		public void OnSettingsChanged()
		{
			if (MapState != MapStates.MAPLOADED) return;
		}
	}
}

namespace MemoryLeakFPSfix.Patches
{
	[HarmonyPatch(typeof(Effects.Decals.CanvasDecalRenderer), nameof(Effects.Decals.CanvasDecalRenderer.Render), typeof(Vector2), typeof(string), typeof(string))]
	static class CanvasDecalRendererPatch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			try
			{
				var codeMatcher = new CodeMatcher(instructions)
					.MatchEndForward(
					new CodeMatch(OpCodes.Ldloc_S),
					new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RenderTexture), nameof(RenderTexture.Release))))
					.ThrowIfNotMatch("Could not find CanvasDecalRenderer.Render")
					.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.RenderTexture), nameof(UnityEngine.RenderTexture.ReleaseTemporary), new[] { typeof(UnityEngine.RenderTexture) })));
				return codeMatcher.InstructionEnumeration();
			} catch (Exception e)
			{
				Loader.Log("CanvasDecalRenderer.Render not found");
				return instructions;
			}
		}
	}

	[HarmonyPatch(typeof(EnviroManager), nameof(EnviroManager.Start))]
	static class EnviroUpdateOnPositionPatch
	{
		static bool Prefix(EnviroReflectionsModule ___Reflections)
		{
			___Reflections.Settings.globalReflectionsUpdateOnPosition = false;
			return true;
		}
		static void Postfix(EnviroReflectionsModule ___Reflections)
		{
			___Reflections.RenderGlobalReflectionProbe(true);
		}
	}
}
